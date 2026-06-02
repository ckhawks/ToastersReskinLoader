using System;
using Unity.Profiling;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol;

// Subscribes to a curated set of Unity's built-in profiler markers via
// ProfilerRecorder so the Top Calls view can finger-point microstutters
// at engine subsystems (Physics, Animator, Canvas batch, GC, …) — not
// just the 9 hand-Harmony-patched targets.
//
// Each marker is sampled once per frame from LastValue (nanoseconds, the
// last-frame cost for that marker) and rolled into per-marker stats that
// mirror FrameProfilerPatches.SystemStats. Invalid markers (those that
// don't exist in this Unity build) are silently skipped.
public static class FrameProfilerBuiltinMarkers
{
    // Definition: friendly name + Unity profiler category + raw marker name.
    struct MarkerDef
    {
        public string DisplayName;
        public ProfilerCategory Category;
        public string Stat;
    }

    // Unity's marker names vary by version. We try several aliases per
    // concept and pick whichever Valid one emits >0 ns in any frame.
    static readonly MarkerDef[] DEFS = new MarkerDef[]
    {
        // Known-good thread-level markers (these also drive the CPU/GPU
        // breakdown bars). Listing them here so the per-function view
        // isn't empty even if everything else fails to resolve.
        new MarkerDef { DisplayName = "Main Thread",                      Category = ProfilerCategory.Internal,  Stat = "Main Thread" },
        new MarkerDef { DisplayName = "Render Thread",                    Category = ProfilerCategory.Internal,  Stat = "Render Thread" },
        new MarkerDef { DisplayName = "Gfx: WaitForPresentOnGfxThread",   Category = ProfilerCategory.Render,    Stat = "Gfx.WaitForPresentOnGfxThread" },
        new MarkerDef { DisplayName = "WaitForTargetFPS (vsync wait)",    Category = ProfilerCategory.Internal,  Stat = "WaitForTargetFPS" },

        // Scripts — every MonoBehaviour Update across the scene.
        new MarkerDef { DisplayName = "Scripts: Behaviour.Update",        Category = ProfilerCategory.Scripts,   Stat = "BehaviourUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.Update (alt)",  Category = ProfilerCategory.Internal,  Stat = "Update.ScriptRunBehaviourUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.Update (alt2)", Category = ProfilerCategory.Internal,  Stat = "ScriptRunBehaviourUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.LateUpdate",    Category = ProfilerCategory.Scripts,   Stat = "LateBehaviourUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.LateUpdate(2)", Category = ProfilerCategory.Internal,  Stat = "PreLateUpdate.ScriptRunBehaviourLateUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.FixedUpdate",   Category = ProfilerCategory.Scripts,   Stat = "FixedBehaviourUpdate" },
        new MarkerDef { DisplayName = "Scripts: Behaviour.FixedUpdate(2)",Category = ProfilerCategory.Internal,  Stat = "FixedUpdate.ScriptRunBehaviourFixedUpdate" },

        // Physics
        new MarkerDef { DisplayName = "Physics.Simulate",                 Category = ProfilerCategory.Physics,   Stat = "Physics.Simulate" },
        new MarkerDef { DisplayName = "Physics.FixedUpdate",              Category = ProfilerCategory.Internal,  Stat = "FixedUpdate.PhysicsFixedUpdate" },
        new MarkerDef { DisplayName = "Physics.Processing",               Category = ProfilerCategory.Physics,   Stat = "Physics.Processing" },

        // Animation
        new MarkerDef { DisplayName = "Animator.Update",                  Category = ProfilerCategory.Animation, Stat = "Animator.Update" },
        new MarkerDef { DisplayName = "Animators.Update",                 Category = ProfilerCategory.Animation, Stat = "Animators.Update" },
        new MarkerDef { DisplayName = "DirectorUpdateAnimationBegin",     Category = ProfilerCategory.Internal,  Stat = "PreLateUpdate.DirectorUpdateAnimationBegin" },

        // Render
        new MarkerDef { DisplayName = "Camera.Render",                    Category = ProfilerCategory.Render,    Stat = "Camera.Render" },
        new MarkerDef { DisplayName = "RenderLoop.Draw",                  Category = ProfilerCategory.Render,    Stat = "RenderLoop.Draw" },
        new MarkerDef { DisplayName = "Render Camera",                    Category = ProfilerCategory.Render,    Stat = "Render Camera" },
        new MarkerDef { DisplayName = "Canvas.SendWillRenderCanvases",    Category = ProfilerCategory.Render,    Stat = "Canvas.SendWillRenderCanvases" },
        new MarkerDef { DisplayName = "UGUI.Rendering.EmitWorldScreenspaceCameraGeometry", Category = ProfilerCategory.Render, Stat = "UGUI.Rendering.EmitWorldScreenspaceCameraGeometry" },
        new MarkerDef { DisplayName = "Canvas.BuildBatch",                Category = ProfilerCategory.Render,    Stat = "Canvas.BuildBatch" },
        new MarkerDef { DisplayName = "Shadows.RenderShadowMap",          Category = ProfilerCategory.Render,    Stat = "Shadows.RenderShadowMap" },

        // Memory / GC
        new MarkerDef { DisplayName = "GC.Collect",                       Category = ProfilerCategory.Memory,    Stat = "GC.Collect" },
        new MarkerDef { DisplayName = "GC.MarkDependencies",              Category = ProfilerCategory.Memory,    Stat = "GC.MarkDependencies" },

        // Audio / particles / network
        new MarkerDef { DisplayName = "AudioSystem.Update",               Category = ProfilerCategory.Audio,     Stat = "AudioSystem.Update" },
        new MarkerDef { DisplayName = "ParticleSystem.Update",            Category = ProfilerCategory.Render,    Stat = "ParticleSystem.Update" },
        new MarkerDef { DisplayName = "NetworkUpdate",                    Category = ProfilerCategory.Internal,  Stat = "NetworkUpdate" },
    };

    static ProfilerRecorder[] recorders;
    static SystemStats[] stats;
    static int diagnosticFrames = 0;
    const int DIAGNOSTIC_DUMP_FRAME = 180; // ~3s at 60fps

    public struct SystemStats
    {
        public string Name;
        public int Calls;
        public float TotalMs;
        public float MaxMs;
        public void Record(float ms)
        {
            Calls++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
        }
        public void Reset()
        {
            Calls = 0; TotalMs = 0f; MaxMs = 0f;
        }
    }

    public static void Start()
    {
        Stop(); // idempotent
        recorders = new ProfilerRecorder[DEFS.Length];
        stats = new SystemStats[DEFS.Length];
        int valid = 0;
        for (int i = 0; i < DEFS.Length; i++)
        {
            try
            {
                recorders[i] = ProfilerRecorder.StartNew(DEFS[i].Category, DEFS[i].Stat, 1);
                stats[i].Name = DEFS[i].DisplayName;
                if (recorders[i].Valid) valid++;
            }
            catch { /* marker not available in this Unity build */ }
        }
        Plugin.Log($"[FrameProfiler][MARKERS] {valid}/{DEFS.Length} built-in markers subscribed");
    }

    public static void Stop()
    {
        if (recorders == null) return;
        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i].Valid) recorders[i].Dispose();
        recorders = null;
        stats = null;
    }

    public static void Sample()
    {
        if (recorders == null) return;
        const float NS_TO_MS = 1f / 1_000_000f;
        for (int i = 0; i < recorders.Length; i++)
        {
            if (!recorders[i].Valid) continue;
            float ms = recorders[i].LastValue * NS_TO_MS;
            if (ms > 0f) stats[i].Record(ms);
        }
        // Once-only diagnostic dump after ~3s so we can see which markers
        // actually emit on this Unity build.
        diagnosticFrames++;
        if (diagnosticFrames == DIAGNOSTIC_DUMP_FRAME)
        {
            Plugin.Log("[FrameProfiler][MARKERS] === Diagnostic — markers emitting after 3s ===");
            int alive = 0;
            for (int i = 0; i < recorders.Length; i++)
            {
                if (!recorders[i].Valid) continue;
                float lastMs = recorders[i].LastValue * NS_TO_MS;
                bool everEmitted = stats[i].Calls > 0;
                if (everEmitted) alive++;
                Plugin.Log($"[FrameProfiler][MARKERS]   {DEFS[i].DisplayName,-48} valid={recorders[i].Valid,-5}  lastMs={lastMs,6:F3}  everEmitted={everEmitted}");
            }
            Plugin.Log($"[FrameProfiler][MARKERS] {alive}/{recorders.Length} markers have emitted >0 samples");
        }
    }

    // Per-window reset; called when the 10s aggregated report dumps.
    public static void ResetWindow()
    {
        if (stats == null) return;
        for (int i = 0; i < stats.Length; i++) stats[i].Reset();
    }

    public static int GetCount() => stats?.Length ?? 0;
    public static SystemStats GetSnapshot(int i) => stats[i];

    // LastValue (in ms) for the most recent frame. Returns 0 for invalid
    // markers or when this Unity build doesn't expose the marker.
    public static float GetLastMs(int i)
    {
        if (recorders == null || i < 0 || i >= recorders.Length) return 0f;
        if (!recorders[i].Valid) return 0f;
        const float NS_TO_MS = 1f / 1_000_000f;
        return recorders[i].LastValue * NS_TO_MS;
    }

    public static string GetName(int i) => stats != null && i >= 0 && i < stats.Length ? stats[i].Name : "";
}
