// PresetApplier.cs
//
// Applies a preset onto the live profile (merge/overlay: only the fields the preset
// contains are written), optionally swapping blue<->red for a team-scoped preset aimed
// at the other side, then runs a broad refresh so the change takes effect immediately.
//
// "Which team?" is a UI concern (phase 4) — this just takes the chosen targetTeam.

using System;
using System.Collections.Generic;
using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.presets;

public sealed class PresetApplyResult
{
    public int AppliedCount;
    public int SkippedCount;
    public bool TeamSwapped;
    public List<PresetDependency> MissingDependencies = new();
}

public static class PresetApplier
{
    /// <param name="targetTeam">
    /// For a team-scoped preset: which side to apply it to. If it differs from the preset's
    /// authored side, team-scoped fields are remapped to their blue/red partner. Ignored for
    /// non-team presets.
    /// </param>
    public static PresetApplyResult Apply(Preset preset, PresetTeam targetTeam = PresetTeam.None)
    {
        var result = new PresetApplyResult();
        if (preset?.Fields == null) return result;

        var profile = ReskinProfileManager.currentProfile;

        bool flip = preset.IsTeamScoped && targetTeam != PresetTeam.None &&
            ((preset.TeamScoped == "blue" && targetTeam == PresetTeam.Red) ||
             (preset.TeamScoped == "red" && targetTeam == PresetTeam.Blue));
        result.TeamSwapped = flip;

        foreach (var prop in preset.Fields.Properties())
        {
            var src = PresetFieldRegistry.ById(prop.Name);
            if (src == null)
            {
                Plugin.LogWarning($"[Presets] Apply: unknown field '{prop.Name}', skipped.");
                result.SkippedCount++;
                continue;
            }

            // Pick the destination field: same field, or its opposite-team partner when swapping.
            var dest = src;
            if (flip && (src.Team == PresetTeam.Blue || src.Team == PresetTeam.Red))
            {
                var partner = PresetFieldRegistry.ById(src.SwapPartnerId);
                if (partner != null) dest = partner;
            }

            if (PresetCodec.TryDecode(src, prop.Value, out var value, result.MissingDependencies))
            {
                dest.SetValue(profile, value);
                result.AppliedCount++;
            }
            else
            {
                result.SkippedCount++;
            }
        }

        ReskinProfileManager.SaveProfile();
        RefreshAll();

        Plugin.Log($"[Presets] Applied '{preset.PresetName}': {result.AppliedCount} setting(s)"
            + (flip ? ", team-swapped" : "")
            + (result.MissingDependencies.Count > 0 ? $", {result.MissingDependencies.Count} missing pack(s)" : "")
            + (result.SkippedCount > 0 ? $", {result.SkippedCount} skipped" : "")
            + ".");
        return result;
    }

    /// Re-apply the whole profile to the world without applying a preset (used after
    /// "Reset all to defaults").
    public static void RefreshWorld() => RefreshAll();

    /// Re-apply everything to the world. Mirrors the Reload button (textures + SetAll) and adds
    /// the few things SetAll doesn't cover (sticks, pucks, team-color notify) so a preset that
    /// touches them takes effect without waiting for a respawn. Each step is isolated so one
    /// failing swapper can't abort the rest.
    private static void RefreshAll()
    {
        Safe(() => ReskinProfileManager.LoadTexturesForActiveReskins(), "load textures");
        Safe(() => SwapperManager.SetAll(), "set all swappers");
        Safe(() => PuckSwapper.SetAllPucksTextures(), "pucks");
        Safe(() =>
        {
            SwapperManager.OnPersonalStickChanged();
            SwapperManager.OnBlueTeamStickChanged();
            SwapperManager.OnRedTeamStickChanged();
        }, "sticks");
        Safe(() => ToasterReskinLoaderAPI.NotifyTeamColorsChanged(), "team colors");
        // In the locker room the in-game swappers above have no live Player to act on — the
        // preview mannequin is driven separately. No-op outside the main menu.
        Safe(() => ChangingRoomHelper.RefreshPreview(), "locker room preview");
    }

    private static void Safe(Action action, string what)
    {
        try { action(); }
        catch (Exception e) { Plugin.LogError($"[Presets] Refresh ({what}) failed: {e.Message}"); }
    }
}
