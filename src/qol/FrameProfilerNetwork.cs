using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToasterReskinLoader.qol;

// Client-side network instrumentation. Patches the server→client sync RPC
// (Server_SynchronizeObjectsRpc on SynchronizedObjectManager) to capture
// arrival timing, payload size, and dropped-tick events. RTT is sampled
// every frame from the underlying transport.
//
// SynchronizedObjectData is 16 bytes (8 shorts/ushorts). Per-RPC overhead
// is ~10 bytes (varint tickId + double serverTime), so payload bytes are
// approximated as 10 + count*16.
public static class FrameProfilerNetwork
{
    public const int RING_SIZE = 512;
    const int SYNC_DATA_BYTES = 16;
    const int RPC_OVERHEAD_BYTES = 10;

    // Per-tick samples
    public static readonly float[] arrivalTimes = new float[RING_SIZE];
    public static readonly int[] payloadBytes = new int[RING_SIZE];
    public static readonly int[] payloadCounts = new int[RING_SIZE];
    public static readonly ushort[] tickIds = new ushort[RING_SIZE];
    // sampleGap[i] = number of ticks missing between the previous sample
    // and this one (0 = no drop). Used to visualize packet loss.
    public static readonly int[] sampleGap = new int[RING_SIZE];
    public static int ringIndex = 0;
    public static int totalSamples = 0;

    // Per-frame RTT samples + parallel rolling-window loss % samples
    // (snapshotted at the same cadence so they can share an X axis).
    public const int RTT_RING = 600;
    public static readonly float[] rttMs = new float[RTT_RING];
    public static readonly float[] rttTimes = new float[RTT_RING];
    public static readonly float[] lossPct = new float[RTT_RING];
    public static int rttIndex = 0;
    public static int rttTotal = 0;

    public static int droppedTickCount = 0;
    // Per-Unity-frame tick arrival counter. Each tick RPC increments it;
    // the overlay consumes+resets it once per Update so each frame gets
    // an accurate "ticks processed this frame" count for correlating
    // client stutters with server-side issues.
    public static int frameTickCounter = 0;
    public static int ConsumeAndResetFrameTickCount()
    {
        int v = frameTickCounter;
        frameTickCounter = 0;
        return v;
    }
    public static ushort lastTickId = 0;
    public static bool hasFirstTick = false;
    public static float lastArrivalTime = 0f;

    // Cached aggregates (refreshed at ~10Hz by overlay)
    public static float currentTickRateHz = 0f;
    public static float currentBytesPerSec = 0f;
    public static float currentRttMs = 0f;
    public static float jitterMs = 0f;
    public static float meanInterArrivalMs = 0f;
    public static int lastDroppedSnapshot = 0;
    public static int droppedInWindow = 0;
    // Auto-scaled RTT y-axis cap. Updated in RefreshAggregates.
    public static float rttAxisMaxMs = 50f;
    // Current rolling-window loss %, snapshotted into lossPct per RTT sample.
    public static float currentLossPct = 0f;

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            var method = AccessTools.Method(typeof(SynchronizedObjectManager), "Server_SynchronizeObjectsRpc");
            if (method == null)
            {
                Plugin.Log("[FrameProfiler][NET] SKIP Server_SynchronizeObjectsRpc - method not found");
                return;
            }
            var postfix = AccessTools.Method(typeof(Patch_SyncRpcReceived), nameof(Patch_SyncRpcReceived.Postfix));
            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
            Plugin.Log("[FrameProfiler][NET] OK   Server_SynchronizeObjectsRpc (client receive instrumentation)");
        }
        catch (Exception e)
        {
            Plugin.Log($"[FrameProfiler][NET] FAIL net patch: {e.Message}");
        }
    }

    public static void SampleRtt()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.NetworkConfig?.NetworkTransport == null) return;
            // On the client, the transport tracks RTT keyed by the *peer*
            // we're connected to — which is the server (ServerClientId = 0).
            // Calling GetCurrentRtt(LocalClientId) returns 0.
            ulong rtt = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            // Fallback: if the transport returns 0, use the server-published
            // Player.Ping NetworkVariable (slower, ~1Hz, but available).
            if (rtt == 0)
            {
                var pm = MonoBehaviourSingleton<PlayerManager>.Instance;
                var local = pm != null ? pm.GetLocalPlayer() : null;
                if (local != null && local.Ping != null) rtt = local.Ping.Value;
            }
            rttMs[rttIndex] = rtt;
            rttTimes[rttIndex] = Time.unscaledTime;
            lossPct[rttIndex] = ComputeRollingLossPct(1f);
            rttIndex = (rttIndex + 1) % RTT_RING;
            if (rttTotal < RTT_RING) rttTotal++;
            currentRttMs = rtt;
        }
        catch { }
    }

    // Rolling packet loss %, computed over the last `windowSec` of arrivals.
    // loss = totalDropped / (totalDropped + totalArrived) * 100
    static float ComputeRollingLossPct(float windowSec)
    {
        float now = Time.unscaledTime;
        int count = Math.Min(totalSamples, RING_SIZE);
        if (count == 0) return 0f;
        int start = (totalSamples < RING_SIZE) ? 0 : ringIndex;
        int arrivals = 0;
        long dropped = 0;
        for (int k = 0; k < count; k++)
        {
            int idx = (start + k) % RING_SIZE;
            if (arrivalTimes[idx] < now - windowSec) continue;
            arrivals++;
            dropped += sampleGap[idx];
        }
        long total = arrivals + dropped;
        currentLossPct = total > 0 ? (dropped * 100f) / total : 0f;
        return currentLossPct;
    }

    public static void RecordTickArrival(ushort tickId, int objectCount)
    {
        float now = Time.unscaledTime;
        arrivalTimes[ringIndex] = now;
        payloadCounts[ringIndex] = objectCount;
        payloadBytes[ringIndex] = RPC_OVERHEAD_BYTES + objectCount * SYNC_DATA_BYTES;
        tickIds[ringIndex] = tickId;
        // sampleGap is set further below (after we compute the gap), then
        // ringIndex is advanced.

        // Dropped-tick detection: tickId should be lastTickId+1 (wrapping).
        // Anything bigger than +1 indicates lost ticks; the game itself
        // drops late (out-of-order) ticks separately.
        int gapForThisSample = 0;
        if (hasFirstTick)
        {
            ushort expected = (ushort)(lastTickId + 1);
            ushort gap = (ushort)(tickId - expected);
            // gap is 0 for in-order; small positive = missed N ticks; very
            // large = wrap or out-of-order. Clamp at 32 to avoid counting
            // wraps as massive losses.
            if (gap > 0 && gap < 32)
            {
                droppedTickCount += gap;
                gapForThisSample = gap;
            }
        }
        sampleGap[ringIndex] = gapForThisSample;
        ringIndex = (ringIndex + 1) % RING_SIZE;
        if (totalSamples < RING_SIZE) totalSamples++;
        lastTickId = tickId;
        hasFirstTick = true;
        lastArrivalTime = now;
        frameTickCounter++;
    }

    // Refresh aggregates over the last `windowSec` seconds. Called by the
    // overlay's 10Hz refresh tick, not every frame.
    public static void RefreshAggregates(float windowSec = 1f)
    {
        float now = Time.unscaledTime;
        int count = Math.Min(totalSamples, RING_SIZE);
        int hits = 0;
        long bytes = 0;
        float earliest = now;
        // Inter-arrival time accumulation for jitter (stddev).
        float interSum = 0f;
        int interCount = 0;
        float lastT = -1f;
        // Walk the ring oldest→newest. Index of oldest = ringIndex when full.
        int start = (totalSamples < RING_SIZE) ? 0 : ringIndex;
        for (int k = 0; k < count; k++)
        {
            int idx = (start + k) % RING_SIZE;
            float t = arrivalTimes[idx];
            if (t < now - windowSec) { lastT = t; continue; }
            hits++;
            bytes += payloadBytes[idx];
            if (t < earliest) earliest = t;
            if (lastT > 0)
            {
                interSum += (t - lastT);
                interCount++;
            }
            lastT = t;
        }

        float span = Math.Max(0.001f, now - earliest);
        currentTickRateHz = hits / span;
        currentBytesPerSec = bytes / span;

        if (interCount > 1)
        {
            float mean = interSum / interCount;
            // Recompute stddev (same window walk).
            float varSum = 0f;
            lastT = -1f;
            int interC2 = 0;
            for (int k = 0; k < count; k++)
            {
                int idx = (start + k) % RING_SIZE;
                float t = arrivalTimes[idx];
                if (t < now - windowSec) { lastT = t; continue; }
                if (lastT > 0)
                {
                    float d = (t - lastT) - mean;
                    varSum += d * d;
                    interC2++;
                }
                lastT = t;
            }
            jitterMs = interC2 > 0 ? Mathf.Sqrt(varSum / interC2) * 1000f : 0f;
            meanInterArrivalMs = mean * 1000f;
        }
        else
        {
            jitterMs = 0f;
            meanInterArrivalMs = 0f;
        }

        droppedInWindow = droppedTickCount - lastDroppedSnapshot;
        lastDroppedSnapshot = droppedTickCount;

        // Auto-scale RTT axis: find peak in the RTT ring, snap to a nice band.
        int rttCount = Math.Min(rttTotal, RTT_RING);
        float peak = 0f;
        for (int i = 0; i < rttCount; i++)
            if (rttMs[i] > peak) peak = rttMs[i];
        rttAxisMaxMs =
            peak <= 25f   ? 25f   :
            peak <= 50f   ? 50f   :
            peak <= 100f  ? 100f  :
            peak <= 200f  ? 200f  :
            peak <= 300f  ? 300f  :
            peak <= 500f  ? 500f  :
            peak <= 1000f ? 1000f :
            Mathf.Ceil(peak / 250f) * 250f;
    }

    public static void Reset()
    {
        ringIndex = 0; totalSamples = 0;
        rttIndex = 0; rttTotal = 0;
        droppedTickCount = 0; lastDroppedSnapshot = 0; droppedInWindow = 0;
        hasFirstTick = false;
        currentTickRateHz = 0f; currentBytesPerSec = 0f; currentRttMs = 0f;
        jitterMs = 0f; meanInterArrivalMs = 0f;
        currentLossPct = 0f;
        Array.Clear(sampleGap, 0, sampleGap.Length);
        Array.Clear(lossPct, 0, lossPct.Length);
    }

    public static class Patch_SyncRpcReceived
    {
        public static void Postfix(ushort tickId, double serverTime, SynchronizedObjectData[] synchronizedObjectsData)
        {
            try
            {
                // Server side also runs this same method (it's how it
                // dispatches), so guard against double-counting on host.
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && !nm.IsClient) return;
                int count = synchronizedObjectsData != null ? synchronizedObjectsData.Length : 0;
                RecordTickArrival(tickId, count);
            }
            catch { }
        }
    }
}
