// PluginEnableTimer.cs
//
// Times how long each plugin/mod's Enable() takes and logs one line per enable,
// e.g. "[EnableTiming] plugin 'ToastersRinkCompanion': 1123 ms (ok=True)".
//
// The game enables plugins and workshop mods through BasePlugin<BasePluginState>
// .Enable() (both `Plugin` and `Mod` derive from that same closed generic and
// neither overrides Enable), so a single patch covers everything. It's applied by
// TRL's main harmony.PatchAll() at the top of OnEnable, so it measures every
// plugin/mod that enables AFTER TRL — from the startup logs that's all of them
// except whichever one sorts before TRL (e.g. ToasterCameras), which can still be
// read from the game's own timestamped "[X] Enabling…" log lines.
//
// Limitation by construction: TRL can't time itself or anything enabled before it,
// because the patch doesn't exist until TRL loads. That's inherent to doing this
// from inside a plugin.

using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.diagnostics;

[HarmonyPatch]
public static class PluginEnableTimer
{
    // Patch the shared base Enable() that both Plugin and Mod inherit.
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(BasePlugin<BasePluginState>), "Enable");

    private static void Prefix(out Stopwatch __state) => __state = Stopwatch.StartNew();

    private static void Postfix(object __instance, bool __result, Stopwatch __state)
    {
        __state.Stop();

        // Enable() returns in ~0ms when a plugin is already enabled / not ready, and
        // asset-only workshop mods have no assembly to run OnEnable — both are noise.
        if (__state.ElapsedMilliseconds < 2) return;
        if (!(Settings.Current?.enablePluginEnableTiming ?? true)) return;

        string id, kind;
        if (__instance is global::Mod m) { id = SafeModId(m); kind = "mod"; }
        else if (__instance is global::Plugin p) { id = p.Id; kind = "plugin"; }
        else { id = __instance?.GetType().Name ?? "?"; kind = "?"; }

        Plugin.Log($"[EnableTiming] {kind} '{id}': {__state.ElapsedMilliseconds} ms (ok={__result})");
    }

    private static string SafeModId(global::Mod m)
    {
        try { return m.Id; } catch { return "?"; }
    }
}
