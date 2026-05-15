// UnicodeFontFallback — restores Unicode glyph coverage that the b323 build of
// Puck lost. The shipped LiberationSans-based font assets only cover basic
// Latin in this version, so glyphs like the server-browser sort arrows render
// as blank boxes (layout still allocates space for them).
//
// Puck uses two parallel text stacks:
//   - TextMeshPro (TMPro.TMP_FontAsset)            — scoreboard, HUD, in-world text
//   - UI Toolkit  (UnityEngine.TextCore.Text.FontAsset via PanelTextSettings)
//                                                  — server browser, settings menus
//
// We register dynamic OS-font fallbacks (DynamicOS atlas population — glyphs
// rasterized on demand from the system .ttf) on BOTH stacks:
//   1. TMP_Settings.fallbackFontAssets               (global TMP fallback)
//   2. Every loaded TMP_FontAsset.fallbackFontAssetTable
//   3. Every loaded TextCore TextSettings.fallbackFontAssets (covers
//      PanelTextSettings used by all UIDocuments)
//
// Then force every existing TMP_Text to re-mesh so blanks resolve.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace ToasterReskinLoader.qol;

public static class UnicodeFontFallback
{
    private static readonly string[] CandidateFonts =
    {
        "Segoe UI Symbol",
        "Segoe UI",
        "Segoe UI Emoji",
        "Arial Unicode MS",
        "Arial",
        "Tahoma"
    };

    private static bool applied;
    private static readonly List<TMP_FontAsset> tmpFallbacks = new List<TMP_FontAsset>();
    private static readonly List<FontAsset> uitkFallbacks = new List<FontAsset>();

    public static void Apply()
    {
        if (applied) return;
        applied = true;

        Plugin.Log("[UnicodeFontFallback] Building Unicode fallback chain...");

        foreach (var family in CandidateFonts)
        {
            TMP_FontAsset tmp = null;
            FontAsset uitk = null;
            try { tmp = TMP_FontAsset.CreateFontAsset(family, "Regular", 90); }
            catch (System.Exception e) { Plugin.LogWarning($"[UnicodeFontFallback] TMP CreateFontAsset({family}) threw: {e.Message}"); }
            try { uitk = FontAsset.CreateFontAsset(family, "Regular", 90); }
            catch (System.Exception e) { Plugin.LogWarning($"[UnicodeFontFallback] UITK CreateFontAsset({family}) threw: {e.Message}"); }

            if (tmp == null && uitk == null)
            {
                Plugin.Log($"[UnicodeFontFallback] Skipped (not installed?): {family}");
                continue;
            }
            if (tmp != null)
            {
                tmp.name = $"TRL_UnicodeFallback_TMP_{family}";
                tmpFallbacks.Add(tmp);
            }
            if (uitk != null)
            {
                uitk.name = $"TRL_UnicodeFallback_UITK_{family}";
                uitkFallbacks.Add(uitk);
            }
            Plugin.Log($"[UnicodeFontFallback] Created fallback: {family} (tmp={tmp != null}, uitk={uitk != null})");
        }

        ApplyTMP();
        ApplyUITK();
        RefreshExistingText();
    }

    private static void ApplyTMP()
    {
        if (tmpFallbacks.Count == 0) return;

        var globalList = TMP_Settings.fallbackFontAssets;
        if (globalList == null)
        {
            globalList = new List<TMP_FontAsset>();
            TMP_Settings.fallbackFontAssets = globalList;
        }
        foreach (var fb in tmpFallbacks)
            if (!globalList.Contains(fb)) globalList.Add(fb);
        Plugin.Log($"[UnicodeFontFallback] TMP_Settings.fallbackFontAssets: {globalList.Count} entries.");

        int patched = 0;
        foreach (var fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (tmpFallbacks.Contains(fa)) continue;
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
            foreach (var fb in tmpFallbacks)
                if (!fa.fallbackFontAssetTable.Contains(fb)) fa.fallbackFontAssetTable.Add(fb);
            patched++;
        }
        Plugin.Log($"[UnicodeFontFallback] Added fallbacks to {patched} TMP_FontAsset(s).");
    }

    private static void ApplyUITK()
    {
        if (uitkFallbacks.Count == 0) return;

        int patchedSettings = 0;
        foreach (var ts in Resources.FindObjectsOfTypeAll<TextSettings>())
        {
            var list = ts.fallbackFontAssets;
            if (list == null)
            {
                list = new List<FontAsset>();
                ts.fallbackFontAssets = list;
            }
            foreach (var fb in uitkFallbacks)
                if (!list.Contains(fb)) list.Add(fb);
            patchedSettings++;
            Plugin.Log($"[UnicodeFontFallback] Patched TextSettings '{ts.name}' ({ts.GetType().Name}): now {list.Count} fallbacks.");
        }
        Plugin.Log($"[UnicodeFontFallback] Total TextSettings patched: {patchedSettings}.");

        int patchedFA = 0;
        foreach (var fa in Resources.FindObjectsOfTypeAll<FontAsset>())
        {
            if (uitkFallbacks.Contains(fa)) continue;
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<FontAsset>();
            foreach (var fb in uitkFallbacks)
                if (!fa.fallbackFontAssetTable.Contains(fb)) fa.fallbackFontAssetTable.Add(fb);
            patchedFA++;
        }
        Plugin.Log($"[UnicodeFontFallback] Added fallbacks to {patchedFA} UITK FontAsset(s).");
    }

    public static void Disable()
    {
        if (!applied) return;
        applied = false;

        // Remove from TMP_Settings global list.
        var tmpGlobal = TMP_Settings.fallbackFontAssets;
        if (tmpGlobal != null)
            foreach (var fb in tmpFallbacks) tmpGlobal.Remove(fb);

        // Remove from every TMP_FontAsset's per-asset table.
        foreach (var fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (fa.fallbackFontAssetTable != null)
                foreach (var fb in tmpFallbacks) fa.fallbackFontAssetTable.Remove(fb);

        // Remove from every UITK TextSettings list.
        foreach (var ts in Resources.FindObjectsOfTypeAll<TextSettings>())
            if (ts.fallbackFontAssets != null)
                foreach (var fb in uitkFallbacks) ts.fallbackFontAssets.Remove(fb);

        // Remove from every UITK FontAsset's per-asset table.
        foreach (var fa in Resources.FindObjectsOfTypeAll<FontAsset>())
            if (fa.fallbackFontAssetTable != null)
                foreach (var fb in uitkFallbacks) fa.fallbackFontAssetTable.Remove(fb);

        tmpFallbacks.Clear();
        uitkFallbacks.Clear();
        RefreshExistingText();
        Plugin.Log("[UnicodeFontFallback] Disabled. (Already-rendered Unicode glyphs may persist in atlases until restart.)");
    }

    private static void RefreshExistingText()
    {
        int refreshed = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            try { t.ForceMeshUpdate(true, true); refreshed++; }
            catch { }
        }
        Plugin.Log($"[UnicodeFontFallback] Re-meshed {refreshed} TMP_Text component(s).");
    }
}
