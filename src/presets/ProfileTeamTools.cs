// ProfileTeamTools.cs
//
// Copy appearance settings from one (team, role) cell to another. Fields are matched across
// cells by a "base key" — the field id with its team and role tokens normalized out — so the
// same setting in different cells lines up (blueSkaterTorso & redGoalieTorso both -> "Torso").
// Backs the 2x2 editor's "copy from <cell>" action.

using System.Collections.Generic;
using System.Linq;

namespace ToasterReskinLoader.presets;

public static class ProfileTeamTools
{
    /// The four player cells, in display order.
    public static readonly (PresetTeam Team, PresetRole Role)[] Cells =
    {
        (PresetTeam.Blue, PresetRole.Skater),
        (PresetTeam.Blue, PresetRole.Goalie),
        (PresetTeam.Red, PresetRole.Skater),
        (PresetTeam.Red, PresetRole.Goalie),
    };

    /// Copy every role-specific setting from the source cell to the matching setting in the
    /// target cell. Only settings present in BOTH cells are copied (e.g. goalie pads/mask have
    /// no skater equivalent, so a skater->goalie copy leaves them untouched). Reskin fields are
    /// also skipped when source and target use different reskin models — skater and goalie
    /// sticks share a base key ("stick") but use distinct models (stick_attacker vs
    /// stick_goalie), so a skater stick skin must not bleed onto a goalie stick. Returns the
    /// count copied. Mutates the live profile in memory; the caller saves + refreshes.
    public static int CopyCell(PresetTeam fromTeam, PresetRole fromRole, PresetTeam toTeam, PresetRole toRole)
    {
        if (fromTeam == toTeam && fromRole == toRole) return 0;

        var profile = ReskinProfileManager.currentProfile;

        var targets = PresetFieldRegistry.All
            .Where(f => f.Team == toTeam && f.Role == toRole)
            .GroupBy(BaseKey)
            .ToDictionary(g => g.Key, g => g.First());

        int copied = 0;
        foreach (var src in PresetFieldRegistry.All.Where(f => f.Team == fromTeam && f.Role == fromRole))
        {
            if (targets.TryGetValue(BaseKey(src), out var dst))
            {
                // Reskin refs only fit fields of the same reskin model. Sticks collide on base
                // key across roles but use different models, so skip the mismatch.
                if (src.ReskinType != dst.ReskinType) continue;

                dst.SetValue(profile, src.GetValue(profile));
                copied++;
            }
        }

        return copied;
    }

    private static string BaseKey(PresetField f)
    {
        string n = f.Id;
        n = n.Replace("Blue", "").Replace("blue", "").Replace("Red", "").Replace("red", "");
        n = n.Replace("Attacker", "").Replace("attacker", "")
             .Replace("Skater", "").Replace("skater", "")
             .Replace("Goalie", "").Replace("goalie", "");
        return n;
    }
}
