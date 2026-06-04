using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace ToasterReskinLoader.qol;

// IMGUI overlay that tracks frame times, detects spikes, and helps isolate
// stutter sources. F4 = cycle display mode, F5 = toggle CSV logging.
// Overlay visibility is controlled by the QoL toggle (Enable/Disable).
public class FrameProfilerOverlay : MonoBehaviour
{
    const int FRAME_HISTORY = 600;
    const float SPIKE_THRESHOLD_MS = 20f;
    const int SPIKE_LOG_MAX = 50;
    const float SUMMARY_INTERVAL = 5f;
    // Cap drawn graph bars to this many per graph. With ~460px of graph
    // width that gives ~3-4 source samples per drawn bar; we max-merge so
    // spikes still show. Drops IMGUI draw calls from ~460/graph to ~150.
    const int MAX_GRAPH_BARS = 150;
    // Sample RTT/loss every Nth frame instead of every frame.
    const int RTT_SAMPLE_INTERVAL_FRAMES = 4;

    int displayMode = 0;
    readonly float[] frameTimes = new float[FRAME_HISTORY];
    // Ticks-processed parallel ring: how many server tick RPCs arrived
    // during each Unity frame. >=3 in a single frame ≈ server backlog
    // burst (multiple delayed ticks landing at once → client must process
    // them all in one frame → can drive a frame-time spike that's NOT
    // the client's fault).
    readonly int[] ticksPerFrame = new int[FRAME_HISTORY];
    const int BACKLOG_BURST_THRESHOLD = 3;
    int frameIndex = 0;
    int totalFrames = 0;

    readonly List<SpikeEntry> spikeLog = new List<SpikeEntry>();
    int spikeCount = 0;

    float minFrameTime = float.MaxValue;
    float maxFrameTime = 0f;
    float sumFrameTime = 0f;
    int statsFrameCount = 0;
    float lastSummaryTime = 0f;

    long lastTotalMemory = 0;
    float lastGcTime = 0f;
    int gcEventsInWindow = 0;
    readonly float[] gcTimestamps = new float[32];
    int gcTimestampIndex = 0;

    float lastSpikeTime = 0f;
    readonly float[] spikeIntervals = new float[32];
    int spikeIntervalIndex = 0;
    int spikeIntervalCount = 0;

    // Cached aggregates refreshed at ~5Hz from Update() instead of recomputed
    // every OnGUI pass. The sort + string formatting was the dominant
    // overlay cost.
    readonly float[] sortBuffer = new float[FRAME_HISTORY];
    float cachedOnePercentLow = 0f;
    float cachedAvgFrameMs = 0f;
    float cachedAvgFps = 0f;
    // Frame-time axis auto-scales to the worst frame in the window (snapped
    // to a nice band) so low-ms graphs aren't squished against the baseline.
    float frameTimeAxisMs = 25f;
    int cachedBacklogFrames = 0;
    int cachedServerSuspectedSpikes = 0;
    string cachedServerLine = "";
    string cachedLine1 = "";
    string cachedLine2 = "";
    string cachedLine3 = "";
    string cachedLine4 = "";
    float lastStatRefreshTime = -1f;
    float lastBakeTime = -1f;
    const float BAKE_INTERVAL = 1f / 90f; // 90 Hz graph repaint

    bool csvLogging = false;
    StringBuilder csvBuffer = new StringBuilder();
    int csvFlushCounter = 0;
    string csvPath;

    float monitorUpdateTimer = 0f;
    long monoHeapSize = 0;
    long monoUsedSize = 0;
    long totalAllocatedMemory = 0;
    long totalReservedMemory = 0;
    int currentGcCount = 0;

    // CPU/GPU split from Unity's built-in ProfilerRecorder counters.
    // FrameTimingManager requires "Frame Timing Stats" enabled in Player
    // Settings which we can't toggle on a shipped game — ProfilerRecorder
    // works on every platform without that setting.
    ProfilerRecorder mainThreadRecorder;
    ProfilerRecorder renderThreadRecorder;
    ProfilerRecorder gfxWaitRecorder;
    ProfilerRecorder vsyncWaitRecorder;
    readonly float[] cpuMainHistory = new float[FRAME_HISTORY];
    readonly float[] cpuRenderHistory = new float[FRAME_HISTORY];
    readonly float[] gfxWaitHistory = new float[FRAME_HISTORY];
    float lastCpuMain = 0f;
    float lastCpuRender = 0f;
    float lastGfxWait = 0f;
    float lastVsyncWait = 0f;
    bool frameTimingAvailable = false;
    string cachedCpuGpuLine = "";
    string cachedCpuGpuVerdict = "";

    Texture2D graphBg;
    Texture2D spikeLine;
    Texture2D barGreen;
    Texture2D barYellow;
    Texture2D barRed;
    Texture2D fpsLine;
    Texture2D fpsRefLine;
    Texture2D lossLine;
    Texture2D dropMarker;

    // Baked graphs — pixel-buffered, repainted at ~10Hz, drawn with one
    // GUI.DrawTexture each per frame.
    BakedGraph bakedFrame;
    BakedGraph bakedFps;
    BakedGraph bakedRtt;
    BakedGraph bakedTick;
    const int GRAPH_W = 460;
    const int FRAME_H = 180;
    const int FPS_H = 190;
    const int RTT_H = 120;
    const int TICK_H = 100;
    GUIStyle labelStyle;
    GUIStyle headerStyle;
    GUIStyle graphTitleStyle;
    GUIStyle rttMaxLabelStyle;
    GUIStyle lossMaxLabelStyle;
    GUIStyle boxStyle;
    bool stylesInitialized = false;

    struct SpikeEntry
    {
        public int FrameNumber;
        public float TimeMs;
        public float TimeSinceStart;
        public float IntervalSinceLast;
        public long MemoryDelta;
        public bool GcOccurred;
        public string AttributedCall;   // most expensive instrumented call in that frame
        public float AttributedMs;
        public int TicksThisFrame;       // server tick RPCs processed during this Unity frame
        public bool ServerSuspected;     // ticksThisFrame >= BACKLOG_BURST_THRESHOLD
    }

    void Start()
    {
        csvPath = Application.persistentDataPath + "/frame_profiler_log.csv";
        CreateTextures();
        // Built-in Unity profiler markers — these are always recorded
        // internally; ProfilerRecorder just subscribes to read them.
        // Values are returned in nanoseconds.
        mainThreadRecorder   = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
        // GPU wait: time the render thread spent blocked on Present (= GPU
        // busy / GPU-bound). High value => GPU bottleneck.
        gfxWaitRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", 1);
        // VSync wait: time CPU spent idle hitting the target frame rate.
        // High value => CPU has headroom (capped/vsynced).
        vsyncWaitRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "WaitForTargetFPS", 1);
        FrameProfilerBuiltinMarkers.Start();
        // Pre-allocate baked graphs. The semi-transparent black bg matches
        // the original graphBg texture so the panel composites the same.
        var bgC = new Color(0f, 0f, 0f, 0.75f);
        bakedFrame = new BakedGraph(GRAPH_W, FRAME_H, bgC);
        bakedFps   = new BakedGraph(GRAPH_W, FPS_H,   bgC);
        bakedRtt   = new BakedGraph(GRAPH_W, RTT_H,   bgC);
        bakedTick  = new BakedGraph(GRAPH_W, TICK_H,  bgC);
        Plugin.Log($"[FrameProfiler] Overlay started. Spike threshold: {SPIKE_THRESHOLD_MS}ms");
        Plugin.Log($"[FrameProfiler] CSV log path: {csvPath}");
    }

    void CreateTextures()
    {
        graphBg = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.75f));
        spikeLine = MakeTex(1, 1, new Color(1f, 0f, 0f, 0.5f));
        barGreen = MakeTex(1, 1, new Color(0.2f, 0.9f, 0.2f, 0.9f));
        barYellow = MakeTex(1, 1, new Color(0.9f, 0.9f, 0.2f, 0.9f));
        barRed = MakeTex(1, 1, new Color(0.9f, 0.2f, 0.2f, 0.9f));
        fpsLine = MakeTex(1, 1, new Color(0.4f, 0.8f, 1f, 1f));
        fpsRefLine = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        lossLine = MakeTex(1, 1, new Color(1f, 0.3f, 0.3f, 0.95f));
        dropMarker = MakeTex(1, 1, new Color(1f, 0.2f, 0.2f, 0.35f));
    }

    void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.8f, 1f) }
        };

        graphTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            wordWrap = false,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = graphBg }
        };

        rttMaxLabelStyle = new GUIStyle(labelStyle); rttMaxLabelStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
        lossMaxLabelStyle = new GUIStyle(labelStyle); lossMaxLabelStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float dtMs = dt * 1000f;

        frameTimes[frameIndex] = dtMs;
        // Sample CPU/GPU timings BEFORE advancing frameIndex so they land
        // in the same slot as this frame's frameTimes value.
        SampleFrameTimings();
        FrameProfilerBuiltinMarkers.Sample();
        cpuMainHistory[frameIndex] = lastCpuMain;
        cpuRenderHistory[frameIndex] = lastCpuRender;
        gfxWaitHistory[frameIndex] = lastGfxWait;
        ticksPerFrame[frameIndex] = FrameProfilerNetwork.ConsumeAndResetFrameTickCount();

        if (Time.frameCount % RTT_SAMPLE_INTERVAL_FRAMES == 0)
            FrameProfilerNetwork.SampleRtt();
        // Kick a background ICMP probe to the connected server when due.
        // Cheap no-op until SetTarget gives it an IP (done in the stat refresh).
        FrameProfilerIcmp.Poll();

        frameIndex = (frameIndex + 1) % FRAME_HISTORY;
        totalFrames++;

        statsFrameCount++;
        sumFrameTime += dtMs;
        if (dtMs < minFrameTime) minFrameTime = dtMs;
        if (dtMs > maxFrameTime) maxFrameTime = dtMs;

        long currentMemory = GC.GetTotalMemory(false);
        long memDelta = currentMemory - lastTotalMemory;
        int gcCount = GC.CollectionCount(0);
        bool gcOccurred = gcCount > currentGcCount;
        if (gcOccurred)
        {
            currentGcCount = gcCount;
            gcTimestamps[gcTimestampIndex] = Time.unscaledTime;
            gcTimestampIndex = (gcTimestampIndex + 1) % gcTimestamps.Length;
            gcEventsInWindow++;
            lastGcTime = Time.unscaledTime;
        }
        lastTotalMemory = currentMemory;

        if (dtMs > SPIKE_THRESHOLD_MS)
        {
            spikeCount++;
            float interval = Time.unscaledTime - lastSpikeTime;

            // Spike attribution: pull the most expensive instrumented call
            // recorded in the frame that just ended (Time.frameCount - 1).
            string attrName;
            float attrMs;
            if (!FrameProfilerPatches.TryGetSpikeAttribution(Time.frameCount - 1, out attrName, out attrMs))
            {
                attrName = "";
                attrMs = 0f;
            }

            // ticksPerFrame was written above with the index that just
            // recorded this frame's data — (frameIndex - 1) after the
            // advance.
            int spikeIdx = (frameIndex - 1 + FRAME_HISTORY) % FRAME_HISTORY;
            int ticksThisFrame = ticksPerFrame[spikeIdx];

            var entry = new SpikeEntry
            {
                FrameNumber = Time.frameCount,
                TimeMs = dtMs,
                TimeSinceStart = Time.unscaledTime,
                IntervalSinceLast = lastSpikeTime > 0 ? interval : 0f,
                MemoryDelta = memDelta,
                GcOccurred = gcOccurred,
                AttributedCall = attrName,
                AttributedMs = attrMs,
                TicksThisFrame = ticksThisFrame,
                ServerSuspected = ticksThisFrame >= BACKLOG_BURST_THRESHOLD,
            };

            if (spikeLog.Count < SPIKE_LOG_MAX)
                spikeLog.Add(entry);
            else
                spikeLog[spikeCount % SPIKE_LOG_MAX] = entry;

            if (lastSpikeTime > 0)
            {
                spikeIntervals[spikeIntervalIndex] = interval;
                spikeIntervalIndex = (spikeIntervalIndex + 1) % spikeIntervals.Length;
                if (spikeIntervalCount < spikeIntervals.Length) spikeIntervalCount++;
            }

            lastSpikeTime = Time.unscaledTime;

            if (csvLogging)
            {
                csvBuffer.AppendLine($"{Time.frameCount},{dtMs:F2},{Time.unscaledTime:F3},{interval:F3},{memDelta},{gcOccurred},{currentMemory}");
                csvFlushCounter++;
                if (csvFlushCounter >= 10)
                {
                    FlushCsv();
                    csvFlushCounter = 0;
                }
            }
        }

        monitorUpdateTimer += dt;
        if (monitorUpdateTimer >= 0.5f)
        {
            monitorUpdateTimer = 0f;
            monoHeapSize = Profiler.GetMonoHeapSizeLong();
            monoUsedSize = Profiler.GetMonoUsedSizeLong();
            totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            totalReservedMemory = Profiler.GetTotalReservedMemoryLong();
        }

        if (Time.unscaledTime - lastSummaryTime >= SUMMARY_INTERVAL)
        {
            PrintSummary();
            lastSummaryTime = Time.unscaledTime;
        }

        // Refresh stat strings at 10Hz (eye-friendly, cheap).
        if (Time.unscaledTime - lastStatRefreshTime >= 0.1f)
        {
            lastStatRefreshTime = Time.unscaledTime;
            RefreshCachedStats();
        }
        // Bake graphs at 90Hz (smooth, but capped so we don't pay the
        // pixel-buffer cost on every render frame on a 240Hz monitor).
        // Only bake mode 1 (Overview) graphs — other modes don't show them.
        if (displayMode == 1 && Time.unscaledTime - lastBakeTime >= BAKE_INTERVAL)
        {
            lastBakeTime = Time.unscaledTime;
            if (bakedFrame != null) BakeFrameGraph();
            if (bakedFps   != null) BakeFpsGraph();
            if (bakedRtt   != null) BakeRttGraph();
            if (bakedTick  != null) BakeTickGraph();
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.f4Key.wasPressedThisFrame) displayMode = (displayMode + 1) % 4;
            if (kb.f5Key.wasPressedThisFrame)
            {
                csvLogging = !csvLogging;
                if (csvLogging)
                {
                    csvBuffer.Clear();
                    csvBuffer.AppendLine("Frame,FrameTimeMs,GameTime,IntervalSinceLast,MemoryDelta,GcOccurred,TotalMemory");
                    Plugin.Log($"[FrameProfiler] CSV logging ENABLED -> {csvPath}");
                }
                else
                {
                    FlushCsv();
                    Plugin.Log("[FrameProfiler] CSV logging DISABLED, file flushed.");
                }
            }
        }
    }

    void SampleFrameTimings()
    {
        const float NS_TO_MS = 1f / 1_000_000f;
        try
        {
            if (mainThreadRecorder.Valid)
                lastCpuMain = mainThreadRecorder.LastValue * NS_TO_MS;
            if (renderThreadRecorder.Valid)
                lastCpuRender = renderThreadRecorder.LastValue * NS_TO_MS;
            if (gfxWaitRecorder.Valid)
                lastGfxWait = gfxWaitRecorder.LastValue * NS_TO_MS;
            if (vsyncWaitRecorder.Valid)
                lastVsyncWait = vsyncWaitRecorder.LastValue * NS_TO_MS;
            if (lastCpuMain > 0f || lastCpuRender > 0f) frameTimingAvailable = true;
        }
        catch { }
    }

    void RefreshCachedStats()
    {
        int sortCount = Math.Min(totalFrames, FRAME_HISTORY);
        if (sortCount <= 0) return;

        // Copy current ring into preallocated sort buffer, sort for percentile.
        for (int i = 0; i < sortCount; i++)
            sortBuffer[i] = frameTimes[(frameIndex - sortCount + i + FRAME_HISTORY) % FRAME_HISTORY];
        Array.Sort(sortBuffer, 0, sortCount);
        cachedOnePercentLow = 1000f / sortBuffer[sortCount - 1 - sortCount / 100];

        // Rolling average over the FRAME_HISTORY window (~10s at 60fps).
        float sumMs = 0f;
        for (int i = 0; i < sortCount; i++) sumMs += sortBuffer[i];
        cachedAvgFrameMs = sortCount > 0 ? sumMs / sortCount : 0f;
        cachedAvgFps = cachedAvgFrameMs > 0f ? 1000f / cachedAvgFrameMs : 0f;

        // Auto-scale the frame-time axis: peak in the window (already sorted
        // ascending), snap to nice bands so the y-scale doesn't flicker.
        float peakMs = sortBuffer[sortCount - 1];
        // Count backlog-burst frames in window + spikes that overlap them.
        int backlogFrames = 0;
        int serverSpikes = 0;
        for (int i = 0; i < sortCount; i++)
        {
            int idx = (frameIndex - sortCount + i + FRAME_HISTORY) % FRAME_HISTORY;
            if (ticksPerFrame[idx] >= BACKLOG_BURST_THRESHOLD)
            {
                backlogFrames++;
                if (frameTimes[idx] > SPIKE_THRESHOLD_MS) serverSpikes++;
            }
        }
        cachedBacklogFrames = backlogFrames;
        cachedServerSuspectedSpikes = serverSpikes;

        frameTimeAxisMs =
            peakMs <= 12f  ? 12f  :
            peakMs <= 25f  ? 25f  :
            peakMs <= 50f  ? 50f  :
            peakMs <= 100f ? 100f :
            peakMs <= 250f ? 250f : 500f;

        float currentMs = frameTimes[(frameIndex - 1 + FRAME_HISTORY) % FRAME_HISTORY];
        float fps = currentMs > 0 ? 1000f / currentMs : 0;

        string serverEndpoint = "";
        string serverIp = "";
        try
        {
            var conn = GlobalStateManager.ConnectionState.Connection;
            if (conn != null && conn.EndPoint != null)
            {
                serverEndpoint = conn.EndPoint.ToString();
                // EndPoint.ToString() is typically "ip:port"; split.
                int colon = serverEndpoint.LastIndexOf(':');
                serverIp = colon > 0 ? serverEndpoint.Substring(0, colon) : serverEndpoint;
            }
        }
        catch { }
        // Point the ICMP prober at the live server IP (null clears it).
        FrameProfilerIcmp.SetTarget(serverIp);
        if (string.IsNullOrEmpty(serverEndpoint))
        {
            cachedServerLine = "Server: (offline)";
        }
        else
        {
            // Trigger geoip fetch on first sighting of this IP. Cached after.
            if (!FrameProfilerGeoIP.TryGet(serverIp, out var geo))
            {
                StartCoroutine(FrameProfilerGeoIP.FetchAsync(serverIp));
                cachedServerLine = $"Server: {serverEndpoint} (resolving…)";
            }
            else if (geo.LookupFailed)
            {
                cachedServerLine = $"Server: {serverEndpoint}";
            }
            else
            {
                string locale = !string.IsNullOrEmpty(geo.City)
                    ? $"{geo.City}, {geo.Country}"
                    : geo.Country ?? "";
                string isp = !string.IsNullOrEmpty(geo.Isp) ? $" — {geo.Isp}" : "";
                cachedServerLine = $"Server: {serverEndpoint} ({locale}{isp})";
            }
        }

        cachedLine1 = $"FPS: {fps:F0} ({cachedAvgFps:F0} 10s avg)  Frame: {currentMs:F1}ms ({cachedAvgFrameMs:F1} avg)  1%Low: {cachedOnePercentLow:F0}fps";
        cachedLine2 = $"Spikes(>{SPIKE_THRESHOLD_MS}ms): {spikeCount}  GC0: {currentGcCount}  Heap: {FormatBytes(monoHeapSize)}";

        float timeSinceGc = Time.unscaledTime - lastGcTime;
        string gcAge = lastGcTime > 0 ? $"{timeSinceGc:F1}s ago" : "none";
        cachedLine3 = $"Last GC: {gcAge}  Mono used: {FormatBytes(monoUsedSize)}";

        if (spikeIntervalCount >= 3)
        {
            float sum = 0f;
            int c = Math.Min(spikeIntervalCount, spikeIntervals.Length);
            for (int i = 0; i < c; i++) sum += spikeIntervals[i];
            float mean = sum / c;
            cachedLine4 = $"Spike pattern: ~{mean:F2}s avg interval ({c} samples)";
        }
        else
        {
            cachedLine4 = "";
        }

        // CPU/GPU bound analysis from built-in profiler counters.
        //   - Main thread / Render thread → CPU time on each thread
        //   - Gfx.WaitForPresentOnGfxThread → time render thread spent
        //     blocked waiting for GPU. High = GPU-bound.
        //   - WaitForTargetFPS → time main thread spent idle for vsync/cap.
        //     High = CPU has headroom, frame rate is capped not bottlenecked.
        if (frameTimingAvailable)
        {
            float frameMs = sumFrameTime > 0 && statsFrameCount > 0
                ? sumFrameTime / statsFrameCount : 16.7f;
            float cpu = Mathf.Max(lastCpuMain - lastVsyncWait, lastCpuRender - lastGfxWait);
            cpu = Mathf.Max(cpu, 0f);
            string verdict;
            if (lastVsyncWait > frameMs * 0.3f) verdict = "vsync/cap-limited";
            else if (lastGfxWait > frameMs * 0.3f) verdict = "GPU-bound";
            else if (lastCpuMain > lastCpuRender * 1.2f) verdict = "CPU-bound (main thread)";
            else if (lastCpuRender > lastCpuMain * 1.2f) verdict = "CPU-bound (render thread)";
            else verdict = "CPU-bound";
            cachedCpuGpuLine = $"Main {lastCpuMain:F1}ms  Render {lastCpuRender:F1}ms  GpuWait {lastGfxWait:F1}ms  VSync {lastVsyncWait:F1}ms";
            cachedCpuGpuVerdict = $"Verdict: {verdict}";
        }
        else
        {
            cachedCpuGpuLine = "CPU/GPU: profiler recorders not yet valid";
            cachedCpuGpuVerdict = "";
        }

        // Refresh network aggregates for the network mode and any other
        // consumers.
        FrameProfilerNetwork.RefreshAggregates();
    }

    static readonly Color C_BAR_GREEN  = new Color(0.2f, 0.9f, 0.2f, 0.9f);
    static readonly Color C_BAR_YELLOW = new Color(0.9f, 0.9f, 0.2f, 0.9f);
    static readonly Color C_BAR_RED    = new Color(0.9f, 0.2f, 0.2f, 0.9f);
    static readonly Color C_FPS_LINE   = new Color(0.4f, 0.8f, 1f, 1f);
    static readonly Color C_REF_LINE   = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    static readonly Color C_SPIKE_LINE = new Color(1f, 0f, 0f, 0.5f);
    static readonly Color C_LOSS       = new Color(1f, 0.3f, 0.3f, 0.95f);
    static readonly Color C_ICMP_LINE  = new Color(1f, 0.6f, 0.1f, 1f);     // orange = network-layer ICMP RTT
    static readonly Color C_DROP       = new Color(1f, 0.2f, 0.2f, 0.35f);
    static readonly Color C_BACKLOG    = new Color(0.7f, 0.4f, 1f, 0.95f);  // purple cap = server backlog burst

    void BakeFrameGraph()
    {
        var g = bakedFrame;
        g.Clear();
        float axis = frameTimeAxisMs;
        // Spike threshold reference line (only drawn when it's inside the
        // visible range — at very high refresh rates the axis caps at 12ms
        // and the line falls off the top).
        if (SPIKE_THRESHOLD_MS <= axis)
        {
            int threshY = Mathf.RoundToInt((SPIKE_THRESHOLD_MS / axis) * g.H);
            g.HLine(threshY, C_SPIKE_LINE);
        }
        int srcCount = Math.Min(FRAME_HISTORY, totalFrames);
        if (srcCount <= 0) { g.Apply(); return; }
        int stride = Math.Max(1, srcCount / g.W);
        int barCount = Math.Min(g.W, srcCount / stride);
        int barPx = Math.Max(1, g.W / barCount);
        for (int i = 0; i < barCount; i++)
        {
            float ms = 0f;
            int maxTicks = 0;
            for (int j = 0; j < stride; j++)
            {
                int idx = (frameIndex - srcCount + i * stride + j + FRAME_HISTORY) % FRAME_HISTORY;
                if (frameTimes[idx] > ms) ms = frameTimes[idx];
                if (ticksPerFrame[idx] > maxTicks) maxTicks = ticksPerFrame[idx];
            }
            int bh = Mathf.Clamp(Mathf.RoundToInt(ms / axis * g.H), 1, g.H - 1);
            var c = ms < 12f ? C_BAR_GREEN : ms < SPIKE_THRESHOLD_MS ? C_BAR_YELLOW : C_BAR_RED;
            g.Rect(i * barPx, 0, bh, barPx, c);
            // Purple cap on top of bars where >=3 server ticks landed in
            // that Unity frame — signals server backlog burst, regardless
            // of whether the frame was actually a stutter.
            if (maxTicks >= BACKLOG_BURST_THRESHOLD)
            {
                int capH = Math.Min(3, g.H - bh);
                if (capH > 0) g.Rect(i * barPx, bh, bh + capH - 1, barPx, C_BACKLOG);
            }
        }
        g.Apply();
    }

    void BakeFpsGraph()
    {
        var g = bakedFps;
        g.Clear();
        const float fpsMax = 1000f;
        g.HLine(Mathf.RoundToInt(60f  / fpsMax * g.H), C_REF_LINE);
        g.HLine(Mathf.RoundToInt(144f / fpsMax * g.H), C_REF_LINE);
        int srcCount = Math.Min(FRAME_HISTORY, totalFrames);
        if (srcCount <= 0) { g.Apply(); return; }
        int stride = Math.Max(1, srcCount / g.W);
        int barCount = Math.Min(g.W, srcCount / stride);
        int barPx = Math.Max(1, g.W / barCount);
        for (int i = 0; i < barCount; i++)
        {
            // Min-merge FPS over stride window so dips remain visible.
            float minFps = float.MaxValue;
            for (int j = 0; j < stride; j++)
            {
                int idx = (frameIndex - srcCount + i * stride + j + FRAME_HISTORY) % FRAME_HISTORY;
                float ms = frameTimes[idx];
                if (ms <= 0f) continue;
                float f = Mathf.Min(1000f / ms, fpsMax);
                if (f < minFps) minFps = f;
            }
            if (minFps == float.MaxValue) continue;
            int y = Mathf.RoundToInt(minFps / fpsMax * g.H);
            g.Dot(i * barPx, Mathf.Clamp(y, 0, g.H - 2), barPx, 2, C_FPS_LINE);
        }
        g.Apply();
    }

    void BakeRttGraph()
    {
        var g = bakedRtt;
        g.Clear();
        float rttMax = FrameProfilerNetwork.rttAxisMaxMs;
        const float LOSS_MAX = 100f;
        // Reference lines (depend on rttMax band)
        void RefAt(float val) { int y = Mathf.RoundToInt(val / rttMax * g.H); g.HLine(y, C_REF_LINE); }
        if (rttMax <= 25f)      { RefAt(10); RefAt(20); }
        else if (rttMax <= 50f) { RefAt(20); RefAt(40); }
        else if (rttMax <= 100f){ RefAt(25); RefAt(50); RefAt(75); }
        else if (rttMax <= 200f){ RefAt(50); RefAt(100); RefAt(150); }
        else                    { RefAt(rttMax*0.25f); RefAt(rttMax*0.5f); RefAt(rttMax*0.75f); }

        int rttCount = Math.Min(FrameProfilerNetwork.rttTotal, FrameProfilerNetwork.RTT_RING);
        if (rttCount <= 0) { g.Apply(); return; }
        int rttStart = FrameProfilerNetwork.rttTotal < FrameProfilerNetwork.RTT_RING
            ? 0 : FrameProfilerNetwork.rttIndex;
        int stride = Math.Max(1, rttCount / g.W);
        int barCount = Math.Min(g.W, rttCount / stride);
        int barPx = Math.Max(1, g.W / barCount);
        // Pass 1: loss as filled area from baseline up to %.
        for (int i = 0; i < barCount; i++)
        {
            float loss = 0f;
            for (int j = 0; j < stride; j++)
            {
                int idx = (rttStart + (rttCount - barCount * stride) + i * stride + j) % FrameProfilerNetwork.RTT_RING;
                if (FrameProfilerNetwork.lossPct[idx] > loss) loss = FrameProfilerNetwork.lossPct[idx];
            }
            if (loss <= 0f) continue;
            loss = Mathf.Min(loss, LOSS_MAX);
            int top = Mathf.RoundToInt(loss / LOSS_MAX * g.H);
            g.Rect(i * barPx, 0, top, barPx, C_LOSS);
        }
        // Pass 2: cyan RTT line on top.
        for (int i = 0; i < barCount; i++)
        {
            float v = 0f;
            for (int j = 0; j < stride; j++)
            {
                int idx = (rttStart + (rttCount - barCount * stride) + i * stride + j) % FrameProfilerNetwork.RTT_RING;
                if (FrameProfilerNetwork.rttMs[idx] > v) v = FrameProfilerNetwork.rttMs[idx];
            }
            v = Mathf.Min(v, rttMax);
            int y = Mathf.RoundToInt(v / rttMax * g.H);
            g.Dot(i * barPx, Mathf.Clamp(y, 0, g.H - 2), barPx, 2, C_FPS_LINE);
        }
        // Pass 3: orange ICMP line (network-layer RTT) — held between probes,
        // so it reads as a step line under the cyan game RTT. The vertical gap
        // between the two is the server/queueing overhead. Negative samples
        // (ICMP blocked / no reply) are skipped, leaving a gap in the line.
        for (int i = 0; i < barCount; i++)
        {
            float v = -1f;
            for (int j = 0; j < stride; j++)
            {
                int idx = (rttStart + (rttCount - barCount * stride) + i * stride + j) % FrameProfilerNetwork.RTT_RING;
                float s = FrameProfilerNetwork.icmpMs[idx];
                if (s >= 0f && s > v) v = s;
            }
            if (v < 0f) continue;
            v = Mathf.Min(v, rttMax);
            int y = Mathf.RoundToInt(v / rttMax * g.H);
            g.Dot(i * barPx, Mathf.Clamp(y, 0, g.H - 2), barPx, 2, C_ICMP_LINE);
        }
        g.Apply();
    }

    void BakeTickGraph()
    {
        var g = bakedTick;
        g.Clear();
        const float TICK_MAX = 150f;
        g.HLine(Mathf.RoundToInt(50f  / TICK_MAX * g.H), C_REF_LINE);
        g.HLine(Mathf.RoundToInt(100f / TICK_MAX * g.H), C_REF_LINE);
        int tickCount = Math.Min(FrameProfilerNetwork.totalSamples, FrameProfilerNetwork.RING_SIZE);
        if (tickCount <= 1) { g.Apply(); return; }
        int tickStart = FrameProfilerNetwork.totalSamples < FrameProfilerNetwork.RING_SIZE
            ? 0 : FrameProfilerNetwork.ringIndex;
        int stride = Math.Max(1, tickCount / g.W);
        int barCount = Math.Max(1, Math.Min(g.W, (tickCount - 1) / stride));
        int barPx = Math.Max(1, g.W / barCount);
        for (int i = 0; i < barCount; i++)
        {
            float minHz = float.MaxValue;
            bool hadDrop = false;
            for (int j = 0; j < stride; j++)
            {
                int kB = 1 + i * stride + j;
                if (kB >= tickCount) break;
                int idxA = (tickStart + kB - 1) % FrameProfilerNetwork.RING_SIZE;
                int idxB = (tickStart + kB) % FrameProfilerNetwork.RING_SIZE;
                float dt = FrameProfilerNetwork.arrivalTimes[idxB] - FrameProfilerNetwork.arrivalTimes[idxA];
                if (dt > 0f)
                {
                    float hz = Mathf.Min(1f / dt, TICK_MAX);
                    if (hz < minHz) minHz = hz;
                }
                if (FrameProfilerNetwork.sampleGap[idxB] > 0) hadDrop = true;
            }
            if (hadDrop) g.Rect(i * barPx, 0, g.H - 1, barPx, C_DROP);
            if (minHz != float.MaxValue)
            {
                int y = Mathf.RoundToInt(minHz / TICK_MAX * g.H);
                g.Dot(i * barPx, Mathf.Clamp(y, 0, g.H - 2), barPx, 2, C_BAR_GREEN);
            }
        }
        g.Apply();
    }

    void PrintSummary()
    {
        if (statsFrameCount == 0) return;

        float avg = sumFrameTime / statsFrameCount;
        Plugin.Log($"[FrameProfiler][PERF] frames={statsFrameCount} avg={avg:F1}ms min={minFrameTime:F1}ms max={maxFrameTime:F1}ms " +
                   $"spikes(>{SPIKE_THRESHOLD_MS}ms)={spikeCount} gc0={currentGcCount} " +
                   $"monoHeap={FormatBytes(monoHeapSize)} monoUsed={FormatBytes(monoUsedSize)}");

        if (spikeIntervalCount >= 3) AnalyzeSpikePattern();

        minFrameTime = float.MaxValue;
        maxFrameTime = 0f;
        sumFrameTime = 0f;
        statsFrameCount = 0;
        spikeCount = 0;
    }

    void AnalyzeSpikePattern()
    {
        float sum = 0f;
        int count = Math.Min(spikeIntervalCount, spikeIntervals.Length);
        for (int i = 0; i < count; i++) sum += spikeIntervals[i];
        float mean = sum / count;

        float variance = 0f;
        for (int i = 0; i < count; i++)
        {
            float diff = spikeIntervals[i] - mean;
            variance += diff * diff;
        }
        float stddev = (float)Math.Sqrt(variance / count);

        float regularity = mean > 0 ? (1f - stddev / mean) : 0f;
        string pattern = regularity > 0.7f ? "REGULAR (timer/tick suspected)" :
                         regularity > 0.4f ? "SEMI-REGULAR" : "IRREGULAR (likely GC or load)";

        Plugin.Log($"[FrameProfiler][PATTERN] spike interval: mean={mean:F2}s stddev={stddev:F2}s regularity={regularity:P0} -> {pattern}");

        if (regularity > 0.5f)
        {
            if (mean > 0.8f && mean < 1.3f)
                Plugin.Log("[FrameProfiler][PATTERN] ~1s interval matches: GameManager.Server_Tick, PlayerController ping, EdgegapManager poll");
            else if (mean > 1.8f && mean < 2.3f)
                Plugin.Log("[FrameProfiler][PATTERN] ~2s interval: possible doubled tick or GC gen1");
            else if (mean > 4.5f && mean < 5.5f)
                Plugin.Log("[FrameProfiler][PATTERN] ~5s interval matches: ChatManager timeout, UIAnnouncements hide, or GC gen2");
            else
                Plugin.Log($"[FrameProfiler][PATTERN] ~{mean:F1}s interval: no known game timer match - investigate custom systems");
        }
    }

    void OnGUI()
    {
        // OnGUI fires twice per frame (Layout + Repaint). DrawTexture is a
        // no-op outside Repaint, but the per-bar loop still burns CPU — so
        // bail entirely for non-Repaint events.
        if (Event.current.type != EventType.Repaint) return;
        InitStyles();

        // Four modes, simple → advanced → debug:
        //   0 Minimal    — just FPS + frame time text (cheapest)
        //   1 Overview   — all live graphs + stats on one panel
        //   2 Diagnostics — spike log w/ attribution + top calls table
        //   3 Debug      — memory/GC/system monitor
        switch (displayMode)
        {
            case 0: DrawMinimal(); break;
            case 1: DrawOverview(); break;
            case 2: DrawDiagnostics(); break;
            case 3: DrawSystemMonitors(); break;
        }
    }

    void DrawMinimal()
    {
        float panelW = 240f;
        float panelH = 90f;
        float x = Screen.width - panelW - 10f;
        float y = 10f;
        GUI.Box(new Rect(x, y, panelW, panelH), "", boxStyle);
        float currentMs = frameTimes[(frameIndex - 1 + FRAME_HISTORY) % FRAME_HISTORY];
        float fps = currentMs > 0f ? 1000f / currentMs : 0f;
        // headerStyle is fontSize 15 — needs ~22px of vertical space.
        GUI.Label(new Rect(x + 8, y + 3,  panelW, 22), $"{fps:F0} fps  ({cachedAvgFps:F0} 10s avg)", headerStyle);
        GUI.Label(new Rect(x + 8, y + 27, panelW, 18), $"{currentMs:F1} ms  ({cachedAvgFrameMs:F1} avg)", labelStyle);
        GUI.Label(new Rect(x + 8, y + 47, panelW, 18), $"1% low: {cachedOnePercentLow:F0} fps", labelStyle);
        GUI.Label(new Rect(x + 8, y + 69, panelW, 16), "F4 = cycle mode    F5 = toggle CSV log", graphTitleStyle);
    }

    void DrawOverview()
    {
        float panelW = 540f;
        float panelH = 1220f;
        float x = Screen.width - panelW - 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, panelW, panelH), "", boxStyle);

        float graphX = x + 10f;
        // Reserve ~70px on the right for axis labels.
        float graphW = panelW - 80f;
        float graphY = y + 45f;   // header + extra room for the first graph title (18px)
        float graphH = 180f;

        string csvState = csvLogging ? "CSV:REC" : "CSV:off";
        GUI.Label(new Rect(x + 10, y + 3, panelW, 22), $"Frame Profiler [F4 mode | F5 {csvState}]", headerStyle);

        GUI.Label(new Rect(graphX, graphY - 20, graphW, 18),
            "Frame time ms — red>20 yellow>12 green ok | purple cap = server backlog burst",
            graphTitleStyle);
        // Single blit of the baked texture replaces ~150 per-bar IMGUI calls.
        if (bakedFrame != null) GUI.DrawTexture(new Rect(graphX, graphY, graphW, graphH), bakedFrame.Tex);
        GUI.Label(new Rect(graphX + graphW + 2, graphY - 2, 60, 18), $"{frameTimeAxisMs:F0}ms", labelStyle);
        if (SPIKE_THRESHOLD_MS <= frameTimeAxisMs)
        {
            float thresholdY = graphY + graphH - (SPIKE_THRESHOLD_MS / frameTimeAxisMs) * graphH;
            GUI.Label(new Rect(graphX + graphW + 2, thresholdY - 8, 60, 18), $"{SPIKE_THRESHOLD_MS}ms", labelStyle);
        }
        GUI.Label(new Rect(graphX + graphW + 2, graphY + graphH - 14, 60, 18), "0", labelStyle);

        // FPS line graph below the frame-time bars (1000fps fixed scale).
        float fpsY = graphY + graphH + 32f;  // 18 title + ~14 breathing room
        float fpsH = 190f;
        const float fpsMax = 1000f;
        GUI.Label(new Rect(graphX, fpsY - 20, graphW, 18),
            "FPS — reference lines at 60 and 144",
            graphTitleStyle);
        if (bakedFps != null) GUI.DrawTexture(new Rect(graphX, fpsY, graphW, fpsH), bakedFps.Tex);

        float ref60Y = fpsY + fpsH - (60f / fpsMax) * fpsH;
        float ref144Y = fpsY + fpsH - (144f / fpsMax) * fpsH;
        GUI.Label(new Rect(graphX + graphW + 2, fpsY - 2, 50, 18), "1000", labelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, ref144Y - 8, 50, 18), "144", labelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, ref60Y - 8, 50, 18), "60", labelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, fpsY + fpsH - 14, 50, 18), "0", labelStyle);

        // RTT line graph (cyan) with packet-loss % overlay (red, secondary
        // Y axis on the right). Loss axis caps at 25% so even small drops
        // are visible.
        float rttY = fpsY + fpsH + 32f;
        float rttH = 120f;
        float rttMax = FrameProfilerNetwork.rttAxisMaxMs;
        const float LOSS_MAX = 100f;
        GUI.Label(new Rect(graphX, rttY - 20, graphW, 18),
            "RTT ms — game (cyan) vs ICMP (orange) + Packet loss % (red, 0-100%)",
            graphTitleStyle);
        if (bakedRtt != null) GUI.DrawTexture(new Rect(graphX, rttY, graphW, rttH), bakedRtt.Tex);
        GUI.Label(new Rect(graphX + graphW + 2, rttY - 2,         60, 18), $"{rttMax:F0}ms",  rttMaxLabelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, rttY + 14,        60, 18), $"{LOSS_MAX:F0}%", lossMaxLabelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, rttY + rttH - 14, 60, 18), "0", labelStyle);

        // Server tick arrival rate: instantaneous Hz from inter-arrival gaps.
        // Red vertical impulse drawn at each sample whose tickId showed a
        // gap from the previous one (= server dropped or coalesced ticks).
        float tickY = rttY + rttH + 32f;
        float tickH = 100f;
        const float TICK_MAX = 150f;
        GUI.Label(new Rect(graphX, tickY - 20, graphW, 18),
            "Server tick rate Hz (green) — red bars = dropped ticks (100Hz target)",
            graphTitleStyle);
        if (bakedTick != null) GUI.DrawTexture(new Rect(graphX, tickY, graphW, tickH), bakedTick.Tex);
        GUI.Label(new Rect(graphX + graphW + 2, tickY - 2, 50, 18), $"Hz {TICK_MAX:F0}", labelStyle);
        GUI.Label(new Rect(graphX + graphW + 2, tickY + tickH - 14, 50, 18), "0", labelStyle);

        // Stats stack
        float statsY = tickY + tickH + 6f;
        float ly = statsY;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedServerLine, labelStyle); ly += 18;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedLine1, labelStyle); ly += 18;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedLine2, labelStyle); ly += 18;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedLine3, labelStyle); ly += 18;
        if (!string.IsNullOrEmpty(cachedLine4))
        {
            GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedLine4, labelStyle); ly += 18;
        }
        GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedCpuGpuLine, labelStyle); ly += 18;
        if (!string.IsNullOrEmpty(cachedCpuGpuVerdict))
        {
            GUI.Label(new Rect(x + 10, ly, panelW, 18), cachedCpuGpuVerdict, labelStyle); ly += 18;
        }

        // Frame composition breakdown — horizontal bars normalized to the
        // longest segment so you can see the proportions at a glance.
        ly += 4;
        GUI.Label(new Rect(x + 10, ly, panelW, 16),
            "Frame composition (last frame) — bars scaled to the slowest segment:",
            graphTitleStyle);
        ly += 18;
        float labelColW = 80f;
        float valueColW = 110f;
        float barColX = x + 10 + labelColW;
        float barColW = panelW - 20 - labelColW - valueColW;
        float maxSeg = Mathf.Max(0.001f,
            Mathf.Max(Mathf.Max(lastCpuMain, lastCpuRender),
                      Mathf.Max(lastGfxWait, lastVsyncWait)));
        DrawCompBar(x + 10, ly, labelColW, barColX, barColW, valueColW, "CPU main",   lastCpuMain,   maxSeg, C_BAR_GREEN); ly += 17;
        DrawCompBar(x + 10, ly, labelColW, barColX, barColW, valueColW, "CPU render", lastCpuRender, maxSeg, C_FPS_LINE);  ly += 17;
        DrawCompBar(x + 10, ly, labelColW, barColX, barColW, valueColW, "GPU wait",   lastGfxWait,   maxSeg, C_BAR_YELLOW); ly += 17;
        DrawCompBar(x + 10, ly, labelColW, barColX, barColW, valueColW, "VSync wait", lastVsyncWait, maxSeg, C_REF_LINE);   ly += 17;

        // Per-function breakdown of THIS frame — top 6 built-in profiler
        // markers by last-frame cost. Bars normalized to current frame
        // time (cachedAvgFrameMs as a reasonable visual reference).
        ly += 4;
        GUI.Label(new Rect(x + 10, ly, panelW, 16),
            "Top functions this frame (built-in profiler markers, sorted by last-frame ms):",
            graphTitleStyle);
        ly += 18;
        int markerCount = FrameProfilerBuiltinMarkers.GetCount();
        // Show ANY marker with a nonzero LastValue. Many real ones are
        // sub-millisecond on a fast frame and the previous 0.01ms cutoff
        // was hiding them.
        var topFns = new List<(string name, float ms)>();
        for (int i = 0; i < markerCount; i++)
        {
            float ms = FrameProfilerBuiltinMarkers.GetLastMs(i);
            if (ms > 0f) topFns.Add((FrameProfilerBuiltinMarkers.GetName(i), ms));
        }
        topFns.Sort((a, b) => b.ms.CompareTo(a.ms));
        float fnMax = topFns.Count > 0 ? topFns[0].ms : 1f;
        int rowsToShow = Math.Min(6, topFns.Count);
        for (int i = 0; i < rowsToShow; i++)
        {
            DrawCompBar(x + 10, ly, 210f, x + 10 + 210f, panelW - 20 - 210f - 90f, 90f,
                topFns[i].name, topFns[i].ms, fnMax, C_FPS_LINE);
            ly += 17;
        }
        if (rowsToShow == 0)
        {
            GUI.Label(new Rect(x + 10, ly, panelW, 16),
                "(profiler markers not yet emitting data — wait a frame)",
                labelStyle);
            ly += 17;
        }

        // Network summary lines
        float icmp = FrameProfilerIcmp.currentIcmpMs;
        // Δ = game RTT − ICMP ≈ server/processing + send-buffer overhead.
        // Only meaningful when we actually got an ICMP reply; otherwise show
        // why (blocked, timeout, …) since many hosts drop echo requests.
        string icmpStr = icmp >= 0f
            ? $"ICMP {icmp:F0}ms  Δ {(FrameProfilerNetwork.currentRttMs - icmp):+0;-0;0}ms"
            : $"ICMP {FrameProfilerIcmp.StatusText()}";
        GUI.Label(new Rect(x + 10, ly, panelW, 18),
            $"RTT {FrameProfilerNetwork.currentRttMs:F0}ms  {icmpStr}  Ticks {FrameProfilerNetwork.currentTickRateHz:F1}Hz/100  Jitter {FrameProfilerNetwork.jitterMs:F1}ms",
            labelStyle); ly += 18;
        GUI.Label(new Rect(x + 10, ly, panelW, 18),
            $"Loss {FrameProfilerNetwork.currentLossPct:F1}%  Net {FormatBytes((long)FrameProfilerNetwork.currentBytesPerSec)}/s  Dropped +{FrameProfilerNetwork.droppedInWindow}/s (total {FrameProfilerNetwork.droppedTickCount})",
            labelStyle); ly += 18;
        GUI.Label(new Rect(x + 10, ly, panelW, 18),
            $"Server backlog frames: {cachedBacklogFrames} (of which {cachedServerSuspectedSpikes} were stutters >{SPIKE_THRESHOLD_MS}ms)",
            labelStyle);
    }

    void DrawRttRefLines(float gx, float gy, float gw, float gh, float rttMax)
    {
        if (rttMax <= 25f)
        {
            DrawRefLine(gx, gy, gw, gh, 10f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 20f, rttMax);
        }
        else if (rttMax <= 50f)
        {
            DrawRefLine(gx, gy, gw, gh, 20f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 40f, rttMax);
        }
        else if (rttMax <= 100f)
        {
            DrawRefLine(gx, gy, gw, gh, 25f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 50f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 75f, rttMax);
        }
        else if (rttMax <= 200f)
        {
            DrawRefLine(gx, gy, gw, gh, 50f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 100f, rttMax);
            DrawRefLine(gx, gy, gw, gh, 150f, rttMax);
        }
        else
        {
            DrawRefLine(gx, gy, gw, gh, rttMax * 0.25f, rttMax);
            DrawRefLine(gx, gy, gw, gh, rttMax * 0.5f, rttMax);
            DrawRefLine(gx, gy, gw, gh, rttMax * 0.75f, rttMax);
        }
    }

    // Mode 1: spike log (with per-frame call attribution) on top, top-calls
    // table below. Single panel so you see both views at once.
    void DrawDiagnostics()
    {
        float panelW = 800f;
        float panelH = 620f;
        float x = Screen.width - panelW - 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, panelW, panelH), "", boxStyle);
        GUI.Label(new Rect(x + 10, y + 3, panelW, 22),
            $"Diagnostics — Spike Log + Top Calls [F4 mode | F5 {(csvLogging ? "CSV:REC" : "CSV:off")}]", headerStyle);

        // Spike Log
        float ly = y + 30f;
        GUI.Label(new Rect(x + 10, ly, panelW, 14),
            "Frame stutters (>20ms) — \"Attributed call\" = most expensive instrumented function in that frame",
            graphTitleStyle); ly += 16f;
        GUI.Label(new Rect(x + 10, ly, panelW, 18),
            "Frame      Time     Since   Interval  MemDelta   GC  Tks  Cause", labelStyle);
        ly += 18f;

        int count = Math.Min(spikeLog.Count, 14);
        int start = Math.Max(0, spikeLog.Count - count);
        for (int i = start; i < spikeLog.Count; i++)
        {
            var s = spikeLog[i];
            string gc = s.GcOccurred ? "YES" : "   ";
            string interval = s.IntervalSinceLast > 0 ? $"{s.IntervalSinceLast,7:F2}s" : "     --";
            // "Cause" column merges attributed-call attribution with
            // server-backlog detection. SERVER tag means N>=3 server ticks
            // landed in this Unity frame → likely server backlog flush.
            string cause;
            if (s.ServerSuspected)
                cause = string.IsNullOrEmpty(s.AttributedCall)
                    ? "*SERVER backlog burst*"
                    : $"*SERVER backlog* + {s.AttributedCall} ({s.AttributedMs:F1}ms)";
            else if (string.IsNullOrEmpty(s.AttributedCall))
                cause = "(none instrumented — local cause unattributed)";
            else
                cause = $"{s.AttributedCall} ({s.AttributedMs:F1}ms)";
            GUI.Label(new Rect(x + 10, ly, panelW, 18),
                $"{s.FrameNumber,7}  {s.TimeMs,7:F1}ms  {s.TimeSinceStart,6:F1}s  {interval}  {FormatBytesSigned(s.MemoryDelta),9}  {gc}  {s.TicksThisFrame,3}  {cause}",
                labelStyle);
            ly += 17f;
        }

        // Separator
        float sepY = y + 30f + 16f + 18f + 14 * 17f + 4f;
        var sepTex = fpsRefLine;
        GUI.DrawTexture(new Rect(x + 10, sepY, panelW - 20, 1), sepTex);

        // Top Calls — combined view: hand-instrumented Harmony patches
        // (allocation-aware) + Unity built-in profiler markers (broad
        // engine-subsystem coverage). Sorted by total ms in the window.
        float topY = sepY + 8f;
        GUI.Label(new Rect(x + 10, topY, panelW, 18),
            "Top calls — Harmony patches (with alloc tracking) + Unity built-in markers, sorted by total ms",
            graphTitleStyle); topY += 18f;
        GUI.Label(new Rect(x + 10, topY, panelW, 18),
            "Source  System                                  Calls    Avg ms    Max ms   Total ms    Alloc", labelStyle);
        topY += 18f;

        // Build unified rows from three sources:
        //   patch  — our 9 hand-instrumented Harmony patches (with alloc)
        //   marker — Unity built-in profiler markers
        //   mod    — per-mod aggregate from FrameProfilerMods (when enabled)
        int nPatches = FrameProfilerPatches.GetSystemCount();
        int nMarkers = FrameProfilerBuiltinMarkers.GetCount();
        var modList = new List<FrameProfilerMods.ModStats>(FrameProfilerMods.Snapshot());
        int nMods = modList.Count;
        var combined = new (string src, string name, int calls, float total, float max, long bytes)[nPatches + nMarkers + nMods];
        for (int i = 0; i < nPatches; i++)
        {
            var r = FrameProfilerPatches.GetSystemSnapshot(i);
            combined[i] = ("patch ", r.Name, r.Calls, r.TotalMs, r.MaxMs, r.TotalBytes);
        }
        for (int i = 0; i < nMarkers; i++)
        {
            var r = FrameProfilerBuiltinMarkers.GetSnapshot(i);
            combined[nPatches + i] = ("marker", r.Name, r.Calls, r.TotalMs, r.MaxMs, 0L);
        }
        for (int i = 0; i < nMods; i++)
        {
            var m = modList[i];
            combined[nPatches + nMarkers + i] = ("mod   ", m.ModName, m.Calls, m.TotalMs, m.MaxMs, m.TotalAllocBytes);
        }
        Array.Sort(combined, (a, b) => b.total.CompareTo(a.total));

        int rowsShown = 0;
        for (int i = 0; i < combined.Length && rowsShown < 18; i++)
        {
            var r = combined[i];
            if (r.calls == 0 && r.total <= 0f) continue; // skip totally inactive
            float avg = r.calls > 0 ? r.total / r.calls : 0f;
            string alloc = r.bytes > 0 ? FormatBytes(r.bytes) : "      —";
            GUI.Label(new Rect(x + 10, topY, panelW, 18),
                $"{r.src}  {r.name,-38}  {r.calls,6}  {avg,7:F2}  {r.max,7:F2}  {r.total,8:F1}   {alloc,10}",
                labelStyle);
            topY += 17f;
            rowsShown++;
        }
    }

    void DrawSystemMonitors()
    {
        float panelW = 420f;
        float panelH = 220f;
        float x = Screen.width - panelW - 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, panelW, panelH), "", boxStyle);
        GUI.Label(new Rect(x + 10, y + 3, panelW, 22), $"System Monitor [F4 mode | F5 {(csvLogging ? "CSV:REC" : "CSV:off")}]", headerStyle);

        float ly = y + 28f;
        int lineH = 19;

        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Mono Heap:          {FormatBytes(monoHeapSize)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Mono Used:          {FormatBytes(monoUsedSize)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Unity Allocated:    {FormatBytes(totalAllocatedMemory)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Unity Reserved:     {FormatBytes(totalReservedMemory)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"GC Gen0 Count:      {GC.CollectionCount(0)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"GC Gen1 Count:      {GC.CollectionCount(1)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"GC Gen2 Count:      {GC.CollectionCount(2)}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Total Frame Count:  {Time.frameCount}", labelStyle); ly += lineH;
        GUI.Label(new Rect(x + 10, ly, panelW, 18), $"Time.timeScale:     {Time.timeScale:F2}", labelStyle); ly += lineH;
    }

    void DrawRefLine(float gx, float gy, float gw, float gh, float val, float max)
    {
        float ry = gy + gh - (val / max) * gh;
        GUI.DrawTexture(new Rect(gx, ry, gw, 1), fpsRefLine);
        GUI.Label(new Rect(gx + gw + 2, ry - 8, 50, 18), $"{val:F0}", labelStyle);
    }

    // One row of the frame composition breakdown: text label, horizontal
    // bar normalized to the largest segment, ms + % value on the right.
    void DrawCompBar(float lx, float y, float labelW, float barX, float barW, float valueW,
                     string label, float ms, float maxMs, Color color)
    {
        GUI.Label(new Rect(lx, y, labelW, 16), label, labelStyle);
        float frac = maxMs > 0f ? Mathf.Clamp01(ms / maxMs) : 0f;
        // bg trough
        GUI.DrawTexture(new Rect(barX, y + 3, barW, 10), graphBg);
        // filled portion
        if (frac > 0.001f)
        {
            var tex = ColorTex(color);
            GUI.DrawTexture(new Rect(barX, y + 3, barW * frac, 10), tex);
        }
        float pct = maxMs > 0f ? ms / maxMs * 100f : 0f;
        GUI.Label(new Rect(barX + barW + 4, y, valueW, 16), $"{ms,5:F2} ms  {pct,3:F0}%", labelStyle);
    }

    // Cache of 1x1 colored textures for use in DrawCompBar / other bars.
    readonly Dictionary<Color, Texture2D> colorTexCache = new Dictionary<Color, Texture2D>();
    Texture2D ColorTex(Color c)
    {
        if (colorTexCache.TryGetValue(c, out var t) && t != null) return t;
        t = MakeTex(1, 1, c);
        colorTexCache[c] = t;
        return t;
    }

    void FlushCsv()
    {
        if (csvBuffer.Length == 0) return;
        try
        {
            System.IO.File.AppendAllText(csvPath, csvBuffer.ToString());
            csvBuffer.Clear();
        }
        catch (Exception e)
        {
            Plugin.LogError($"[FrameProfiler] CSV write failed: {e.Message}");
            csvLogging = false;
        }
    }

    public void OnDestroy()
    {
        if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
        if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
        if (gfxWaitRecorder.Valid) gfxWaitRecorder.Dispose();
        if (vsyncWaitRecorder.Valid) vsyncWaitRecorder.Dispose();
        FrameProfilerBuiltinMarkers.Stop();
        bakedFrame?.Dispose();
        bakedFps?.Dispose();
        bakedRtt?.Dispose();
        bakedTick?.Dispose();
        if (csvLogging) FlushCsv();
        if (graphBg != null) Destroy(graphBg);
        if (spikeLine != null) Destroy(spikeLine);
        if (barGreen != null) Destroy(barGreen);
        if (barYellow != null) Destroy(barYellow);
        if (barRed != null) Destroy(barRed);
        if (fpsLine != null) Destroy(fpsLine);
        if (fpsRefLine != null) Destroy(fpsRefLine);
        if (lossLine != null) Destroy(lossLine);
        if (dropMarker != null) Destroy(dropMarker);
    }

    static Texture2D MakeTex(int w, int h, Color col)
    {
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / 1048576f:F1} MB";
    }

    static string FormatBytesSigned(long bytes)
    {
        string sign = bytes >= 0 ? "+" : "";
        if (Math.Abs(bytes) < 1024) return $"{sign}{bytes} B";
        if (Math.Abs(bytes) < 1048576) return $"{sign}{bytes / 1024f:F0} KB";
        return $"{sign}{bytes / 1048576f:F1} MB";
    }
}
