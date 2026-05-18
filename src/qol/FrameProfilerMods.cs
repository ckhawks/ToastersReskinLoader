using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.qol;

// Cross-mod instrumentation. Discovers every loaded mod assembly
// (anything containing an IPuckPlugin implementation), enumerates its
// MonoBehaviour subclasses, and Harmony-patches their Update /
// LateUpdate / FixedUpdate methods with a shared timing + allocation
// prefix/postfix. Results aggregate per-mod (one row per assembly) so
// the UI can finger-point microstutters at a specific mod without
// drowning the table in per-method rows.
//
// Excludes ourselves (ToasterReskinLoader) so we don't measure our own
// overlay overhead.
public static class FrameProfilerMods
{
    public class ModStats
    {
        public string ModName;
        public int Calls;
        public float TotalMs;
        public float MaxMs;
        public long TotalAllocBytes;
        public long MaxAllocBytes;
        public int PatchedMethods;

        public void Record(float ms, long alloc)
        {
            Calls++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
            TotalAllocBytes += alloc;
            if (alloc > MaxAllocBytes) MaxAllocBytes = alloc;
        }

        public void ResetWindow()
        {
            Calls = 0; TotalMs = 0f; MaxMs = 0f;
            TotalAllocBytes = 0; MaxAllocBytes = 0;
        }
    }

    public struct PerCallState
    {
        public long Ticks;
        public long Mem;
    }

    // method → owning mod stats. Looked up in the postfix.
    static readonly Dictionary<MethodBase, ModStats> methodToStats = new Dictionary<MethodBase, ModStats>();
    // Per-mod aggregate. Keyed by assembly name (for MonoBehaviour discovery)
    // or Harmony owner string (for foreign-patch attribution, prefixed
    // with "[harmony] ").
    static readonly Dictionary<string, ModStats> modStats = new Dictionary<string, ModStats>();

    static readonly string[] SKIP_PREFIXES =
    {
        "UnityEngine", "UnityEditor", "Unity.",
        "System", "Mscorlib", "mscorlib", "netstandard",
        "HarmonyLib", "MonoMod", "0Harmony",
        "Mono.", "ICSharpCode", "BouncyCastle",
        "Newtonsoft", "SharpConfig", "SocketIO",
        "Assembly-CSharp", "Assembly-CSharp-firstpass",
        "ToasterReskinLoader",
    };

    static readonly string[] HOOK_METHODS =
    {
        "Update", "LateUpdate", "FixedUpdate", "OnGUI",
    };

    public static void ApplyPatches(Harmony harmony)
    {
        int totalPatches = 0;
        int totalMods = 0;
        var modAssemblies = DiscoverModAssemblies();
        foreach (var asm in modAssemblies)
        {
            string modName = asm.GetName().Name;
            var stats = new ModStats { ModName = modName };
            modStats[modName] = stats;

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
            catch { continue; }

            int patches = 0;
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

                foreach (var methodName in HOOK_METHODS)
                {
                    MethodInfo m;
                    try
                    {
                        m = t.GetMethod(methodName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            null, Type.EmptyTypes, null);
                    }
                    catch { continue; }
                    if (m == null || m.IsAbstract) continue;

                    try
                    {
                        var prefix = AccessTools.Method(typeof(Patch_ModMethod), nameof(Patch_ModMethod.Prefix));
                        var postfix = AccessTools.Method(typeof(Patch_ModMethod), nameof(Patch_ModMethod.Postfix));
                        harmony.Patch(m,
                            prefix: new HarmonyMethod(prefix),
                            postfix: new HarmonyMethod(postfix));
                        methodToStats[m] = stats;
                        patches++;
                    }
                    catch { /* method may be inlined/intrinsic; skip silently */ }
                }
            }

            stats.PatchedMethods = patches;
            totalPatches += patches;
            if (patches > 0) totalMods++;
            Plugin.Log($"[FrameProfiler][MODS] {modName}: patched {patches} Update/LateUpdate/FixedUpdate/OnGUI methods");
        }

        Plugin.Log($"[FrameProfiler][MODS] {totalPatches} method patches across {totalMods} mods");
        ApplyHarmonyAttributionPatches(harmony);
    }

    // Attribute Harmony-patched cost back to the patching mod. Walks every
    // method patched by anyone (us, foreign mods, the game itself), and for
    // each foreign prefix/postfix/finalizer, patches that patch method with
    // a timing wrapper. The owner string on each Harmony Patch identifies
    // which mod installed it.
    public static void ApplyHarmonyAttributionPatches(Harmony harmony)
    {
        string ourId = harmony.Id;
        int count = 0;
        int ownerCount = 0;
        var seenOwners = new HashSet<string>();

        MethodBase[] allPatched;
        try { allPatched = Harmony.GetAllPatchedMethods().ToArray(); }
        catch (Exception e) { Plugin.LogError($"[FrameProfiler][HARMONY] GetAllPatchedMethods failed: {e.Message}"); return; }

        foreach (var m in allPatched)
        {
            HarmonyLib.Patches info;
            try { info = Harmony.GetPatchInfo(m); }
            catch { continue; }
            if (info == null) continue;
            count += PatchEach(harmony, info.Prefixes, ourId, seenOwners);
            count += PatchEach(harmony, info.Postfixes, ourId, seenOwners);
            count += PatchEach(harmony, info.Finalizers, ourId, seenOwners);
            // Skip transpilers — they execute once at IL-rewrite time, not
            // per-call, so timing them gives 0 meaningful runtime data.
        }
        ownerCount = seenOwners.Count;
        Plugin.Log($"[FrameProfiler][HARMONY] Wrapped {count} foreign Harmony patches across {ownerCount} owners (excludes our own '{ourId}')");
    }

    static int PatchEach(Harmony ourHarmony, System.Collections.ObjectModel.ReadOnlyCollection<HarmonyLib.Patch> patches,
                         string ourId, HashSet<string> seenOwners)
    {
        if (patches == null || patches.Count == 0) return 0;
        int n = 0;
        var prefix  = AccessTools.Method(typeof(Patch_HarmonyAttribute), nameof(Patch_HarmonyAttribute.Prefix));
        var postfix = AccessTools.Method(typeof(Patch_HarmonyAttribute), nameof(Patch_HarmonyAttribute.Postfix));
        foreach (var p in patches)
        {
            if (p == null) continue;
            if (string.IsNullOrEmpty(p.owner) || p.owner == ourId) continue;
            var pm = p.PatchMethod;
            if (pm == null) continue;
            if (methodToStats.ContainsKey(pm)) { seenOwners.Add(p.owner); continue; } // already wrapped

            string displayName = "[harmony] " + p.owner;
            if (!modStats.TryGetValue(displayName, out var stats))
            {
                stats = new ModStats { ModName = displayName };
                modStats[displayName] = stats;
            }
            try
            {
                ourHarmony.Patch(pm,
                    prefix:  new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix));
                methodToStats[pm] = stats;
                stats.PatchedMethods++;
                seenOwners.Add(p.owner);
                n++;
            }
            catch { /* method may be inlined / abstract / unsupported */ }
        }
        return n;
    }

    public static class Patch_HarmonyAttribute
    {
        public static void Prefix(out PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }
        public static void Postfix(MethodBase __originalMethod, PerCallState __state)
        {
            try
            {
                long elapsed = Stopwatch.GetTimestamp() - __state.Ticks;
                float ms = (float)elapsed / Stopwatch.Frequency * 1000f;
                long alloc = Math.Max(0, GC.GetTotalMemory(false) - __state.Mem);
                if (methodToStats.TryGetValue(__originalMethod, out var s))
                    s.Record(ms, alloc);
            }
            catch { }
        }
    }

    static List<Assembly> DiscoverModAssemblies()
    {
        var result = new List<Assembly>();
        // Path-based detection: anything under <gameRoot>/Plugins or a
        // Steam Workshop content folder is a mod. This catches mods whose
        // main assembly doesn't directly implement IPuckPlugin (e.g. the
        // interface impl lives in a separate file).
        string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string pluginsDir = Path.Combine(gameRoot, "Plugins");
        // e.g. ...steamapps\workshop\content\3164490\1234567890
        const string WORKSHOP_HINT = @"workshop\content";

        int kept = 0;
        int considered = 0;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            considered++;
            string name = asm.GetName().Name;
            bool skipByName = false;
            foreach (var p in SKIP_PREFIXES) if (name.StartsWith(p)) { skipByName = true; break; }
            if (skipByName) continue;

            // Dynamic assemblies (Harmony-generated, etc.) have no Location.
            string location = null;
            try { if (!asm.IsDynamic) location = asm.Location; } catch { }

            bool pathSaysMod = !string.IsNullOrEmpty(location)
                && (location.StartsWith(pluginsDir, StringComparison.OrdinalIgnoreCase)
                    || location.IndexOf(WORKSHOP_HINT, StringComparison.OrdinalIgnoreCase) >= 0);

            bool hasPlugin = false;
            try
            {
                Type[] ts;
                try { ts = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { ts = rtle.Types; }
                if (ts != null)
                {
                    foreach (var t in ts)
                    {
                        if (t == null || t.IsAbstract || t.IsInterface) continue;
                        if (typeof(IPuckPlugin).IsAssignableFrom(t)) { hasPlugin = true; break; }
                    }
                }
            }
            catch { }

            if (pathSaysMod || hasPlugin)
            {
                result.Add(asm);
                kept++;
                Plugin.Log($"[FrameProfiler][MODS] DISCOVERED  {name,-40} hasPlugin={hasPlugin}  pathMatch={pathSaysMod}  loc={location}");
            }
            else if (location != null)
            {
                // Useful negative-result log to see what we're rejecting.
                Plugin.LogDebug($"[FrameProfiler][MODS] skip {name} (loc={location})");
            }
        }
        Plugin.Log($"[FrameProfiler][MODS] Considered {considered} assemblies, kept {kept} mod candidates");
        return result;
    }

    public static int GetCount() => modStats.Count;
    public static IEnumerable<ModStats> Snapshot() => modStats.Values;

    public static void ResetAllWindows()
    {
        foreach (var s in modStats.Values) s.ResetWindow();
    }

    public static void Reset()
    {
        methodToStats.Clear();
        modStats.Clear();
    }

    public static class Patch_ModMethod
    {
        public static void Prefix(out PerCallState __state)
        {
            __state.Ticks = Stopwatch.GetTimestamp();
            __state.Mem = GC.GetTotalMemory(false);
        }

        public static void Postfix(MethodBase __originalMethod, PerCallState __state)
        {
            try
            {
                long elapsed = Stopwatch.GetTimestamp() - __state.Ticks;
                float ms = (float)elapsed / Stopwatch.Frequency * 1000f;
                long alloc = Math.Max(0, GC.GetTotalMemory(false) - __state.Mem);
                if (methodToStats.TryGetValue(__originalMethod, out var s))
                {
                    s.Record(ms, alloc);
                }
            }
            catch { }
        }
    }
}
