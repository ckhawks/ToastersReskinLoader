using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.diagnostics.profiler;

// Frame timing / stutter profiler. Ported from the standalone
// ToasterFrameProfiler mod and gated behind QoL.enableFrameProfiler in the
// Developer section. F4 = cycle modes, F5 = toggle CSV log.
public static class FrameProfiler
{
    const string HARMONY_ID = "pw.stellaric.toaster.reskinloader.frameprofiler";
    static readonly Harmony harmony = new Harmony(HARMONY_ID);

    static GameObject overlayHost;
    public static FrameProfilerOverlay Overlay;
    static bool enabled;

    public static void Enable()
    {
        if (enabled) return;
        try
        {
            FrameProfilerPatches.ApplyPatches(harmony);
            FrameProfilerNetwork.ApplyPatches(harmony);

            // Optional: patch other mods' Update methods for per-mod cost.
            // Off by default — adds 100s of harmony patches at load time.
            var cfg = QoLRunner.Instance?.Config;
            if (cfg != null && cfg.enableFrameProfilerModInstrumentation)
            {
                FrameProfilerMods.ApplyPatches(harmony);
            }

            if (IsDedicatedServer())
            {
                Plugin.Log("[FrameProfiler] Dedicated server detected - running in log-only mode.");
            }
            else
            {
                overlayHost = new GameObject("ToasterFrameProfiler");
                UnityEngine.Object.DontDestroyOnLoad(overlayHost);
                Overlay = overlayHost.AddComponent<FrameProfilerOverlay>();
                Plugin.Log("[FrameProfiler] Enabled. F4 = mode, F5 = CSV.");
            }

            enabled = true;
        }
        catch (Exception e)
        {
            Plugin.LogError($"[FrameProfiler] Enable failed: {e.Message}\n{e.StackTrace}");
        }
    }

    public static void Disable()
    {
        if (!enabled) return;
        try
        {
            harmony.UnpatchSelf();
            FrameProfilerNetwork.Reset();
            FrameProfilerMods.Reset();
            if (overlayHost != null)
            {
                UnityEngine.Object.Destroy(overlayHost);
                overlayHost = null;
                Overlay = null;
            }
            Plugin.Log("[FrameProfiler] Disabled.");
        }
        catch (Exception e)
        {
            Plugin.LogError($"[FrameProfiler] Disable failed: {e.Message}");
        }
        finally
        {
            enabled = false;
        }
    }

    static bool IsDedicatedServer()
    {
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }
}
