using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.swappers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader;

public static class ChangingRoomHelper
{
    private static LockerRoomStick lockerRoomStick;
    private static LockerRoomPlayer lockerRoomPlayer;
    private static LockerRoomCamera lockerRoomCamera;

    // Saved user settings to restore on menu close
    private static PlayerTeam savedTeam;
    private static PlayerRole savedRole;
    private static bool hasOverride;

    // Reflection fields
    static readonly FieldInfo _attackerStickMeshField = typeof(LockerRoomStick)
        .GetField("attackerStickMesh", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _goalieStickMeshField = typeof(LockerRoomStick)
        .GetField("goalieStickMesh", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _playerMeshField = typeof(LockerRoomPlayer)
        .GetField("playerMesh", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _usernameTextField = typeof(PlayerTorso)
        .GetField("usernameText", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo _numberTextField = typeof(PlayerTorso)
        .GetField("numberText", BindingFlags.Instance | BindingFlags.NonPublic);

    public static StickMesh GetStickMesh(PlayerRole role)
    {
        if (lockerRoomStick == null) return null;
        return role == PlayerRole.Goalie
            ? (StickMesh)_goalieStickMeshField.GetValue(lockerRoomStick)
            : (StickMesh)_attackerStickMeshField.GetValue(lockerRoomStick);
    }

    public static PlayerMesh GetPlayerMesh()
    {
        if (lockerRoomPlayer == null) return null;
        return (PlayerMesh)_playerMeshField?.GetValue(lockerRoomPlayer);
    }

    public static bool IsInMainMenu()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "locker_room";
    }

    // ==================== PREVIEW CONTEXT ====================

    /// <summary>
    /// Switches the locker room model to show a specific team+role.
    /// Saves the user's real settings so they can be restored later.
    /// Fires game events so the game's controllers update the model,
    /// then our Harmony postfixes apply our textures.
    /// </summary>
    public static void SetPreviewContext(PlayerTeam team, PlayerRole role)
    {
        if (!IsInMainMenu()) return;
        Scan();

        // Save original settings on first override
        if (!hasOverride)
        {
            savedTeam = SettingsManager.Team;
            savedRole = SettingsManager.Role;
            hasOverride = true;
        }

        // Set the fields directly (no SaveManager persist)
        bool teamChanged = SettingsManager.Team != team;
        bool roleChanged = SettingsManager.Role != role;
        SettingsManager.Team = team;
        SettingsManager.Role = role;

        // Fire events so game controllers update the model + stick
        if (teamChanged)
            EventManager.TriggerEvent("Event_OnTeamChanged", new Dictionary<string, object> { { "value", team } });
        if (roleChanged)
            EventManager.TriggerEvent("Event_OnRoleChanged", new Dictionary<string, object> { { "value", role } });

        // If neither changed, the events won't fire and our patches won't run.
        // Force a refresh in that case.
        if (!teamChanged && !roleChanged)
            ForceModelRefresh(team, role);

        // Apply everything our patches might miss (colors, tapes, lettering)
        ApplyCustomizationsAfterGameUpdate(team, role);
    }

    /// <summary>
    /// Restores the locker room model to the user's real settings.
    /// Called when closing the reskin menu.
    /// </summary>
    public static void ResetPreviewContext()
    {
        if (!hasOverride) return;

        hasOverride = false;
        SettingsManager.Team = savedTeam;
        SettingsManager.Role = savedRole;

        // Fire events to restore
        EventManager.TriggerEvent("Event_OnTeamChanged", new Dictionary<string, object> { { "value", savedTeam } });
        EventManager.TriggerEvent("Event_OnRoleChanged", new Dictionary<string, object> { { "value", savedRole } });

        // Apply our customizations for the restored context
        ApplyCustomizationsAfterGameUpdate(savedTeam, savedRole);
    }

    /// <summary>
    /// Re-applies all our customizations for the current preview context.
    /// Called by UI callbacks after any dropdown/slider/color change.
    /// </summary>
    public static void RefreshPreview()
    {
        if (!IsInMainMenu()) return;
        Scan();

        var team = SettingsManager.Team;
        var role = SettingsManager.Role;

        // Force the game to re-apply its defaults so our postfixes re-fire
        ForceModelRefresh(team, role);

        // Apply everything our patches might miss
        ApplyCustomizationsAfterGameUpdate(team, role);
    }

    // ==================== INTERNAL ====================

    /// <summary>
    /// Forces the game's locker room model to re-apply by calling Set methods directly.
    /// This triggers our Harmony postfixes on SetJerseyID/SetHeadgearID/SetSkinID.
    /// </summary>
    private static void ForceModelRefresh(PlayerTeam team, PlayerRole role)
    {
        if (lockerRoomPlayer != null)
        {
            lockerRoomPlayer.SetLegsPadsActive(role == PlayerRole.Goalie);
            lockerRoomPlayer.SetJerseyID(SettingsManager.GetJerseyID(team, role), team);
            lockerRoomPlayer.SetHeadgearID(SettingsManager.GetHeadgearID(team, role), role);
        }

        if (lockerRoomStick != null)
        {
            lockerRoomStick.ShowRoleStick(role);
            lockerRoomStick.SetSkinID(SettingsManager.GetStickSkinID(team, role), team, role);
            lockerRoomStick.SetShaftTapeID(SettingsManager.GetStickShaftTapeID(team, role), role);
            lockerRoomStick.SetBladeTapeID(SettingsManager.GetStickBladeTapeID(team, role), role);
        }
    }

    /// <summary>
    /// Applies customizations that our Harmony postfixes don't cover:
    /// colors (helmet, cage, leg pads), lettering, stick tape overrides.
    /// </summary>
    private static void ApplyCustomizationsAfterGameUpdate(PlayerTeam team, PlayerRole role)
    {
        try
        {
            var playerMesh = GetPlayerMesh();
            if (playerMesh != null)
            {
                // Helmet/mask/cage colors (patches apply textures but not standalone colors)
                ApplyHelmetColors(playerMesh, team, role);

                // Leg pad colors
                ApplyLegPadColors(playerMesh, team);

                // Lettering colors
                ApplyLetteringColor(playerMesh, team, role);
            }

            // Stick tape
            StickMesh stickMesh = GetStickMesh(role);
            if (stickMesh != null)
            {
                StickTapeSwapper.ApplyTapeToStickMesh(stickMesh, team, role);
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error in ApplyCustomizationsAfterGameUpdate: {e.Message}");
        }
    }

    /// <summary>
    /// Applies helmet/mask/cage color overrides. The head hierarchy contains these renderers:
    ///   Goalie: "Helmet Cage &amp; Neck Guard (Goalie)" (main shell), "Cage", "Neck Guard"
    ///   Skater: "Helmet ..." (excludes cage/neck variants)
    /// Colors are only applied when no custom texture is set for that piece.
    /// </summary>
    private static void ApplyHelmetColors(PlayerMesh playerMesh, PlayerTeam team, PlayerRole role)
    {
        if (playerMesh.PlayerHead == null) return;

        var renderers = playerMesh.PlayerHead.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            string name = renderer.gameObject.name.ToLower();

            if (role == PlayerRole.Goalie)
            {
                // Main goalie mask shell: "Helmet Cage & Neck Guard (Goalie)"
                if (name.Contains("helmet") && name.Contains("goalie"))
                {
                    var entry = team == PlayerTeam.Blue
                        ? ReskinProfileManager.currentProfile.blueGoalieHelmet
                        : ReskinProfileManager.currentProfile.redGoalieHelmet;
                    if (entry == null || entry.Path == null)
                    {
                        SetMaterialColor(renderer.material, team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueGoalieHelmetColor
                            : ReskinProfileManager.currentProfile.redGoalieHelmetColor);
                    }
                }
                // Standalone cage: "Cage"
                else if (name == "cage")
                {
                    SetMaterialColor(renderer.material, team == PlayerTeam.Blue
                        ? ReskinProfileManager.currentProfile.blueGoalieCageColor
                        : ReskinProfileManager.currentProfile.redGoalieCageColor);
                }
                // Standalone neck guard: "Neck Guard"
                else if (name == "neck guard")
                {
                    var entry = team == PlayerTeam.Blue
                        ? ReskinProfileManager.currentProfile.blueGoalieMask
                        : ReskinProfileManager.currentProfile.redGoalieMask;
                    if (entry == null || entry.Path == null)
                    {
                        SetMaterialColor(renderer.material, team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueGoalieMaskColor
                            : ReskinProfileManager.currentProfile.redGoalieMaskColor);
                    }
                }
            }
            else
            {
                // Skater helmet color (only when no custom texture)
                if (name.Contains("helmet") && !name.Contains("cage") && !name.Contains("neck"))
                {
                    var entry = team == PlayerTeam.Blue
                        ? ReskinProfileManager.currentProfile.blueSkaterHelmet
                        : ReskinProfileManager.currentProfile.redSkaterHelmet;
                    if (entry == null || entry.Path == null)
                    {
                        SetMaterialColor(renderer.material, team == PlayerTeam.Blue
                            ? ReskinProfileManager.currentProfile.blueSkaterHelmetColor
                            : ReskinProfileManager.currentProfile.redSkaterHelmetColor);
                    }
                }
            }
        }
    }

    private static void ApplyLegPadColors(PlayerMesh playerMesh, PlayerTeam team)
    {
        // Leg pad default colors (when no custom texture) — applied per-pad independently
        if (playerMesh.PlayerLegPadLeft == null || playerMesh.PlayerLegPadRight == null) return;

        var leftEntry = team == PlayerTeam.Blue
            ? ReskinProfileManager.currentProfile.blueLegPadLeft
            : ReskinProfileManager.currentProfile.redLegPadLeft;
        var rightEntry = team == PlayerTeam.Blue
            ? ReskinProfileManager.currentProfile.blueLegPadRight
            : ReskinProfileManager.currentProfile.redLegPadRight;

        Color padColor = team == PlayerTeam.Blue
            ? ReskinProfileManager.currentProfile.blueLegPadDefaultColor
            : ReskinProfileManager.currentProfile.redLegPadDefaultColor;

        if (leftEntry == null || leftEntry.Path == null)
        {
            var leftRenderer = playerMesh.PlayerLegPadLeft.GetComponent<Renderer>();
            if (leftRenderer != null) SetMaterialColor(leftRenderer.material, padColor);
        }

        if (rightEntry == null || rightEntry.Path == null)
        {
            var rightRenderer = playerMesh.PlayerLegPadRight.GetComponent<Renderer>();
            if (rightRenderer != null) SetMaterialColor(rightRenderer.material, padColor);
        }
    }

    private static void ApplyLetteringColor(PlayerMesh playerMesh, PlayerTeam team, PlayerRole role)
    {
        if (playerMesh.PlayerTorso == null) return;

        Color textColor;
        if (team == PlayerTeam.Blue)
            textColor = role == PlayerRole.Goalie
                ? ReskinProfileManager.currentProfile.blueGoalieLetteringColor
                : ReskinProfileManager.currentProfile.blueSkaterLetteringColor;
        else if (team == PlayerTeam.Red)
            textColor = role == PlayerRole.Goalie
                ? ReskinProfileManager.currentProfile.redGoalieLetteringColor
                : ReskinProfileManager.currentProfile.redSkaterLetteringColor;
        else return;

        try
        {
            var usernameText = (TMPro.TMP_Text)_usernameTextField?.GetValue(playerMesh.PlayerTorso);
            var numberText = (TMPro.TMP_Text)_numberTextField?.GetValue(playerMesh.PlayerTorso);
            if (usernameText != null) usernameText.color = textColor;
            if (numberText != null) numberText.color = textColor;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error applying lettering color: {e.Message}");
        }
    }

    /// <summary>
    /// Sets color on a material using all known property names for b310 shaders.
    /// </summary>
    private static void SetMaterialColor(Material mat, Color color)
    {
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("baseColorFactor")) mat.SetColor("baseColorFactor", color);
    }

    // ==================== FACIAL HAIR ====================

    public static void SetBeardID(int beardID)
    {
        if (!IsInMainMenu()) return;
        Scan();
        if (lockerRoomPlayer != null)
            lockerRoomPlayer.SetBeardID(beardID);
    }

    public static void SetMustacheID(int mustacheID)
    {
        if (!IsInMainMenu()) return;
        Scan();
        if (lockerRoomPlayer != null)
            lockerRoomPlayer.SetMustacheID(mustacheID);
    }

    /// <summary>
    /// Logs mustache and beard ID-to-GameObject mappings from the locker room PlayerHead.
    /// Call once to discover the actual ID assignments.
    /// </summary>
    public static void LogFacialHairMappings()
    {
        if (!IsInMainMenu()) return;
        Scan();
        var playerMesh = GetPlayerMesh();
        if (playerMesh?.PlayerHead == null) return;

        // Use reflection to access the serialized lists
        var headType = typeof(PlayerHead);
        var mustachesField = headType.GetField("mustaches", BindingFlags.Instance | BindingFlags.NonPublic);
        var beardsField = headType.GetField("beards", BindingFlags.Instance | BindingFlags.NonPublic);

        if (mustachesField != null)
        {
            var mustaches = (System.Collections.IList)mustachesField.GetValue(playerMesh.PlayerHead);
            Plugin.Log($"[FacialHair] Mustache mappings ({mustaches.Count}):");
            foreach (var m in mustaches)
            {
                var idField2 = m.GetType().GetField("ID");
                var goField2 = m.GetType().GetField("GameObject");
                int id = (int)idField2.GetValue(m);
                GameObject go = (GameObject)goField2.GetValue(m);
                Plugin.Log($"[FacialHair]   ID={id} -> {go?.name ?? "null"}");
            }
        }

        if (beardsField != null)
        {
            var beards = (System.Collections.IList)beardsField.GetValue(playerMesh.PlayerHead);
            Plugin.Log($"[FacialHair] Beard mappings ({beards.Count}):");
            foreach (var b in beards)
            {
                var idField2 = b.GetType().GetField("ID");
                var goField2 = b.GetType().GetField("GameObject");
                int id = (int)idField2.GetValue(b);
                GameObject go = (GameObject)goField2.GetValue(b);
                Plugin.Log($"[FacialHair]   ID={id} -> {go?.name ?? "null"}");
            }
        }
    }

    // ==================== CAMERA ====================

    public static void ShowStick()
    {
        if (!IsInMainMenu()) return;
        Scan();
        if (lockerRoomCamera != null)
            lockerRoomCamera.SetPosition("stickCloseUp");
        if (lockerRoomPlayer != null)
            lockerRoomPlayer.AllowRotation = false;
    }

    public static void ShowBody()
    {
        if (!IsInMainMenu()) return;
        Scan();
        try
        {
            if (lockerRoomCamera != null)
                lockerRoomCamera.SetPosition("bodyCloseUp");
            if (lockerRoomPlayer != null)
                lockerRoomPlayer.AllowRotation = true;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error ShowBody(): {e.Message}");
        }
    }

    public static void ShowBaseFocus()
    {
        if (!IsInMainMenu()) return;
        Scan();
        if (lockerRoomCamera != null)
            lockerRoomCamera.SetPosition("bodyCloseUp");
        if (lockerRoomPlayer != null)
        {
            lockerRoomPlayer.AllowRotation = false;
            lockerRoomPlayer.SetRotationFromPreset("front");
        }
    }

    public static void Unfocus()
    {
        if (!IsInMainMenu()) return;
        Scan();
        if (lockerRoomCamera != null)
            lockerRoomCamera.SetPosition("default");
        if (lockerRoomPlayer != null)
        {
            lockerRoomPlayer.AllowRotation = false;
            lockerRoomPlayer.SetRotationFromPreset("front");
        }
    }

    // ==================== SCANNING ====================

    public static void Scan()
    {
        if (!IsInMainMenu()) return;

        if (lockerRoomStick == null)
        {
            var sticks = Object.FindObjectsByType<LockerRoomStick>(FindObjectsSortMode.None);
            foreach (var s in sticks)
            {
                if (!PartyLineup.IsPartyStickClone(s))
                {
                    lockerRoomStick = s;
                    break;
                }
            }
        }

        if (lockerRoomCamera == null)
        {
            var cameras = Object.FindObjectsByType<LockerRoomCamera>(FindObjectsSortMode.None);
            if (cameras.Length > 0) lockerRoomCamera = cameras[0];
        }

        if (lockerRoomPlayer == null)
        {
            var players = Object.FindObjectsByType<LockerRoomPlayer>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (!PartyLineup.IsPartyPlayerClone(p))
                {
                    lockerRoomPlayer = p;
                    break;
                }
            }
        }
    }

    // ==================== HARMONY PATCHES ====================

    // Patch LockerRoomStick.SetSkinID to apply our custom stick skin
    [HarmonyPatch(typeof(LockerRoomStick), nameof(LockerRoomStick.SetSkinID))]
    public static class LockerRoomStickSetSkinIDPatch
    {
        [HarmonyPostfix]
        public static void Postfix(LockerRoomStick __instance, int skinID, PlayerTeam team, PlayerRole role)
        {
            if (PartyLineup.IsPartyStickClone(__instance)) return;
            try
            {
                lockerRoomStick = __instance;

                StickMesh stickMesh = role == PlayerRole.Goalie
                    ? (StickMesh)_goalieStickMeshField.GetValue(__instance)
                    : (StickMesh)_attackerStickMeshField.GetValue(__instance);

                if (stickMesh == null) return;

                ReskinRegistry.ReskinEntry reskinEntry = null;
                switch (team)
                {
                    case PlayerTeam.Blue:
                        reskinEntry = role == PlayerRole.Attacker
                            ? ReskinProfileManager.currentProfile.stickAttackerBluePersonal
                            : ReskinProfileManager.currentProfile.stickGoalieBluePersonal;
                        break;
                    case PlayerTeam.Red:
                        reskinEntry = role == PlayerRole.Attacker
                            ? ReskinProfileManager.currentProfile.stickAttackerRedPersonal
                            : ReskinProfileManager.currentProfile.stickGoalieRedPersonal;
                        break;
                }

                if (reskinEntry != null)
                {
                    StickSwapper.SetStickMeshTexture(stickMesh, reskinEntry, role);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error in LockerRoomStickSetSkinIDPatch: {e}");
            }
        }
    }

    // ==================== LEGACY COMPAT (called from Plugin.OnEnable) ====================

    /// <summary>
    /// Called on initial load to apply customizations to the already-loaded locker room.
    /// </summary>
    public static void ApplyInitialCustomizations()
    {
        if (!IsInMainMenu()) return;
        Scan();

        var team = SettingsManager.Team;
        var role = SettingsManager.Role;

        ForceModelRefresh(team, role);
        ApplyCustomizationsAfterGameUpdate(team, role);
    }
}
