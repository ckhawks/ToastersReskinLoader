using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol;

// Harmony patches that instrument suspected stutter sources. Tracks both
// time AND allocation per call to find GC pressure sources. Uses manual
// patching so one failed target doesn't prevent the rest from loading.
public static class FrameProfilerPatches
{
    const float REPORT_THRESHOLD_MS = 2f;
    const long ALLOC_REPORT_THRESHOLD = 32 * 1024;
    const float REPORT_INTERVAL = 10f;

    const long SPIKE_ALLOC_THRESHOLD = 1 * 1024 * 1024;
    const float SPIKE_TIME_THRESHOLD_MS = 50f;
    const float SPIKE_TOAST_COOLDOWN = 5f;
    static float lastSpikeToastTime;

    static readonly SystemStats[] stats = new SystemStats[(int)TrackedSystem.COUNT];
    static float lastReportTime;

    static readonly Dictionary<string, EventAllocStats> eventAllocStats = new Dictionary<string, EventAllocStats>();

    // Per-frame attribution: which instrumented call was the most expensive
    // in the current Unity frame? Finalized on frame change and exposed for
    // the overlay's spike attribution.
    static int trackedFrame = -1;
    static float frameWinnerMs = 0f;
    static string frameWinnerName = "";
    static int lastFinalizedFrame = -1;
    static float lastFinalizedMs = 0f;
    static string lastFinalizedName = "";

    public static bool TryGetSpikeAttribution(int forFrame, out string name, out float ms)
    {
        // The spike-firing frame should be the one we just finalized
        // (RecordAndReport ran during it, then Unity moved to next frame).
        if (forFrame == lastFinalizedFrame)
        {
            name = lastFinalizedName;
            ms = lastFinalizedMs;
            return !string.IsNullOrEmpty(name);
        }
        name = "";
        ms = 0f;
        return false;
    }

    // Live snapshot for the Top Calls overlay mode. Returns count of
    // populated entries.
    public struct TopCallEntry
    {
        public string Name;
        public int Calls;
        public float TotalMs;
        public float MaxMs;
        public long TotalBytes;
    }
    public static int GetSystemCount() => (int)TrackedSystem.COUNT;
    public static TopCallEntry GetSystemSnapshot(int i) => new TopCallEntry
    {
        Name = stats[i].Name,
        Calls = stats[i].CallCount,
        TotalMs = stats[i].TotalMs,
        MaxMs = stats[i].MaxMs,
        TotalBytes = stats[i].TotalAllocBytes,
    };

    enum TrackedSystem
    {
        GameManagerTick,
        SteamCallbackLoop,
        PhysicsSimulate,
        SyncObjectTick,
        SyncObjectGather,
        ReplayRecorderTick,
        EventManagerTrigger,
        PlayerManagerGetPlayers,
        PuckManagerGetPucks,
        COUNT
    }

    struct SystemStats
    {
        public string Name;
        public int CallCount;
        public float TotalMs;
        public float MaxMs;
        public long TotalAllocBytes;
        public long MaxAllocBytes;

        public void Record(float ms, long allocBytes)
        {
            CallCount++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
            TotalAllocBytes += allocBytes;
            if (allocBytes > MaxAllocBytes) MaxAllocBytes = allocBytes;
        }

        public void Reset()
        {
            CallCount = 0;
            TotalMs = 0;
            MaxMs = 0;
            TotalAllocBytes = 0;
            MaxAllocBytes = 0;
        }
    }

    struct EventAllocStats
    {
        public int Count;
        public long TotalBytes;
    }

    static FrameProfilerPatches()
    {
        stats[(int)TrackedSystem.GameManagerTick]         = new SystemStats { Name = "GameManager.Server_Tick" };
        stats[(int)TrackedSystem.SteamCallbackLoop]       = new SystemStats { Name = "SteamMgr.StartCallbackLoop" };
        stats[(int)TrackedSystem.PhysicsSimulate]         = new SystemStats { Name = "PhysicsManager.Update" };
        stats[(int)TrackedSystem.SyncObjectTick]          = new SystemStats { Name = "SyncObjMgr.Server_ServerTick" };
        stats[(int)TrackedSystem.SyncObjectGather]        = new SystemStats { Name = "SyncObjMgr.GatherData" };
        stats[(int)TrackedSystem.ReplayRecorderTick]      = new SystemStats { Name = "ReplayRecorder.Server_Tick" };
        stats[(int)TrackedSystem.EventManagerTrigger]     = new SystemStats { Name = "EventManager.TriggerEvent" };
        stats[(int)TrackedSystem.PlayerManagerGetPlayers] = new SystemStats { Name = "PlayerManager.GetPlayers" };
        stats[(int)TrackedSystem.PuckManagerGetPucks]     = new SystemStats { Name = "PuckManager.GetPucks" };
    }

    public static void ApplyPatches(Harmony harmony)
    {
        var targets = new (string label, Type type, string method, Type patchClass)[]
        {
            ("GameManager.Server_Tick",                        typeof(GameManager),                "Server_Tick",                       typeof(Patch_GameManagerTick)),
            ("SteamManager.StartCallbackLoop",                 typeof(SteamManager),               "StartCallbackLoop",                 typeof(Patch_SteamCallbackLoop)),
            ("PhysicsManager.Update",                          typeof(PhysicsManager),             "Update",                            typeof(Patch_PhysicsUpdate)),
            ("SyncObjMgr.Server_ServerTick",                   typeof(SynchronizedObjectManager),  "Server_ServerTick",                 typeof(Patch_SyncObjectTick)),
            ("SyncObjMgr.Server_GatherSynchronizedObjectData", typeof(SynchronizedObjectManager),  "Server_GatherSynchronizedObjectData", typeof(Patch_SyncObjectGather)),
            ("ReplayRecorder.Server_Tick",                     typeof(ReplayRecorder),             "Server_Tick",                       typeof(Patch_ReplayRecorderTick)),
            ("EventManager.TriggerEvent",                      typeof(EventManager),               "TriggerEvent",                      typeof(Patch_EventManagerTrigger)),
            ("PlayerManager.GetPlayers",                       typeof(PlayerManager),              "GetPlayers",                        typeof(Patch_PlayerManagerGetPlayers)),
            ("PuckManager.GetPucks",                           typeof(PuckManager),                "GetPucks",                          typeof(Patch_PuckManagerGetPucks)),
        };

        int succeeded = 0;
        foreach (var (label, type, method, patchClass) in targets)
        {
            try
            {
                var original = AccessTools.Method(type, method);
                if (original == null)
                {
                    Plugin.Log($"[FrameProfiler][PATCH] SKIP {label} - method not found");
                    continue;
                }

                var prefix = AccessTools.Method(patchClass, "Prefix");
                var postfix = AccessTools.Method(patchClass, "Postfix");

                harmony.Patch(original,
                    prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix: postfix != null ? new HarmonyMethod(postfix) : null);

                Plugin.Log($"[FrameProfiler][PATCH] OK   {label}");
                succeeded++;
            }
            catch (Exception e)
            {
                Plugin.Log($"[FrameProfiler][PATCH] FAIL {label} - {e.Message}");
            }
        }

        Plugin.Log($"[FrameProfiler][PATCH] {succeeded}/{targets.Length} patches applied successfully");
    }

    static void TrackFrameWinner(string name, float ms)
    {
        int f = Time.frameCount;
        if (f != trackedFrame)
        {
            lastFinalizedFrame = trackedFrame;
            lastFinalizedMs = frameWinnerMs;
            lastFinalizedName = frameWinnerName;
            trackedFrame = f;
            frameWinnerMs = 0f;
            frameWinnerName = "";
        }
        if (ms > frameWinnerMs)
        {
            frameWinnerMs = ms;
            frameWinnerName = name;
        }
    }

    static void RecordAndReport(TrackedSystem system, float elapsedMs, long allocBytes, string detail = null)
    {
        stats[(int)system].Record(elapsedMs, allocBytes);
        TrackFrameWinner(stats[(int)system].Name, elapsedMs);

        if (elapsedMs > REPORT_THRESHOLD_MS || allocBytes > ALLOC_REPORT_THRESHOLD)
        {
            string extra = detail != null ? $" ({detail})" : "";
            Plugin.Log($"[FrameProfiler][STUTTER] {stats[(int)system].Name} time={elapsedMs:F2}ms alloc={FormatBytes(allocBytes)}{extra}");
        }

        if ((allocBytes > SPIKE_ALLOC_THRESHOLD || elapsedMs > SPIKE_TIME_THRESHOLD_MS)
            && Time.unscaledTime - lastSpikeToastTime > SPIKE_TOAST_COOLDOWN)
        {
            lastSpikeToastTime = Time.unscaledTime;
            string name = stats[(int)system].Name;
            string msg = $"[Profiler] {name} - {elapsedMs:F0}ms / {FormatBytes(allocBytes)}";
            Plugin.Log($"[FrameProfiler][SPIKE ALERT] {msg}");
            try { MonoBehaviourSingleton<UIManager>.Instance.ToastManager.ShowToast("tfp_spike", msg, 4f); } catch { }
        }

        CheckPeriodicReport();
    }

    static void CheckPeriodicReport()
    {
        if (Time.unscaledTime - lastReportTime >= REPORT_INTERVAL)
        {
            lastReportTime = Time.unscaledTime;
            PrintAggregatedReport();
        }
    }

    static void PrintAggregatedReport()
    {
        Plugin.Log("[FrameProfiler] === System Timing + Allocation Report (last 10s) ===");
        for (int i = 0; i < (int)TrackedSystem.COUNT; i++)
        {
            ref var s = ref stats[i];
            if (s.CallCount > 0)
            {
                float avgMs = s.TotalMs / s.CallCount;
                long avgAlloc = s.CallCount > 0 ? s.TotalAllocBytes / s.CallCount : 0;
                Plugin.Log($"[FrameProfiler]  {s.Name,-35} calls={s.CallCount,6}  time: total={s.TotalMs,7:F0}ms avg={avgMs,5:F2}ms max={s.MaxMs,5:F2}ms | alloc: total={FormatBytes(s.TotalAllocBytes),8} avg={FormatBytes(avgAlloc),6} max={FormatBytes(s.MaxAllocBytes),6}");
            }
            s.Reset();
        }

        if (eventAllocStats.Count > 0)
        {
            Plugin.Log("[FrameProfiler] --- Top Event Allocators ---");
            var sorted = new List<KeyValuePair<string, EventAllocStats>>(eventAllocStats);
            sorted.Sort((a, b) => b.Value.TotalBytes.CompareTo(a.Value.TotalBytes));
            int shown = 0;
            foreach (var kv in sorted)
            {
                if (shown >= 10) break;
                long avg = kv.Value.Count > 0 ? kv.Value.TotalBytes / kv.Value.Count : 0;
                Plugin.Log($"[FrameProfiler]    {kv.Key,-45} calls={kv.Value.Count,5}  total={FormatBytes(kv.Value.TotalBytes),8}  avg={FormatBytes(avg),6}");
                shown++;
            }
            eventAllocStats.Clear();
        }

        // Also log the top built-in profiler markers for this window, then
        // reset their accumulators so they track the same 10s window as
        // the Harmony patches.
        int markerCount = FrameProfilerBuiltinMarkers.GetCount();
        if (markerCount > 0)
        {
            var rows = new FrameProfilerBuiltinMarkers.SystemStats[markerCount];
            for (int i = 0; i < markerCount; i++) rows[i] = FrameProfilerBuiltinMarkers.GetSnapshot(i);
            Array.Sort(rows, (a, b) => b.TotalMs.CompareTo(a.TotalMs));
            Plugin.Log("[FrameProfiler] --- Top Unity Built-in Markers (last 10s) ---");
            int shown = 0;
            for (int i = 0; i < rows.Length && shown < 10; i++)
            {
                var r = rows[i];
                if (r.Calls == 0 && r.TotalMs <= 0f) continue;
                float avg = r.Calls > 0 ? r.TotalMs / r.Calls : 0f;
                Plugin.Log($"[FrameProfiler]    {r.Name,-45} samples={r.Calls,6}  total={r.TotalMs,7:F0}ms avg={avg,5:F2}ms max={r.MaxMs,5:F2}ms");
                shown++;
            }
            FrameProfilerBuiltinMarkers.ResetWindow();
        }

        // Per-mod rollup (only populated if mod instrumentation toggle is on).
        int modCount = FrameProfilerMods.GetCount();
        if (modCount > 0)
        {
            var mods = new List<FrameProfilerMods.ModStats>(FrameProfilerMods.Snapshot());
            mods.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));
            Plugin.Log("[FrameProfiler] --- Per-Mod Cost (last 10s) ---");
            int shown = 0;
            foreach (var m in mods)
            {
                if (shown >= 10) break;
                if (m.Calls == 0 && m.TotalMs <= 0f) continue;
                float avg = m.Calls > 0 ? m.TotalMs / m.Calls : 0f;
                Plugin.Log($"[FrameProfiler]    {m.ModName,-40} patched={m.PatchedMethods,3}  calls={m.Calls,6}  total={m.TotalMs,7:F0}ms avg={avg,5:F2}ms max={m.MaxMs,5:F2}ms  alloc={FormatBytes(m.TotalAllocBytes),8}");
                shown++;
            }
            FrameProfilerMods.ResetAllWindows();
        }

        Plugin.Log("[FrameProfiler] =============================================");
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1048576) return $"{bytes / 1024f:F1}KB";
        return $"{bytes / 1048576f:F1}MB";
    }

    // All timing patches use Harmony's per-call `out PerCallState __state` instead of shared
    // static Stopwatch/memBefore fields. The shared-static form corrupts measurements under
    // reentrancy (notably EventManager.TriggerEvent, whose handlers can fire nested events):
    // the inner call's Prefix would reset the timer before the outer Postfix read it. With
    // __state each invocation carries its own start timestamp/memory on the call frame.
    static float ElapsedMs(long startTicks) =>
        (float)(Stopwatch.GetTimestamp() - startTicks) / Stopwatch.Frequency * 1000f;

    public static class Patch_GameManagerTick
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.GameManagerTick, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_SteamCallbackLoop
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.SteamCallbackLoop, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_PhysicsUpdate
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.PhysicsSimulate, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_SyncObjectTick
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.SyncObjectTick, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_SyncObjectGather
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.SyncObjectGather, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_ReplayRecorderTick
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.ReplayRecorderTick, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_EventManagerTrigger
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }

        public static void Postfix(string eventName, FrameProfilerMods.PerCallState __state)
        {
            float ms = ElapsedMs(__state.Ticks);
            long alloc = Math.Max(0, GC.GetTotalMemory(false) - __state.Mem);

            stats[(int)TrackedSystem.EventManagerTrigger].Record(ms, alloc);
            TrackFrameWinner($"EventManager.TriggerEvent(\"{eventName}\")", ms);

            if (alloc > 0)
            {
                if (eventAllocStats.TryGetValue(eventName, out var existing))
                {
                    existing.Count++;
                    existing.TotalBytes += alloc;
                    eventAllocStats[eventName] = existing;
                }
                else
                {
                    eventAllocStats[eventName] = new EventAllocStats { Count = 1, TotalBytes = alloc };
                }
            }

            if (ms > REPORT_THRESHOLD_MS || alloc > ALLOC_REPORT_THRESHOLD)
            {
                Plugin.Log($"[FrameProfiler][STUTTER] EventManager.TriggerEvent(\"{eventName}\") time={ms:F2}ms alloc={FormatBytes(alloc)}");
            }

            if ((alloc > SPIKE_ALLOC_THRESHOLD || ms > SPIKE_TIME_THRESHOLD_MS)
                && Time.unscaledTime - lastSpikeToastTime > SPIKE_TOAST_COOLDOWN)
            {
                lastSpikeToastTime = Time.unscaledTime;
                string msg = $"[Profiler] {eventName} - {ms:F0}ms / {FormatBytes(alloc)}";
                Plugin.Log($"[FrameProfiler][SPIKE ALERT] {msg}");
                try { MonoBehaviourSingleton<UIManager>.Instance.ToastManager.ShowToast("tfp_spike", msg, 4f); } catch { }
            }

            CheckPeriodicReport();
        }
    }

    public static class Patch_PlayerManagerGetPlayers
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.PlayerManagerGetPlayers, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }

    public static class Patch_PuckManagerGetPucks
    {
        public static void Prefix(out FrameProfilerMods.PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(FrameProfilerMods.PerCallState __state)
        {
            long alloc = GC.GetTotalMemory(false) - __state.Mem;
            RecordAndReport(TrackedSystem.PuckManagerGetPucks, ElapsedMs(__state.Ticks), Math.Max(0, alloc));
        }
    }
}
