using System;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.api;
using ToasterReskinLoader.display;

namespace ToasterReskinLoader;

/// <summary>
/// Public API for other mods to read TRL settings.
///
/// Hard dependency (reference ToasterReskinLoader.dll):
///   var blue = ToasterReskinLoaderAPI.BlueTeamColor;
///   ToasterReskinLoaderAPI.OnTeamColorsChanged += () => { /* re-read colors */ };
///
/// Soft/optional dependency (reflection, no DLL reference needed):
///   Type api = null;
///   foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
///   {
///       api = asm.GetType("ToasterReskinLoader.ToasterReskinLoaderAPI");
///       if (api != null) break;
///   }
///   if (api != null)
///   {
///       // Read properties
///       bool enabled = (bool)api.GetProperty("TeamColorsEnabled").GetValue(null);
///       Color blue   = (Color)api.GetProperty("BlueTeamColor").GetValue(null);
///       Color red    = (Color)api.GetProperty("RedTeamColor").GetValue(null);
///       string blueName = (string)api.GetProperty("BlueTeamName").GetValue(null);
///       string redName  = (string)api.GetProperty("RedTeamName").GetValue(null);
///
///       // Subscribe to changes
///       var evt = api.GetEvent("OnTeamColorsChanged");
///       var handler = Delegate.CreateDelegate(evt.EventHandlerType, myObj, "OnColorsChanged");
///       evt.AddEventHandler(null, handler);
///   }
/// </summary>
public static class ToasterReskinLoaderAPI
{
    private static readonly Color DefaultBlue = new Color(0.231f, 0.510f, 0.965f, 1f);
    private static readonly Color DefaultRed = new Color(0.820f, 0.200f, 0.200f, 1f);

    /// <summary>
    /// Fired whenever the user changes team color settings (enable/disable, color values).
    /// Subscribe to this to react to changes in real time.
    /// </summary>
    public static event Action OnTeamColorsChanged;

    /// <summary>Whether custom team colors are enabled by the user.</summary>
    public static bool TeamColorsEnabled =>
        (ReskinProfileManager.currentProfile?.blueTeamColorEnabled ?? false)
        || (ReskinProfileManager.currentProfile?.redTeamColorEnabled ?? false);

    /// <summary>The user's custom blue team color, or the default if not customized.</summary>
    public static Color BlueTeamColor =>
        ReskinProfileManager.currentProfile?.blueTeamColor ?? DefaultBlue;

    /// <summary>The user's custom red team color, or the default if not customized.</summary>
    public static Color RedTeamColor =>
        ReskinProfileManager.currentProfile?.redTeamColor ?? DefaultRed;

    /// <summary>The user's custom blue team name, or null/empty if not set.</summary>
    public static string BlueTeamName =>
        ReskinProfileManager.currentProfile?.blueTeamName ?? "";

    /// <summary>The user's custom red team name, or null/empty if not set.</summary>
    public static string RedTeamName =>
        ReskinProfileManager.currentProfile?.redTeamName ?? "";

    // ── Minimap settings ────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the user changes minimap settings (colors, scales).
    /// Subscribe to this to react to changes in real time.
    /// </summary>
    public static event Action OnMinimapSettingsChanged;

    /// <summary>The user's minimap puck color.</summary>
    public static Color MinimapPuckColor =>
        core.Settings.Current?.minimapPuckColor ?? Color.black;

    /// <summary>The user's minimap puck icon scale multiplier (default 1.0).</summary>
    public static float MinimapPuckScale =>
        core.Settings.Current?.minimapPuckScale ?? 1f;

    /// <summary>The user's minimap refresh rate in updates per second (default 60).</summary>
    public static int MinimapRefreshRate =>
        core.Settings.Current?.minimapRefreshRate ?? 60;

    /// <summary>The user's minimap player icon scale multiplier (default 1.0).</summary>
    public static float MinimapPlayerScale =>
        core.Settings.Current?.minimapPlayerScale ?? 1f;

    /// <summary>Call this internally whenever minimap settings change.</summary>
    internal static void NotifyMinimapSettingsChanged()
    {
        try
        {
            display.MinimapSwapper.RefreshAll();
            display.MinimapSwapper.ApplyRefreshRate();
            OnMinimapSettingsChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"ToasterReskinLoaderAPI.OnMinimapSettingsChanged handler error: {e.Message}");
        }
    }

    // ── Settings panels ─────────────────────────────────────────────────
    //
    // Let another mod add its own page to the Reskin Manager menu. The page appears as a
    // sidebar button under the "Plugins" bucket; when opened, your build callback is handed
    // an empty content container to fill. Use SettingsPanelUI for rows that match TRL's look.
    //
    // Hard dependency (reference ToasterReskinLoader.dll):
    //   ToasterReskinLoaderAPI.RegisterSettingsPanel("MyMod", "My Mod", "My Mod", root =>
    //   {
    //       SettingsPanelUI.Note(root, "Tweak my mod here.");
    //       SettingsPanelUI.Toggle(root, "Enable thing", cfg.thing, v => { cfg.thing = v; Save(); });
    //   });
    //
    // Soft/optional dependency (reflection, no DLL reference — recommended for cross-mod use):
    //   Type api = null;
    //   foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    //   {
    //       api = asm.GetType("ToasterReskinLoader.ToasterReskinLoaderAPI");
    //       if (api != null) break;
    //   }
    //   if (api != null)
    //   {
    //       var m = api.GetMethod("RegisterSettingsPanel");
    //       // build is an Action<VisualElement>; UnityEngine.UIElements is shared by both mods.
    //       m.Invoke(null, new object[] { "MyMod", "My Mod", "My Mod", (Action<VisualElement>)Build, 0 });
    //   }
    //   // Inside Build, call the SettingsPanelUI helpers over reflection to match TRL's styling,
    //   // or just add raw VisualElements to the supplied root.

    /// <summary>
    /// Register a settings page contributed by another mod. <paramref name="id"/> is a stable
    /// unique key (re-registering replaces); <paramref name="title"/> is the sidebar label
    /// (keep it distinct from TRL's built-in sections); <paramref name="group"/> is an optional
    /// right-aligned tag; <paramref name="build"/> fills the content container each time the
    /// page is shown; <paramref name="order"/> sorts within the Plugins bucket.
    /// </summary>
    public static void RegisterSettingsPanel(string id, string title, string group,
        Action<VisualElement> build, int order = 0)
        => SettingsPanelRegistry.Register(id, title, group, build, order);

    /// <summary>Remove a previously registered settings page.</summary>
    public static void UnregisterSettingsPanel(string id)
        => SettingsPanelRegistry.Unregister(id);

    /// <summary>Call this internally whenever team color settings change.</summary>
    internal static void NotifyTeamColorsChanged()
    {
        try
        {
            // Refresh TRL's own UI overrides
            swappers.TeamColorSwapper.RefreshAll();

            // Notify external mods
            OnTeamColorsChanged?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"ToasterReskinLoaderAPI.OnTeamColorsChanged handler error: {e.Message}");
        }
    }
}
