// ReskinProfileManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ToasterReskinLoader.presets;
using ToasterReskinLoader.swappers;
using UnityEngine;

namespace ToasterReskinLoader;

public static class ReskinProfileManager
{
    // TODO make this inside of a dictionary or profile setting or something
    public static Profile currentProfile { get; private set; } = new Profile();

    private static string ProfilePath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "reskinprofiles", "ReskinProfile.json");

    public static void SetSelectedReskinInCurrentProfile(ReskinRegistry.ReskinEntry reskinEntry, string type, string slot)
    {
        if (type == "stick_attacker")
        {
            switch (slot)
            {
                case "blue_personal":
                    currentProfile.stickAttackerBluePersonal = reskinEntry;
                    SwapperManager.OnPersonalStickChanged();
                    break;
                case "red_personal":
                    currentProfile.stickAttackerRedPersonal = reskinEntry;
                    SwapperManager.OnPersonalStickChanged();
                    break;
                case "blue_team":
                    currentProfile.stickAttackerBlue = reskinEntry;
                    SwapperManager.OnBlueTeamStickChanged();
                    break;
                case "red_team":
                    currentProfile.stickAttackerRed = reskinEntry;
                    SwapperManager.OnRedTeamStickChanged();
                    break;
            }
        } 
        else if (type == "stick_goalie")
        {
            switch (slot)
            {
                case "blue_personal":
                    currentProfile.stickGoalieBluePersonal = reskinEntry;
                    SwapperManager.OnPersonalStickChanged();
                    break;
                case "red_personal":
                    currentProfile.stickGoalieRedPersonal = reskinEntry;
                    SwapperManager.OnPersonalStickChanged();
                    break;
                case "blue_team":
                    currentProfile.stickGoalieBlue = reskinEntry;
                    SwapperManager.OnBlueTeamStickChanged();
                    break;
                case "red_team":
                    currentProfile.stickGoalieRed = reskinEntry;
                    SwapperManager.OnRedTeamStickChanged();
                    break;
            }
        } else if (type == "jersey_torso")
        {
            switch (slot)
            {
                case "blue_skater":
                    currentProfile.blueSkaterTorso = reskinEntry;
                    SwapperManager.OnBlueJerseyChanged();
                    break;
                case "red_skater":
                    currentProfile.redSkaterTorso = reskinEntry;
                    SwapperManager.OnRedJerseyChanged();
                    break;
                case "blue_goalie":
                    currentProfile.blueGoalieTorso = reskinEntry;
                    SwapperManager.OnBlueJerseyChanged();
                    break;
                case "red_goalie":
                    currentProfile.redGoalieTorso = reskinEntry;
                    SwapperManager.OnRedJerseyChanged();
                    break;
            }
        } else if (type == "jersey_groin")
        {
            switch (slot)
            {
                case "blue_skater":
                    currentProfile.blueSkaterGroin = reskinEntry;
                    SwapperManager.OnBlueJerseyChanged();
                    break;
                case "red_skater":
                    currentProfile.redSkaterGroin = reskinEntry;
                    SwapperManager.OnRedJerseyChanged();
                    break;
                case "blue_goalie":
                    currentProfile.blueGoalieGroin = reskinEntry;
                    SwapperManager.OnBlueJerseyChanged();
                    break;
                case "red_goalie":
                    currentProfile.redGoalieGroin = reskinEntry;
                    SwapperManager.OnRedJerseyChanged();
                    break;
            }
        }   
        else if (type == "legpad")
        {
            switch (slot)
            {
               case "blue_left":
                   currentProfile.blueLegPadLeft = reskinEntry;
                   GoalieEquipmentSwapper.OnBlueLegPadsChanged();
                   break;
               case "blue_right":
                   currentProfile.blueLegPadRight = reskinEntry;
                   GoalieEquipmentSwapper.OnBlueLegPadsChanged();
                   break;
               case "red_left":
                   currentProfile.redLegPadLeft = reskinEntry;
                   GoalieEquipmentSwapper.OnRedLegPadsChanged();
                   break;
               case "red_right":
                   currentProfile.redLegPadRight = reskinEntry;
                   GoalieEquipmentSwapper.OnRedLegPadsChanged();
                   break;
            }
        }
        else if (type == "helmet")
        {
            switch (slot)
            {
                case "goalie_blue":
                    currentProfile.blueGoalieHelmet = reskinEntry;
                    GoalieHelmetSwapper.OnBlueHelmetsChanged();
                    break;
                case "goalie_red":
                    currentProfile.redGoalieHelmet = reskinEntry;
                    GoalieHelmetSwapper.OnRedHelmetsChanged();
                    break;
                case "skater_blue":
                    currentProfile.blueSkaterHelmet = reskinEntry;
                    swappers.SkaterHelmetSwapper.OnBlueHelmetsChanged();
                    break;
                case "skater_red":
                    currentProfile.redSkaterHelmet = reskinEntry;
                    swappers.SkaterHelmetSwapper.OnRedHelmetsChanged();
                    break;
            }
        }
        else if (type == "goalie_mask")
        {
            switch (slot)
            {
                case "blue":
                    currentProfile.blueGoalieMask = reskinEntry;
                    GoalieHelmetSwapper.OnBlueMasksChanged();
                    break;
                case "red":
                    currentProfile.redGoalieMask = reskinEntry;
                    GoalieHelmetSwapper.OnRedMasksChanged();
                    break;
            }
        }
        else if (type == "rink_ice")
        {
            // We aren't using slot here
            currentProfile.ice = reskinEntry;
            IceSwapper.SetIceTexture();
        }
        else if (type == "puck")
        {
            currentProfile.puck = reskinEntry;
            PuckSwapper.SetAllPucksTextures();
        } else if (type == "net")
        {
            currentProfile.net = reskinEntry;
            ArenaSwapper.SetNetTexture();
        }
        
        SaveProfile();
    }

    /// <summary>
    /// Adds a puck to the randomizer list.
    /// </summary>
    public static void AddPuckToRandomizer(ReskinRegistry.ReskinEntry puck)
    {
        if (puck == null) return;

        // Avoid duplicates
        if (!currentProfile.puckList.Any(p => p.Name == puck.Name && p.ParentPack?.UniqueId == puck.ParentPack?.UniqueId))
        {
            currentProfile.puckList.Add(puck);
            SaveProfile();
            PuckSwapper.SetAllPucksTextures();
        }
    }

    /// <summary>
    /// Removes a puck from the randomizer list.
    /// </summary>
    public static void RemovePuckFromRandomizer(ReskinRegistry.ReskinEntry puck)
    {
        if (puck == null) return;

        var toRemove = currentProfile.puckList.FirstOrDefault(p =>
            p.Name == puck.Name && p.ParentPack?.UniqueId == puck.ParentPack?.UniqueId);

        if (toRemove != null)
        {
            currentProfile.puckList.Remove(toRemove);
            SaveProfile();
            PuckSwapper.SetAllPucksTextures();
        }
    }

    /// <summary>
    /// Checks if a puck is in the randomizer list.
    /// </summary>
    public static bool IsPuckInRandomizer(ReskinRegistry.ReskinEntry puck)
    {
        if (puck == null) return false;

        return currentProfile.puckList.Any(p =>
            p.Name == puck.Name && p.ParentPack?.UniqueId == puck.ParentPack?.UniqueId);
    }

    public static void LoadProfile()
    {
        string profilesFolder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "reskinprofiles");
        if (!Directory.Exists(profilesFolder))
        {
            Plugin.LogError($"Local reskin profiles folder not found: {profilesFolder}, creating it...");
            Directory.CreateDirectory(profilesFolder);
        }
        
        if (!File.Exists(ProfilePath))
        {
            Plugin.Log("No reskin profile found. Creating a default profile.");
            currentProfile = new Profile();
            SaveProfile(); // Save the new default profile
            return;
        }
        
        // Create a new profile instance that holds all the correct default values.
        // This is our safety net.
        var defaultProfile = new Profile();
        
        try
        {
            Plugin.Log($"Loading reskin profile from: {ProfilePath}");
            string json = File.ReadAllText(ProfilePath);
            var serializableProfile =
                JsonConvert.DeserializeObject<SerializableProfile>(json);

            // If the JSON file is empty or invalid, serializableProfile could be null.
            if (serializableProfile == null)
            {
                Plugin.LogError(
                    "Failed to deserialize profile (file might be empty or corrupt). Loading default profile."
                );
                currentProfile = defaultProfile;
                return;
            }

            Plugin.Log($"Deserialized {ProfilePath}, loading values...");
            // "Hydrate" the profile: convert references back to live ReskinEntry objects
            currentProfile = new Profile
            {
                // Sticks
                stickAttackerBluePersonal = FindEntryFromReference(serializableProfile?.StickAttackerBluePersonalRef, "stick_attacker"),
                stickAttackerRedPersonal = FindEntryFromReference(serializableProfile?.StickAttackerRedPersonalRef, "stick_attacker"),
                stickAttackerBlue = FindEntryFromReference(serializableProfile?.StickAttackerBlueRef, "stick_attacker"),
                stickAttackerRed = FindEntryFromReference(serializableProfile?.StickAttackerRedRef, "stick_attacker"),
                stickGoalieBluePersonal = FindEntryFromReference(serializableProfile?.StickGoalieBluePersonalRef, "stick_goalie"),
                stickGoalieRedPersonal = FindEntryFromReference(serializableProfile?.StickGoalieRedPersonalRef, "stick_goalie"),
                stickGoalieBlue = FindEntryFromReference(serializableProfile?.StickGoalieBlueRef, "stick_goalie"),
                stickGoalieRed = FindEntryFromReference(serializableProfile?.StickGoalieRedRef, "stick_goalie"),
                
                // Jerseys
                blueSkaterTorso = FindEntryFromReference(serializableProfile?.BlueSkaterTorsoRef, "jersey_torso"),
                blueSkaterGroin = FindEntryFromReference(serializableProfile?.BlueSkaterGroinRef, "jersey_groin"),
                blueGoalieTorso = FindEntryFromReference(serializableProfile?.BlueGoalieTorsoRef, "jersey_torso"),
                blueGoalieGroin = FindEntryFromReference(serializableProfile?.BlueGoalieGroinRef, "jersey_groin"),
                redSkaterTorso = FindEntryFromReference(serializableProfile?.RedSkaterTorsoRef, "jersey_torso"),
                redSkaterGroin = FindEntryFromReference(serializableProfile?.RedSkaterGroinRef, "jersey_groin"),
                redGoalieTorso = FindEntryFromReference(serializableProfile?.RedGoalieTorsoRef, "jersey_torso"),
                redGoalieGroin = FindEntryFromReference(serializableProfile?.RedGoalieGroinRef, "jersey_groin"),
                
                blueLegPadLeft = FindEntryFromReference(serializableProfile?.BlueLegPadLeftRef, "legpad"),
                blueLegPadRight = FindEntryFromReference(serializableProfile?.BlueLegPadRightRef, "legpad"),
                redLegPadLeft = FindEntryFromReference(serializableProfile?.RedLegPadLeftRef, "legpad"),
                redLegPadRight = FindEntryFromReference(serializableProfile?.RedLegPadRightRef, "legpad"),
                blueLegPadDefaultColor = serializableProfile.BlueLegPadDefaultColor != null
                    ? (Color)serializableProfile.BlueLegPadDefaultColor
                    : defaultProfile.blueLegPadDefaultColor,
                redLegPadDefaultColor = serializableProfile.RedLegPadDefaultColor != null
                    ? (Color)serializableProfile.RedLegPadDefaultColor
                    : defaultProfile.redLegPadDefaultColor,
                blueGoalieHelmet = FindEntryFromReference(serializableProfile?.BlueGoalieHelmetRef, "helmet"),
                redGoalieHelmet = FindEntryFromReference(serializableProfile?.RedGoalieHelmetRef, "helmet"),
                blueGoalieHelmetColor = serializableProfile.BlueGoalieHelmetColor != null
                    ? (Color)serializableProfile.BlueGoalieHelmetColor
                    : defaultProfile.blueGoalieHelmetColor,
                redGoalieHelmetColor = serializableProfile.RedGoalieHelmetColor != null
                    ? (Color)serializableProfile.RedGoalieHelmetColor
                    : defaultProfile.redGoalieHelmetColor,
                blueGoalieMask = FindEntryFromReference(serializableProfile?.BlueGoalieMaskRef, "goalie_mask"),
                redGoalieMask = FindEntryFromReference(serializableProfile?.RedGoalieMaskRef, "goalie_mask"),
                blueGoalieMaskColor = serializableProfile.BlueGoalieMaskColor != null
                    ? (Color)serializableProfile.BlueGoalieMaskColor
                    : defaultProfile.blueGoalieMaskColor,
                redGoalieMaskColor = serializableProfile.RedGoalieMaskColor != null
                    ? (Color)serializableProfile.RedGoalieMaskColor
                    : defaultProfile.redGoalieMaskColor,
                blueGoalieCageColor = serializableProfile.BlueGoalieCageColor != null
                    ? (Color)serializableProfile.BlueGoalieCageColor
                    : defaultProfile.blueGoalieCageColor,
                redGoalieCageColor = serializableProfile.RedGoalieCageColor != null
                    ? (Color)serializableProfile.RedGoalieCageColor
                    : defaultProfile.redGoalieCageColor,
                blueSkaterHelmet = FindEntryFromReference(serializableProfile?.BlueSkaterHelmetRef, "helmet"),
                redSkaterHelmet = FindEntryFromReference(serializableProfile?.RedSkaterHelmetRef, "helmet"),
                blueSkaterHelmetColor = serializableProfile.BlueSkaterHelmetColor != null
                    ? (Color)serializableProfile.BlueSkaterHelmetColor
                    : defaultProfile.blueSkaterHelmetColor,
                redSkaterHelmetColor = serializableProfile.RedSkaterHelmetColor != null
                    ? (Color)serializableProfile.RedSkaterHelmetColor
                    : defaultProfile.redSkaterHelmetColor,
                blueSkaterLetteringColor = serializableProfile.BlueSkaterLetteringColor != null
                    ? (Color)serializableProfile.BlueSkaterLetteringColor
                    : defaultProfile.blueSkaterLetteringColor,
                redSkaterLetteringColor = serializableProfile.RedSkaterLetteringColor != null
                    ? (Color)serializableProfile.RedSkaterLetteringColor
                    : defaultProfile.redSkaterLetteringColor,
                blueGoalieLetteringColor = serializableProfile.BlueGoalieLetteringColor != null
                    ? (Color)serializableProfile.BlueGoalieLetteringColor
                    : defaultProfile.blueGoalieLetteringColor,
                redGoalieLetteringColor = serializableProfile.RedGoalieLetteringColor != null
                    ? (Color)serializableProfile.RedGoalieLetteringColor
                    : defaultProfile.redGoalieLetteringColor,
                blueSkaterNumberOutlineColor = serializableProfile.BlueSkaterNumberOutlineColor != null
                    ? (Color)serializableProfile.BlueSkaterNumberOutlineColor
                    : defaultProfile.blueSkaterNumberOutlineColor,
                redSkaterNumberOutlineColor = serializableProfile.RedSkaterNumberOutlineColor != null
                    ? (Color)serializableProfile.RedSkaterNumberOutlineColor
                    : defaultProfile.redSkaterNumberOutlineColor,
                blueGoalieNumberOutlineColor = serializableProfile.BlueGoalieNumberOutlineColor != null
                    ? (Color)serializableProfile.BlueGoalieNumberOutlineColor
                    : defaultProfile.blueGoalieNumberOutlineColor,
                redGoalieNumberOutlineColor = serializableProfile.RedGoalieNumberOutlineColor != null
                    ? (Color)serializableProfile.RedGoalieNumberOutlineColor
                    : defaultProfile.redGoalieNumberOutlineColor,
                blueSkaterNumberOutlineWidth = serializableProfile.BlueSkaterNumberOutlineWidth
                    ?? defaultProfile.blueSkaterNumberOutlineWidth,
                redSkaterNumberOutlineWidth = serializableProfile.RedSkaterNumberOutlineWidth
                    ?? defaultProfile.redSkaterNumberOutlineWidth,
                blueGoalieNumberOutlineWidth = serializableProfile.BlueGoalieNumberOutlineWidth
                    ?? defaultProfile.blueGoalieNumberOutlineWidth,
                redGoalieNumberOutlineWidth = serializableProfile.RedGoalieNumberOutlineWidth
                    ?? defaultProfile.redGoalieNumberOutlineWidth,
                // Puck
                puck = FindEntryFromReference(serializableProfile?.PuckRef, "puck"),
                puckList = LoadPuckList(serializableProfile),

                // Arena
                // Use the ?? (null-coalescing) operator. If the loaded value is null, use the default.
                fullArenaEnabled = serializableProfile.FullArenaEnabled 
                    ?? defaultProfile.fullArenaEnabled,
                fullArenaBundle = serializableProfile.FullArenaBundle 
                    ?? defaultProfile.fullArenaBundle,
                fullArenaPrefab = serializableProfile.FullArenaPrefab 
                    ?? defaultProfile.fullArenaPrefab,
                fullArenaWorkshopId = serializableProfile.FullArenaWorkshopId 
                    ?? defaultProfile.fullArenaWorkshopId,
                crowdEnabled = serializableProfile.CrowdEnabled
                    ?? defaultProfile.crowdEnabled,
                hangarEnabled = serializableProfile.HangarEnabled
                    ?? defaultProfile.hangarEnabled,
                glassEnabled = serializableProfile.GlassEnabled
                                ?? defaultProfile.glassEnabled,
                scoreboardEnabled = serializableProfile.ScoreboardEnabled
                                ?? defaultProfile.scoreboardEnabled,
                ice = FindEntryFromReference(serializableProfile.IceRef, "rink_ice"),
                iceSmoothness = serializableProfile.IceSmoothness
                    ?? defaultProfile.iceSmoothness,

                // For colors, we check if the SerializableColor object is null.
                boardsBorderTopColor =
                    serializableProfile.BoardsBorderTopColor != null
                        ? (Color)serializableProfile.BoardsBorderTopColor
                        : defaultProfile.boardsBorderTopColor,
                boardsMiddleColor =
                    serializableProfile.BoardsMiddleColor != null
                        ? (Color)serializableProfile.BoardsMiddleColor
                        : defaultProfile.boardsMiddleColor,
                boardsBorderBottomColor =
                    serializableProfile.BoardsBorderBottomColor != null
                        ? (Color)serializableProfile.BoardsBorderBottomColor
                        : defaultProfile.boardsBorderBottomColor,
                glassSmoothness = serializableProfile.GlassSmoothness ?? defaultProfile.glassSmoothness,
                pillarsColor = serializableProfile.PillarsColor != null
                    ? (Color)serializableProfile.PillarsColor
                    : defaultProfile.pillarsColor,
                spectatorDensity =  serializableProfile.SpectatorDensity ?? defaultProfile.spectatorDensity,
                net = FindEntryFromReference(serializableProfile?.NetRef, "net"),

                // Stick Tape
                blueSkaterBladeTapeMode = serializableProfile.BlueSkaterBladeTapeMode ?? defaultProfile.blueSkaterBladeTapeMode,
                blueSkaterBladeTape = FindEntryFromReference(serializableProfile?.BlueSkaterBladeTapeRef, "tape_attacker_blade"),
                blueSkaterBladeTapeColor = serializableProfile.BlueSkaterBladeTapeColor != null
                    ? (Color)serializableProfile.BlueSkaterBladeTapeColor
                    : defaultProfile.blueSkaterBladeTapeColor,

                blueSkaterShaftTapeMode = serializableProfile.BlueSkaterShaftTapeMode ?? defaultProfile.blueSkaterShaftTapeMode,
                blueSkaterShaftTape = FindEntryFromReference(serializableProfile?.BlueSkaterShaftTapeRef, "tape_attacker_shaft"),
                blueSkaterShaftTapeColor = serializableProfile.BlueSkaterShaftTapeColor != null
                    ? (Color)serializableProfile.BlueSkaterShaftTapeColor
                    : defaultProfile.blueSkaterShaftTapeColor,

                blueGoalieBladeTapeMode = serializableProfile.BlueGoalieBladeTapeMode ?? defaultProfile.blueGoalieBladeTapeMode,
                blueGoalieBladeTape = FindEntryFromReference(serializableProfile?.BlueGoalieBladeTapeRef, "tape_goalie_blade"),
                blueGoalieBladeTapeColor = serializableProfile.BlueGoalieBladeTapeColor != null
                    ? (Color)serializableProfile.BlueGoalieBladeTapeColor
                    : defaultProfile.blueGoalieBladeTapeColor,

                blueGoalieShaftTapeMode = serializableProfile.BlueGoalieShaftTapeMode ?? defaultProfile.blueGoalieShaftTapeMode,
                blueGoalieShaftTape = FindEntryFromReference(serializableProfile?.BlueGoalieShaftTapeRef, "tape_goalie_shaft"),
                blueGoalieShaftTapeColor = serializableProfile.BlueGoalieShaftTapeColor != null
                    ? (Color)serializableProfile.BlueGoalieShaftTapeColor
                    : defaultProfile.blueGoalieShaftTapeColor,

                redSkaterBladeTapeMode = serializableProfile.RedSkaterBladeTapeMode ?? defaultProfile.redSkaterBladeTapeMode,
                redSkaterBladeTape = FindEntryFromReference(serializableProfile?.RedSkaterBladeTapeRef, "tape_attacker_blade"),
                redSkaterBladeTapeColor = serializableProfile.RedSkaterBladeTapeColor != null
                    ? (Color)serializableProfile.RedSkaterBladeTapeColor
                    : defaultProfile.redSkaterBladeTapeColor,

                redSkaterShaftTapeMode = serializableProfile.RedSkaterShaftTapeMode ?? defaultProfile.redSkaterShaftTapeMode,
                redSkaterShaftTape = FindEntryFromReference(serializableProfile?.RedSkaterShaftTapeRef, "tape_attacker_shaft"),
                redSkaterShaftTapeColor = serializableProfile.RedSkaterShaftTapeColor != null
                    ? (Color)serializableProfile.RedSkaterShaftTapeColor
                    : defaultProfile.redSkaterShaftTapeColor,

                redGoalieBladeTapeMode = serializableProfile.RedGoalieBladeTapeMode ?? defaultProfile.redGoalieBladeTapeMode,
                redGoalieBladeTape = FindEntryFromReference(serializableProfile?.RedGoalieBladeTapeRef, "tape_goalie_blade"),
                redGoalieBladeTapeColor = serializableProfile.RedGoalieBladeTapeColor != null
                    ? (Color)serializableProfile.RedGoalieBladeTapeColor
                    : defaultProfile.redGoalieBladeTapeColor,

                redGoalieShaftTapeMode = serializableProfile.RedGoalieShaftTapeMode ?? defaultProfile.redGoalieShaftTapeMode,
                redGoalieShaftTape = FindEntryFromReference(serializableProfile?.RedGoalieShaftTapeRef, "tape_goalie_shaft"),
                redGoalieShaftTapeColor = serializableProfile.RedGoalieShaftTapeColor != null
                    ? (Color)serializableProfile.RedGoalieShaftTapeColor
                    : defaultProfile.redGoalieShaftTapeColor,

                // Team Colors
                teamColorsEnabled = serializableProfile.TeamColorsEnabled
                    ?? defaultProfile.teamColorsEnabled,
                blueTeamColor = serializableProfile.BlueTeamColor != null
                    ? (Color)serializableProfile.BlueTeamColor
                    : defaultProfile.blueTeamColor,
                redTeamColor = serializableProfile.RedTeamColor != null
                    ? (Color)serializableProfile.RedTeamColor
                    : defaultProfile.redTeamColor,
                teamIndicatorEnabled = serializableProfile.TeamIndicatorEnabled
                    ?? defaultProfile.teamIndicatorEnabled,
                blueTeamName = serializableProfile.BlueTeamName
                    ?? defaultProfile.blueTeamName,
                redTeamName = serializableProfile.RedTeamName
                    ?? defaultProfile.redTeamName,

                // Minimap
                blueMinimapNumberColor = serializableProfile.BlueMinimapNumberColor != null
                    ? (Color)serializableProfile.BlueMinimapNumberColor
                    : defaultProfile.blueMinimapNumberColor,
                redMinimapNumberColor = serializableProfile.RedMinimapNumberColor != null
                    ? (Color)serializableProfile.RedMinimapNumberColor
                    : defaultProfile.redMinimapNumberColor,
                minimapPuckColor = serializableProfile.MinimapPuckColor != null
                    ? (Color)serializableProfile.MinimapPuckColor
                    : defaultProfile.minimapPuckColor,
                minimapPlayerScale = serializableProfile.MinimapPlayerScale
                    ?? defaultProfile.minimapPlayerScale,
                minimapPuckScale = serializableProfile.MinimapPuckScale
                    ?? defaultProfile.minimapPuckScale,
                minimapRefreshRate = serializableProfile.MinimapRefreshRate
                    ?? defaultProfile.minimapRefreshRate,
                localPlayerMinimapIconEnabled = serializableProfile.LocalPlayerMinimapIconEnabled
                    ?? defaultProfile.localPlayerMinimapIconEnabled,
                blueLocalPlayerMinimapIconColor = serializableProfile.BlueLocalPlayerMinimapIconColor != null
                    ? (Color)serializableProfile.BlueLocalPlayerMinimapIconColor
                    : defaultProfile.blueLocalPlayerMinimapIconColor,
                redLocalPlayerMinimapIconColor = serializableProfile.RedLocalPlayerMinimapIconColor != null
                    ? (Color)serializableProfile.RedLocalPlayerMinimapIconColor
                    : defaultProfile.redLocalPlayerMinimapIconColor,

                // Chat
                chatHeight = serializableProfile.ChatHeight
                    ?? defaultProfile.chatHeight,
                chatBackground = serializableProfile.ChatBackground
                    ?? defaultProfile.chatBackground,
                quickChatX = serializableProfile.QuickChatX
                    ?? defaultProfile.quickChatX,
                quickChatY = serializableProfile.QuickChatY
                    ?? defaultProfile.quickChatY,
                chatRenderAllEmojis = serializableProfile.ChatRenderAllEmojis
                    ?? defaultProfile.chatRenderAllEmojis,

                // Shadows (CrispyShadows)
                crispyShadowsEnabled = serializableProfile.CrispyShadowsEnabled
                    ?? defaultProfile.crispyShadowsEnabled,
                shadowResolution = serializableProfile.ShadowResolution
                    ?? defaultProfile.shadowResolution,
                shadowDistance = serializableProfile.ShadowDistance
                    ?? defaultProfile.shadowDistance,
                shadowCascadeCount = serializableProfile.ShadowCascadeCount
                    ?? defaultProfile.shadowCascadeCount,
                shadowSoftShadows = serializableProfile.ShadowSoftShadows
                    ?? defaultProfile.shadowSoftShadows,

                // Skybox
                skyboxAtmosphereThickness =
                    serializableProfile.SkyboxAtmosphereThickness
                    ?? defaultProfile.skyboxAtmosphereThickness,
                skyboxExposure = serializableProfile.SkyboxExposure
                    ?? defaultProfile.skyboxExposure,
                skyboxSunDisk = serializableProfile.SkyboxSunDisk
                    ?? defaultProfile.skyboxSunDisk,
                skyboxSunSize = serializableProfile.SkyboxSunSize
                    ?? defaultProfile.skyboxSunSize,
                skyboxSunSizeConvergence =
                    serializableProfile.SkyboxSunSizeConvergence
                    ?? defaultProfile.skyboxSunSizeConvergence,
                skyboxGroundColor =
                    serializableProfile.SkyboxGroundColor != null
                        ? (Color)serializableProfile.SkyboxGroundColor
                        : defaultProfile.skyboxGroundColor,
                skyboxSkyTint =
                    serializableProfile.SkyboxSkyTint != null
                        ? (Color)serializableProfile.SkyboxSkyTint
                        : defaultProfile.skyboxSkyTint,

                // Puck FX
                puckFXOutlineColor =
                    serializableProfile.PuckFXOutlineColor != null
                        ? (Color)serializableProfile.PuckFXOutlineColor
                        : defaultProfile.puckFXOutlineColor,
                puckFXOutlineKernelSize =
                    serializableProfile.PuckFXOutlineKernelSize
                    ?? defaultProfile.puckFXOutlineKernelSize,
                puckFXElevationIndicatorColor =
                    serializableProfile.PuckFXElevationIndicatorColor != null
                        ? (Color)serializableProfile.PuckFXElevationIndicatorColor
                        : defaultProfile.puckFXElevationIndicatorColor,
                puckFXVerticalityLineColor =
                    serializableProfile.PuckFXVerticalityLineColor != null
                        ? (Color)serializableProfile.PuckFXVerticalityLineColor
                        : defaultProfile.puckFXVerticalityLineColor,
                puckFXVerticalityLineStartAlpha =
                    serializableProfile.PuckFXVerticalityLineStartAlpha
                    ?? defaultProfile.puckFXVerticalityLineStartAlpha,
                puckFXVerticalityLineEndAlpha =
                    serializableProfile.PuckFXVerticalityLineEndAlpha
                    ?? defaultProfile.puckFXVerticalityLineEndAlpha,
                puckFXTrailEnabled =
                    serializableProfile.PuckFXTrailEnabled
                    ?? defaultProfile.puckFXTrailEnabled,
                puckFXTrailColor =
                    serializableProfile.PuckFXTrailColor != null
                        ? (Color)serializableProfile.PuckFXTrailColor
                        : defaultProfile.puckFXTrailColor,
                puckFXTrailStartWidth =
                    serializableProfile.PuckFXTrailStartWidth
                    ?? defaultProfile.puckFXTrailStartWidth,
                puckFXTrailEndWidth =
                    serializableProfile.PuckFXTrailEndWidth
                    ?? defaultProfile.puckFXTrailEndWidth,
                puckFXTrailLifetime =
                    serializableProfile.PuckFXTrailLifetime
                    ?? defaultProfile.puckFXTrailLifetime,
                puckFXTrailStartAlpha =
                    serializableProfile.PuckFXTrailStartAlpha
                    ?? defaultProfile.puckFXTrailStartAlpha,
                puckFXTrailEndAlpha =
                    serializableProfile.PuckFXTrailEndAlpha
                    ?? defaultProfile.puckFXTrailEndAlpha,
                puckFXSilhouetteColor = serializableProfile.PuckFXSilhouetteColor != null
                    ? (Color)serializableProfile.PuckFXSilhouetteColor
                    : defaultProfile.puckFXSilhouetteColor,

                // QoL lives in side-car files now; nothing to copy here.

                // glossiness
                glossRemoverEnabled = serializableProfile.GlossRemoverEnabled
                    ?? defaultProfile.glossRemoverEnabled,
                glossSmoothness = serializableProfile.GlossSmoothness
                    ?? defaultProfile.glossSmoothness,
                glossAffectSticks = serializableProfile.GlossAffectSticks
                    ?? defaultProfile.glossAffectSticks,
                glossAffectPlayers = serializableProfile.GlossAffectPlayers
                    ?? defaultProfile.glossAffectPlayers,
                glossAffectPucks = serializableProfile.GlossAffectPucks
                    ?? defaultProfile.glossAffectPucks,
            };

            Plugin.Log("Reskin profile loaded successfully.");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to load reskin profile: {ex.Message}. Creating a new default profile.");
            currentProfile = new Profile(); // Fallback to a default profile on error
        }
    }

    /// <summary>
    /// Loads the puck list from serializable profile, with backwards compatibility migration.
    /// If the new puckListRef exists, use it. Otherwise, migrate the old single puck entry.
    /// </summary>
    private static List<ReskinRegistry.ReskinEntry> LoadPuckList(SerializableProfile serializableProfile)
    {
        var puckList = new List<ReskinRegistry.ReskinEntry>();

        // If new puckListRef exists, load from it
        if (serializableProfile?.PuckListRef != null && serializableProfile.PuckListRef.Count > 0)
        {
            foreach (var puckRef in serializableProfile.PuckListRef)
            {
                var entry = FindEntryFromReference(puckRef, "puck");
                if (entry != null)
                {
                    puckList.Add(entry);
                }
            }
        }
        // Else if old single puck entry exists, migrate it to the list
        else if (serializableProfile?.PuckRef != null)
        {
            var oldPuck = FindEntryFromReference(serializableProfile.PuckRef, "puck");
            if (oldPuck != null)
            {
                puckList.Add(oldPuck);
                Plugin.Log("Migrated old single puck entry to new puck randomizer list");
            }
        }

        return puckList;
    }

    public static void SaveProfile()
    {
        try
        {
            // Convert the live profile into its serializable representation
            var serializableProfile = new SerializableProfile
            {
                // Sticks
                StickAttackerBluePersonalRef = CreateReferenceFromEntry(currentProfile.stickAttackerBluePersonal),
                StickAttackerRedPersonalRef = CreateReferenceFromEntry(currentProfile.stickAttackerRedPersonal),
                StickAttackerBlueRef = CreateReferenceFromEntry(currentProfile.stickAttackerBlue),
                StickAttackerRedRef = CreateReferenceFromEntry(currentProfile.stickAttackerRed),
                StickGoalieBluePersonalRef = CreateReferenceFromEntry(currentProfile.stickGoalieBluePersonal),
                StickGoalieRedPersonalRef = CreateReferenceFromEntry(currentProfile.stickGoalieRedPersonal),
                StickGoalieBlueRef = CreateReferenceFromEntry(currentProfile.stickGoalieBlue),
                StickGoalieRedRef = CreateReferenceFromEntry(currentProfile.stickGoalieRed),
                
                // Jerseys
                BlueSkaterTorsoRef = CreateReferenceFromEntry(currentProfile.blueSkaterTorso),
                BlueSkaterGroinRef = CreateReferenceFromEntry(currentProfile.blueSkaterGroin),
                BlueGoalieTorsoRef = CreateReferenceFromEntry(currentProfile.blueGoalieTorso),
                BlueGoalieGroinRef = CreateReferenceFromEntry(currentProfile.blueGoalieGroin),
                RedSkaterTorsoRef = CreateReferenceFromEntry(currentProfile.redSkaterTorso),
                RedSkaterGroinRef = CreateReferenceFromEntry(currentProfile.redSkaterGroin),
                RedGoalieTorsoRef = CreateReferenceFromEntry(currentProfile.redGoalieTorso),
                RedGoalieGroinRef = CreateReferenceFromEntry(currentProfile.redGoalieGroin),
                
                // Goalie pads and helmet
                BlueLegPadLeftRef = CreateReferenceFromEntry(currentProfile.blueLegPadLeft),
                BlueLegPadRightRef = CreateReferenceFromEntry(currentProfile.blueLegPadRight),
                RedLegPadLeftRef = CreateReferenceFromEntry(currentProfile.redLegPadLeft),
                RedLegPadRightRef = CreateReferenceFromEntry(currentProfile.redLegPadRight),
                BlueLegPadDefaultColor = new SerializableColor(currentProfile.blueLegPadDefaultColor),
                RedLegPadDefaultColor = new SerializableColor(currentProfile.redLegPadDefaultColor),
                BlueGoalieHelmetRef = CreateReferenceFromEntry(currentProfile.blueGoalieHelmet),
                RedGoalieHelmetRef = CreateReferenceFromEntry(currentProfile.redGoalieHelmet),
                BlueGoalieHelmetColor = new SerializableColor(currentProfile.blueGoalieHelmetColor),
                RedGoalieHelmetColor = new SerializableColor(currentProfile.redGoalieHelmetColor),
                BlueGoalieMaskRef = CreateReferenceFromEntry(currentProfile.blueGoalieMask),
                RedGoalieMaskRef = CreateReferenceFromEntry(currentProfile.redGoalieMask),
                BlueGoalieMaskColor = new SerializableColor(currentProfile.blueGoalieMaskColor),
                RedGoalieMaskColor = new SerializableColor(currentProfile.redGoalieMaskColor),
                BlueGoalieCageColor = new SerializableColor(currentProfile.blueGoalieCageColor),
                RedGoalieCageColor = new SerializableColor(currentProfile.redGoalieCageColor),
                BlueSkaterHelmetRef = CreateReferenceFromEntry(currentProfile.blueSkaterHelmet),
                RedSkaterHelmetRef = CreateReferenceFromEntry(currentProfile.redSkaterHelmet),
                BlueSkaterHelmetColor = new SerializableColor(currentProfile.blueSkaterHelmetColor),
                RedSkaterHelmetColor = new SerializableColor(currentProfile.redSkaterHelmetColor),
                BlueSkaterLetteringColor = new SerializableColor(currentProfile.blueSkaterLetteringColor),
                RedSkaterLetteringColor = new SerializableColor(currentProfile.redSkaterLetteringColor),
                BlueGoalieLetteringColor = new SerializableColor(currentProfile.blueGoalieLetteringColor),
                RedGoalieLetteringColor = new SerializableColor(currentProfile.redGoalieLetteringColor),
                BlueSkaterNumberOutlineColor = new SerializableColor(currentProfile.blueSkaterNumberOutlineColor),
                RedSkaterNumberOutlineColor = new SerializableColor(currentProfile.redSkaterNumberOutlineColor),
                BlueGoalieNumberOutlineColor = new SerializableColor(currentProfile.blueGoalieNumberOutlineColor),
                RedGoalieNumberOutlineColor = new SerializableColor(currentProfile.redGoalieNumberOutlineColor),
                BlueSkaterNumberOutlineWidth = currentProfile.blueSkaterNumberOutlineWidth,
                RedSkaterNumberOutlineWidth = currentProfile.redSkaterNumberOutlineWidth,
                BlueGoalieNumberOutlineWidth = currentProfile.blueGoalieNumberOutlineWidth,
                RedGoalieNumberOutlineWidth = currentProfile.redGoalieNumberOutlineWidth,

                // Puck
                PuckRef = CreateReferenceFromEntry(currentProfile.puck),
                PuckListRef = currentProfile.puckList.Select(p => CreateReferenceFromEntry(p)).ToList(),

                // Full arena
                FullArenaEnabled = currentProfile.fullArenaEnabled,
                FullArenaBundle = currentProfile.fullArenaBundle,
                FullArenaPrefab = currentProfile.fullArenaPrefab,
                FullArenaWorkshopId = currentProfile.fullArenaWorkshopId,
                
                // Default arena-specifics
                CrowdEnabled = currentProfile.crowdEnabled,
                HangarEnabled = currentProfile.hangarEnabled,
                ScoreboardEnabled = currentProfile.scoreboardEnabled,
                GlassEnabled = currentProfile.glassEnabled,
                IceRef = CreateReferenceFromEntry(currentProfile.ice),
                IceSmoothness = currentProfile.iceSmoothness,
                BoardsBorderTopColor = new SerializableColor(currentProfile.boardsBorderTopColor),
                BoardsMiddleColor = new SerializableColor(currentProfile.boardsMiddleColor),
                BoardsBorderBottomColor = new SerializableColor(currentProfile.boardsBorderBottomColor),
                GlassSmoothness = currentProfile.glassSmoothness,
                PillarsColor = new SerializableColor(currentProfile.pillarsColor),
                SpectatorDensity = currentProfile.spectatorDensity,
                NetRef = CreateReferenceFromEntry(currentProfile.net),

                // Stick Tape
                BlueSkaterBladeTapeMode = currentProfile.blueSkaterBladeTapeMode,
                BlueSkaterBladeTapeRef = CreateReferenceFromEntry(currentProfile.blueSkaterBladeTape),
                BlueSkaterBladeTapeColor = new SerializableColor(currentProfile.blueSkaterBladeTapeColor),

                BlueSkaterShaftTapeMode = currentProfile.blueSkaterShaftTapeMode,
                BlueSkaterShaftTapeRef = CreateReferenceFromEntry(currentProfile.blueSkaterShaftTape),
                BlueSkaterShaftTapeColor = new SerializableColor(currentProfile.blueSkaterShaftTapeColor),

                BlueGoalieBladeTapeMode = currentProfile.blueGoalieBladeTapeMode,
                BlueGoalieBladeTapeRef = CreateReferenceFromEntry(currentProfile.blueGoalieBladeTape),
                BlueGoalieBladeTapeColor = new SerializableColor(currentProfile.blueGoalieBladeTapeColor),

                BlueGoalieShaftTapeMode = currentProfile.blueGoalieShaftTapeMode,
                BlueGoalieShaftTapeRef = CreateReferenceFromEntry(currentProfile.blueGoalieShaftTape),
                BlueGoalieShaftTapeColor = new SerializableColor(currentProfile.blueGoalieShaftTapeColor),

                RedSkaterBladeTapeMode = currentProfile.redSkaterBladeTapeMode,
                RedSkaterBladeTapeRef = CreateReferenceFromEntry(currentProfile.redSkaterBladeTape),
                RedSkaterBladeTapeColor = new SerializableColor(currentProfile.redSkaterBladeTapeColor),

                RedSkaterShaftTapeMode = currentProfile.redSkaterShaftTapeMode,
                RedSkaterShaftTapeRef = CreateReferenceFromEntry(currentProfile.redSkaterShaftTape),
                RedSkaterShaftTapeColor = new SerializableColor(currentProfile.redSkaterShaftTapeColor),

                RedGoalieBladeTapeMode = currentProfile.redGoalieBladeTapeMode,
                RedGoalieBladeTapeRef = CreateReferenceFromEntry(currentProfile.redGoalieBladeTape),
                RedGoalieBladeTapeColor = new SerializableColor(currentProfile.redGoalieBladeTapeColor),

                RedGoalieShaftTapeMode = currentProfile.redGoalieShaftTapeMode,
                RedGoalieShaftTapeRef = CreateReferenceFromEntry(currentProfile.redGoalieShaftTape),
                RedGoalieShaftTapeColor = new SerializableColor(currentProfile.redGoalieShaftTapeColor),

                // Team Colors
                TeamColorsEnabled = currentProfile.teamColorsEnabled,
                BlueTeamColor = new SerializableColor(currentProfile.blueTeamColor),
                RedTeamColor = new SerializableColor(currentProfile.redTeamColor),
                TeamIndicatorEnabled = currentProfile.teamIndicatorEnabled,
                BlueTeamName = currentProfile.blueTeamName,
                RedTeamName = currentProfile.redTeamName,

                // Minimap
                BlueMinimapNumberColor = new SerializableColor(currentProfile.blueMinimapNumberColor),
                RedMinimapNumberColor = new SerializableColor(currentProfile.redMinimapNumberColor),
                MinimapPuckColor = new SerializableColor(currentProfile.minimapPuckColor),
                MinimapPlayerScale = currentProfile.minimapPlayerScale,
                MinimapPuckScale = currentProfile.minimapPuckScale,
                MinimapRefreshRate = currentProfile.minimapRefreshRate,
                LocalPlayerMinimapIconEnabled = currentProfile.localPlayerMinimapIconEnabled,
                BlueLocalPlayerMinimapIconColor = new SerializableColor(currentProfile.blueLocalPlayerMinimapIconColor),
                RedLocalPlayerMinimapIconColor = new SerializableColor(currentProfile.redLocalPlayerMinimapIconColor),

                // Chat
                ChatHeight = currentProfile.chatHeight,
                ChatBackground = currentProfile.chatBackground,
                QuickChatX = currentProfile.quickChatX,
                QuickChatY = currentProfile.quickChatY,
                ChatRenderAllEmojis = currentProfile.chatRenderAllEmojis,

                // Shadows (CrispyShadows)
                CrispyShadowsEnabled = currentProfile.crispyShadowsEnabled,
                ShadowResolution = currentProfile.shadowResolution,
                ShadowDistance = currentProfile.shadowDistance,
                ShadowCascadeCount = currentProfile.shadowCascadeCount,
                ShadowSoftShadows = currentProfile.shadowSoftShadows,

                // Skybox
                SkyboxAtmosphereThickness = currentProfile.skyboxAtmosphereThickness,
                SkyboxExposure = currentProfile.skyboxExposure,
                SkyboxSunDisk = currentProfile.skyboxSunDisk,
                SkyboxSunSize = currentProfile.skyboxSunSize,
                SkyboxSunSizeConvergence = currentProfile.skyboxSunSizeConvergence,
                SkyboxGroundColor = new SerializableColor(currentProfile.skyboxGroundColor),
                SkyboxSkyTint = new SerializableColor(currentProfile.skyboxSkyTint),

                // Puck FX
                PuckFXOutlineColor = new SerializableColor(currentProfile.puckFXOutlineColor),
                PuckFXOutlineKernelSize = currentProfile.puckFXOutlineKernelSize,
                PuckFXElevationIndicatorColor = new SerializableColor(currentProfile.puckFXElevationIndicatorColor),
                PuckFXVerticalityLineColor = new SerializableColor(currentProfile.puckFXVerticalityLineColor),
                PuckFXVerticalityLineStartAlpha = currentProfile.puckFXVerticalityLineStartAlpha,
                PuckFXVerticalityLineEndAlpha = currentProfile.puckFXVerticalityLineEndAlpha,
                PuckFXTrailEnabled = currentProfile.puckFXTrailEnabled,
                PuckFXTrailColor = new SerializableColor(currentProfile.puckFXTrailColor),
                PuckFXTrailStartWidth = currentProfile.puckFXTrailStartWidth,
                PuckFXTrailEndWidth = currentProfile.puckFXTrailEndWidth,
                PuckFXTrailLifetime = currentProfile.puckFXTrailLifetime,
                PuckFXTrailStartAlpha = currentProfile.puckFXTrailStartAlpha,
                PuckFXTrailEndAlpha = currentProfile.puckFXTrailEndAlpha,
                PuckFXSilhouetteColor = new SerializableColor(currentProfile.puckFXSilhouetteColor),

                // QoL is persisted by QoLStorage; do not re-include it
                // in the reskin profile so visual profiles stay shareable.

                // Glossiness
                GlossRemoverEnabled = currentProfile.glossRemoverEnabled,
                GlossSmoothness = currentProfile.glossSmoothness,
                GlossAffectSticks = currentProfile.glossAffectSticks,
                GlossAffectPlayers = currentProfile.glossAffectPlayers,
                GlossAffectPucks = currentProfile.glossAffectPucks,
            };

            string json = JsonConvert.SerializeObject(serializableProfile, Formatting.Indented);
            File.WriteAllText(ProfilePath, json);
            Plugin.LogDebug($"Reskin profile saved to: {ProfilePath}");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to save reskin profile: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Applies all settings from the CurrentProfile to the game via the SwapperManager.
    /// Call this after loading a profile or making a change.
    /// </summary>
    public static void LoadTexturesForActiveReskins()
    {
        // 1. Get a list of all reskins that are currently active in the profile.
        var activeReskins = GetAllActiveReskinEntries();

        // 2. Tell the TextureManager to unload anything not on this list.
        TextureManager.UnloadUnusedTextures(activeReskins);
        
        // This function centralizes applying the loaded settings
        // SwapperManager.OnPersonalStickChanged();
        // SwapperManager.OnBlueTeamStickChanged();
        // SwapperManager.OnRedTeamStickChanged();
        // IceSwapper.SetIceTexture();
        // PuckSwapper.SetAllPucksTextures();
        // Add calls for other swappers here...
        
        Plugin.Log($"Loading {activeReskins.Count} active reskins textures to memory...");
        foreach (ReskinRegistry.ReskinEntry reskinEntry in activeReskins)
        {
            TextureManager.GetTexture(reskinEntry);
        }

        PuckSwapper.GetBumpMapPathAndLoad();
        Plugin.Log($"Loaded {activeReskins.Count} active reskins textures to memory!");
    }
    
    /// <summary>
    /// A helper method to gather all non-null ReskinEntry objects from the current profile.
    /// </summary>
    /// <returns>A list of all active reskin entries.</returns>
    private static List<ReskinRegistry.ReskinEntry> GetAllActiveReskinEntries()
    {
        var activeList = new List<ReskinRegistry.ReskinEntry>();

        // Add each property from the profile to the list if it's not null
        if (currentProfile.stickAttackerBlue != null) activeList.Add(currentProfile.stickAttackerBlue);
        if (currentProfile.stickAttackerBluePersonal != null) activeList.Add(currentProfile.stickAttackerBluePersonal);
        if (currentProfile.stickAttackerRed != null) activeList.Add(currentProfile.stickAttackerRed);
        if (currentProfile.stickAttackerRedPersonal != null) activeList.Add(currentProfile.stickAttackerRedPersonal);
        if (currentProfile.stickGoalieBlue != null) activeList.Add(currentProfile.stickGoalieBlue);
        if (currentProfile.stickGoalieBluePersonal != null) activeList.Add(currentProfile.stickGoalieBluePersonal);
        if (currentProfile.stickGoalieRed != null) activeList.Add(currentProfile.stickGoalieRed);
        if (currentProfile.stickGoalieRedPersonal != null) activeList.Add(currentProfile.stickGoalieRedPersonal);
        if (currentProfile.ice != null) activeList.Add(currentProfile.ice);
        if (currentProfile.puck != null) activeList.Add(currentProfile.puck);
        // Add all pucks from puck randomizer list
        if (currentProfile.puckList != null)
        {
            foreach (var puck in currentProfile.puckList)
            {
                if (puck != null) activeList.Add(puck);
            }
        }
        if (currentProfile.net != null) activeList.Add(currentProfile.net);
        if (currentProfile.blueLegPadLeft != null) activeList.Add(currentProfile.blueLegPadLeft);
        if (currentProfile.blueLegPadRight != null) activeList.Add(currentProfile.blueLegPadRight);
        if (currentProfile.redLegPadLeft != null) activeList.Add(currentProfile.redLegPadLeft);
        if (currentProfile.redLegPadRight != null) activeList.Add(currentProfile.redLegPadRight);
        if (currentProfile.blueGoalieHelmet != null) activeList.Add(currentProfile.blueGoalieHelmet);
        if (currentProfile.redGoalieHelmet != null) activeList.Add(currentProfile.redGoalieHelmet);
        if (currentProfile.blueSkaterTorso != null) activeList.Add(currentProfile.blueSkaterTorso);
        if (currentProfile.blueSkaterGroin != null) activeList.Add(currentProfile.blueSkaterGroin);
        if (currentProfile.blueGoalieTorso != null) activeList.Add(currentProfile.blueGoalieTorso);
        if (currentProfile.blueGoalieGroin != null) activeList.Add(currentProfile.blueGoalieGroin);
        if (currentProfile.redSkaterTorso != null) activeList.Add(currentProfile.redSkaterTorso);
        if (currentProfile.redSkaterGroin != null) activeList.Add(currentProfile.redSkaterGroin);
        if (currentProfile.redGoalieTorso != null) activeList.Add(currentProfile.redGoalieTorso);
        if (currentProfile.redGoalieGroin != null) activeList.Add(currentProfile.redGoalieGroin);
        if (currentProfile.blueGoalieMask != null) activeList.Add(currentProfile.blueGoalieMask);
        if (currentProfile.redGoalieMask != null) activeList.Add(currentProfile.redGoalieMask);
        if (currentProfile.blueSkaterHelmet != null) activeList.Add(currentProfile.blueSkaterHelmet);
        if (currentProfile.redSkaterHelmet != null) activeList.Add(currentProfile.redSkaterHelmet);
        if (currentProfile.blueSkaterBladeTape != null) activeList.Add(currentProfile.blueSkaterBladeTape);
        if (currentProfile.blueSkaterShaftTape != null) activeList.Add(currentProfile.blueSkaterShaftTape);
        if (currentProfile.blueGoalieBladeTape != null) activeList.Add(currentProfile.blueGoalieBladeTape);
        if (currentProfile.blueGoalieShaftTape != null) activeList.Add(currentProfile.blueGoalieShaftTape);
        if (currentProfile.redSkaterBladeTape != null) activeList.Add(currentProfile.redSkaterBladeTape);
        if (currentProfile.redSkaterShaftTape != null) activeList.Add(currentProfile.redSkaterShaftTape);
        if (currentProfile.redGoalieBladeTape != null) activeList.Add(currentProfile.redGoalieBladeTape);
        if (currentProfile.redGoalieShaftTape != null) activeList.Add(currentProfile.redGoalieShaftTape);

        return activeList;
    }
    
    /// <summary>
    /// Finds a live ReskinEntry from the registry based on a reference.
    /// Returns null if the pack or entry is no longer installed.
    /// </summary>
    private static ReskinRegistry.ReskinEntry FindEntryFromReference(ReskinReference reference, string type)
    {
        if (reference == null || string.IsNullOrEmpty(reference.PackId))
        {
            return null; // No reference to find
        }

        // Find the pack with the matching UniqueId
        var pack = ReskinRegistry.reskinPacks.FirstOrDefault(p => p.UniqueId == reference.PackId);
        if (pack == null)
        {
            string missingPackInfo = $"Could not find reskin pack with ID '{reference.PackId}' for entry '{reference.EntryName}'. The pack may be uninstalled.";
            if (reference.WorkshopId == 0)
            {
                missingPackInfo += " This was a local pack.";
            }
            else
            {
                // You can now provide a direct link to the workshop item!
                missingPackInfo += $" Workshop Link: https://steamcommunity.com/sharedfiles/filedetails/?id={reference.WorkshopId}";
            }
            Plugin.LogWarning(missingPackInfo); return null; // Pack not found
        }

        // Find the entry within that pack with the matching name
        // TODO the pack reference not saving reskinType with the ref is causing problems looking up
        var entry = pack.Reskins.FirstOrDefault(e => e.Name == reference.EntryName && e.Type == type);

        if (entry == null)
        {
            Plugin.LogWarning($"Could not find reskin entry named '{reference.EntryName}' in pack '{pack.Name}'. The entry may have been removed from the pack.");
            return null; // Entry not found in pack
        }

        return entry;
    }
    
    /// <summary>
    /// Creates a serializable ReskinReference from a live ReskinEntry.
    /// </summary>
    private static ReskinReference CreateReferenceFromEntry(ReskinRegistry.ReskinEntry entry)
    {
        if (entry?.ParentPack == null)
        {
            return null; // Cannot create a reference for a null entry or an entry without a parent pack
        }

        return new ReskinReference
        {
            PackId = entry.ParentPack.UniqueId,
            EntryName = entry.Name,
            WorkshopId = entry.ParentPack.WorkshopId,
        };
    }
    
    /// <summary>
    /// Resets only the skybox-related properties of the current profile
    /// to their default values without affecting other settings like sticks or ice.
    /// </summary>
    public static void ResetSkyboxToDefault()
    {
        Plugin.Log("Resetting skybox settings to their default values.");

        // Create a temporary new profile just to access its default values.
        var defaultValues = new Profile();
        
        // Apply the default skybox values to the current profile.
        currentProfile.skyboxAtmosphereThickness = defaultValues.skyboxAtmosphereThickness;
        currentProfile.skyboxExposure = defaultValues.skyboxExposure;
        currentProfile.skyboxSunDisk = defaultValues.skyboxSunDisk;
        currentProfile.skyboxSunSize = defaultValues.skyboxSunSize;
        currentProfile.skyboxSunSizeConvergence = defaultValues.skyboxSunSizeConvergence;
        currentProfile.skyboxGroundColor = defaultValues.skyboxGroundColor;
        currentProfile.skyboxSkyTint = defaultValues.skyboxSkyTint;

        // Save the profile with the updated skybox values.
        SaveProfile();

        // Apply the changes to the game world.
        swappers.SkyboxSwapper.UpdateSkybox();
    }

    /// <summary>
    /// Resets only the Puck FX-related properties of the current profile
    /// to their default values and saves the profile.
    /// </summary>
    public static void ResetPuckFXToDefault()
    {
        Plugin.Log("Resetting Puck FX settings to their default values.");

        var defaultValues = new Profile();

        currentProfile.puckFXOutlineColor = defaultValues.puckFXOutlineColor;
        currentProfile.puckFXOutlineKernelSize = defaultValues.puckFXOutlineKernelSize;
        currentProfile.puckFXElevationIndicatorColor = defaultValues.puckFXElevationIndicatorColor;
        currentProfile.puckFXVerticalityLineColor = defaultValues.puckFXVerticalityLineColor;
        currentProfile.puckFXVerticalityLineStartAlpha = defaultValues.puckFXVerticalityLineStartAlpha;
        currentProfile.puckFXVerticalityLineEndAlpha = defaultValues.puckFXVerticalityLineEndAlpha;
        currentProfile.puckFXTrailEnabled = defaultValues.puckFXTrailEnabled;
        currentProfile.puckFXTrailColor = defaultValues.puckFXTrailColor;
        currentProfile.puckFXTrailStartWidth = defaultValues.puckFXTrailStartWidth;
        currentProfile.puckFXTrailEndWidth = defaultValues.puckFXTrailEndWidth;
        currentProfile.puckFXTrailLifetime = defaultValues.puckFXTrailLifetime;
        currentProfile.puckFXTrailStartAlpha = defaultValues.puckFXTrailStartAlpha;
        currentProfile.puckFXTrailEndAlpha = defaultValues.puckFXTrailEndAlpha;
        currentProfile.puckFXSilhouetteColor = defaultValues.puckFXSilhouetteColor;

        SaveProfile();

        swappers.PuckFXSwapper.ApplyAll();
    }

    /// <summary>
    /// Resets only the team color properties of the current profile
    /// to their default values and saves the profile.
    /// </summary>
    public static void ResetTeamColorsToDefault()
    {
        Plugin.Log("Resetting team color settings to their default values.");

        var defaultValues = new Profile();

        currentProfile.teamColorsEnabled = defaultValues.teamColorsEnabled;
        currentProfile.blueTeamColor = defaultValues.blueTeamColor;
        currentProfile.redTeamColor = defaultValues.redTeamColor;
        currentProfile.blueTeamName = defaultValues.blueTeamName;
        currentProfile.redTeamName = defaultValues.redTeamName;

        SaveProfile();

        ToasterReskinLoaderAPI.NotifyTeamColorsChanged();
    }

    /// <summary>
    /// Resets only the shadow-related properties of the current profile
    /// to their default values and saves the profile.
    /// </summary>
    public static void ResetShadowsToDefault()
    {
        Plugin.Log("Resetting shadow settings to their default values.");

        var defaultValues = new Profile();

        currentProfile.crispyShadowsEnabled = defaultValues.crispyShadowsEnabled;
        currentProfile.shadowResolution = defaultValues.shadowResolution;
        currentProfile.shadowDistance = defaultValues.shadowDistance;
        currentProfile.shadowCascadeCount = defaultValues.shadowCascadeCount;
        currentProfile.shadowSoftShadows = defaultValues.shadowSoftShadows;

        SaveProfile();

        swappers.CrispyShadowsSwapper.Apply();
    }

    /// <summary>
    /// Resets only the Gloss Remover properties of the current profile
    /// to their default values and saves the profile.
    /// </summary>
    public static void ResetGlossRemoverToDefault()
    {
        Plugin.Log("Resetting Gloss Remover settings to their default values.");

        var defaultValues = new Profile();

        currentProfile.glossRemoverEnabled = defaultValues.glossRemoverEnabled;
        currentProfile.glossSmoothness = defaultValues.glossSmoothness;
        currentProfile.glossAffectSticks = defaultValues.glossAffectSticks;
        currentProfile.glossAffectPlayers = defaultValues.glossAffectPlayers;
        currentProfile.glossAffectPucks = defaultValues.glossAffectPucks;

        SaveProfile();

        swappers.GlossSwapper.RestoreAll();
        if (currentProfile.glossRemoverEnabled)
            swappers.GlossSwapper.Scan();
    }

    /// <summary>
    /// Resets the entire profile to defaults (the "start fresh" action in the Presets
    /// section). Callers should refresh the world afterward (PresetApplier.RefreshWorld).
    /// </summary>
    public static void ResetAllToDefault()
    {
        Plugin.Log("Resetting ALL reskin settings to their default values.");
        currentProfile = new Profile();
        SaveProfile();
    }

    public static void ResetMinimapToDefault()
    {
        Plugin.Log("Resetting minimap settings to their default values.");

        var defaultValues = new Profile();

        currentProfile.blueMinimapNumberColor = defaultValues.blueMinimapNumberColor;
        currentProfile.redMinimapNumberColor = defaultValues.redMinimapNumberColor;
        currentProfile.minimapPuckColor = defaultValues.minimapPuckColor;
        currentProfile.minimapPlayerScale = defaultValues.minimapPlayerScale;
        currentProfile.minimapPuckScale = defaultValues.minimapPuckScale;
        currentProfile.minimapRefreshRate = defaultValues.minimapRefreshRate;
        currentProfile.localPlayerMinimapIconEnabled = defaultValues.localPlayerMinimapIconEnabled;
        currentProfile.blueLocalPlayerMinimapIconColor = defaultValues.blueLocalPlayerMinimapIconColor;
        currentProfile.redLocalPlayerMinimapIconColor = defaultValues.redLocalPlayerMinimapIconColor;

        SaveProfile();
    }

    public class Profile
    {
        // Sticks section
        [PresetField("Sticks", "Attacker (team)", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerBlue;
        [PresetField("Sticks", "Attacker (personal)", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerBluePersonal;
        [PresetField("Sticks", "Attacker (team)", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerRed;
        [PresetField("Sticks", "Attacker (personal)", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerRedPersonal;
        [PresetField("Sticks", "Goalie (team)", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieBlue;
        [PresetField("Sticks", "Goalie (personal)", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieBluePersonal;
        [PresetField("Sticks", "Goalie (team)", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieRed;
        [PresetField("Sticks", "Goalie (personal)", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieRedPersonal;
        
        // Jerseys (grouped with Skaters / Goalies to match the menu's section layout)
        [PresetField("Skaters", "Jersey torso", ReskinType = "jersey_torso")]
        public ReskinRegistry.ReskinEntry blueSkaterTorso;
        [PresetField("Skaters", "Jersey groin", ReskinType = "jersey_groin")]
        public ReskinRegistry.ReskinEntry blueSkaterGroin;
        [PresetField("Goalies", "Jersey torso", ReskinType = "jersey_torso")]
        public ReskinRegistry.ReskinEntry blueGoalieTorso;
        [PresetField("Goalies", "Jersey groin", ReskinType = "jersey_groin")]
        public ReskinRegistry.ReskinEntry blueGoalieGroin;
        [PresetField("Skaters", "Jersey torso", ReskinType = "jersey_torso")]
        public ReskinRegistry.ReskinEntry  redSkaterTorso;
        [PresetField("Skaters", "Jersey groin", ReskinType = "jersey_groin")]
        public ReskinRegistry.ReskinEntry  redSkaterGroin;
        [PresetField("Goalies", "Jersey torso", ReskinType = "jersey_torso")]
        public ReskinRegistry.ReskinEntry  redGoalieTorso;
        [PresetField("Goalies", "Jersey groin", ReskinType = "jersey_groin")]
        public ReskinRegistry.ReskinEntry  redGoalieGroin;
        [PresetField("Goalies", "Left pad", ReskinType = "legpad")]
        public ReskinRegistry.ReskinEntry blueLegPadLeft;
        [PresetField("Goalies", "Right pad", ReskinType = "legpad")]
        public ReskinRegistry.ReskinEntry blueLegPadRight;
        [PresetField("Goalies", "Left pad", ReskinType = "legpad")]
        public ReskinRegistry.ReskinEntry redLegPadLeft;
        [PresetField("Goalies", "Right pad", ReskinType = "legpad")]
        public ReskinRegistry.ReskinEntry redLegPadRight;
        [PresetField("Goalies", "Pad default color")]
        public Color blueLegPadDefaultColor = new Color(0.151f, 0.151f, 0.151f, 1f);
        [PresetField("Goalies", "Pad default color")]
        public Color redLegPadDefaultColor = new Color(0.151f, 0.151f, 0.151f, 1f);
        [PresetField("Goalies", "Helmet", ReskinType = "helmet")]
        public ReskinRegistry.ReskinEntry blueGoalieHelmet;
        [PresetField("Goalies", "Helmet", ReskinType = "helmet")]
        public ReskinRegistry.ReskinEntry redGoalieHelmet;
        [PresetField("Goalies", "Helmet color")]
        public Color blueGoalieHelmetColor = Color.black;
        [PresetField("Goalies", "Helmet color")]
        public Color redGoalieHelmetColor = Color.black;

        [PresetField("Goalies", "Mask", ReskinType = "goalie_mask")]
        public ReskinRegistry.ReskinEntry blueGoalieMask;
        [PresetField("Goalies", "Mask", ReskinType = "goalie_mask")]
        public ReskinRegistry.ReskinEntry redGoalieMask;
        [PresetField("Goalies", "Mask color")]
        public Color blueGoalieMaskColor = Color.black;
        [PresetField("Goalies", "Mask color")]
        public Color redGoalieMaskColor = Color.black;

        [PresetField("Goalies", "Cage color")]
        public Color blueGoalieCageColor = new Color(0.708f, 0.708f, 0.708f, 1f);
        [PresetField("Goalies", "Cage color")]
        public Color redGoalieCageColor = new Color(0.708f, 0.708f, 0.708f, 1f);

        [PresetField("Skaters", "Helmet", ReskinType = "helmet")]
        public ReskinRegistry.ReskinEntry blueSkaterHelmet;
        [PresetField("Skaters", "Helmet", ReskinType = "helmet")]
        public ReskinRegistry.ReskinEntry redSkaterHelmet;
        [PresetField("Skaters", "Helmet color")]
        public Color blueSkaterHelmetColor = Color.black;
        [PresetField("Skaters", "Helmet color")]
        public Color redSkaterHelmetColor = Color.black;

        // Lettering colors (default: white)
        [PresetField("Skaters", "Lettering color")]
        public Color blueSkaterLetteringColor = new Color(1f, 1f, 1f, 1f);
        [PresetField("Skaters", "Lettering color")]
        public Color redSkaterLetteringColor = new Color(1f, 1f, 1f, 1f);
        [PresetField("Goalies", "Lettering color")]
        public Color blueGoalieLetteringColor = new Color(1f, 1f, 1f, 1f);
        [PresetField("Goalies", "Lettering color")]
        public Color redGoalieLetteringColor = new Color(1f, 1f, 1f, 1f);

        // Jersey number outline (default: width 0 = off, color black)
        [PresetField("Skaters", "Number outline color")]
        public Color blueSkaterNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Skaters", "Number outline color")]
        public Color redSkaterNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Goalies", "Number outline color")]
        public Color blueGoalieNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Goalies", "Number outline color")]
        public Color redGoalieNumberOutlineColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Skaters", "Number outline width")]
        public float blueSkaterNumberOutlineWidth = 0f;
        [PresetField("Skaters", "Number outline width")]
        public float redSkaterNumberOutlineWidth = 0f;
        [PresetField("Goalies", "Number outline width")]
        public float blueGoalieNumberOutlineWidth = 0f;
        [PresetField("Goalies", "Number outline width")]
        public float redGoalieNumberOutlineWidth = 0f;

        // Puck section
        public ReskinRegistry.ReskinEntry puck; // Kept for backwards compatibility
        [PresetField("Puck", "Randomizer pucks", ReskinType = "puck")]
        public List<ReskinRegistry.ReskinEntry> puckList = new List<ReskinRegistry.ReskinEntry>();

        // Arena section
        [PresetField("Arena", "Full arena enabled")]
        public bool fullArenaEnabled = false;
        [PresetField("Arena", "Full arena bundle")]
        public string fullArenaBundle = "";
        [PresetField("Arena", "Full arena prefab")]
        public string fullArenaPrefab = "Arena";
        [PresetField("Arena", "Full arena workshop id")]
        public string fullArenaWorkshopId = "";
        [PresetField("Arena", "Crowd enabled")]
        public bool crowdEnabled = true;
        [PresetField("Arena", "Hangar enabled")]
        public bool hangarEnabled = true;
        [PresetField("Arena", "Glass enabled")]
        public bool glassEnabled = true;
        [PresetField("Arena", "Scoreboard enabled")]
        public bool scoreboardEnabled = true;
        [PresetField("Arena", "Ice", ReskinType = "rink_ice")]
        public ReskinRegistry.ReskinEntry ice;
        [PresetField("Arena", "Ice smoothness")]
        public float                      iceSmoothness = 0.8f;
        [PresetField("Arena", "Boards border top")]
        public Color boardsBorderTopColor    = new Color(0, 0.260123f, 1, 1);
        [PresetField("Arena", "Boards middle")]
        public Color boardsMiddleColor       = new Color(1, 1, 1, 1);
        [PresetField("Arena", "Boards border bottom")]
        public Color boardsBorderBottomColor = new Color(1, 0.868332f, 0, 1);
        [PresetField("Arena", "Pillars color")]
        public Color pillarsColor = new Color(0.7830189f, 0.7830189f, 0.7830189f, 1);
        [PresetField("Arena", "Glass smoothness")]
        public float glassSmoothness = 1f;
        [PresetField("Arena", "Spectator density")]
        public float spectatorDensity = 0.25f;
        [PresetField("Arena", "Net", ReskinType = "net")]
        public ReskinRegistry.ReskinEntry net;

        // Stick Tape Customization
        // Blue Team Skater
        [PresetField("Tape", "Skater blade mode")]
        public string blueSkaterBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Skater blade", ReskinType = "tape_attacker_blade")]
        public ReskinRegistry.ReskinEntry blueSkaterBladeTape;
        [PresetField("Tape", "Skater blade color")]
        public Color blueSkaterBladeTapeColor = Color.white;

        [PresetField("Tape", "Skater shaft mode")]
        public string blueSkaterShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Skater shaft", ReskinType = "tape_attacker_shaft")]
        public ReskinRegistry.ReskinEntry blueSkaterShaftTape;
        [PresetField("Tape", "Skater shaft color")]
        public Color blueSkaterShaftTapeColor = Color.white;

        // Blue Team Goalie
        [PresetField("Tape", "Goalie blade mode")]
        public string blueGoalieBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Goalie blade", ReskinType = "tape_goalie_blade")]
        public ReskinRegistry.ReskinEntry blueGoalieBladeTape;
        [PresetField("Tape", "Goalie blade color")]
        public Color blueGoalieBladeTapeColor = Color.white;

        [PresetField("Tape", "Goalie shaft mode")]
        public string blueGoalieShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Goalie shaft", ReskinType = "tape_goalie_shaft")]
        public ReskinRegistry.ReskinEntry blueGoalieShaftTape;
        [PresetField("Tape", "Goalie shaft color")]
        public Color blueGoalieShaftTapeColor = Color.white;

        // Red Team Skater
        [PresetField("Tape", "Skater blade mode")]
        public string redSkaterBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Skater blade", ReskinType = "tape_attacker_blade")]
        public ReskinRegistry.ReskinEntry redSkaterBladeTape;
        [PresetField("Tape", "Skater blade color")]
        public Color redSkaterBladeTapeColor = Color.white;

        [PresetField("Tape", "Skater shaft mode")]
        public string redSkaterShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Skater shaft", ReskinType = "tape_attacker_shaft")]
        public ReskinRegistry.ReskinEntry redSkaterShaftTape;
        [PresetField("Tape", "Skater shaft color")]
        public Color redSkaterShaftTapeColor = Color.white;

        // Red Team Goalie
        [PresetField("Tape", "Goalie blade mode")]
        public string redGoalieBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Goalie blade", ReskinType = "tape_goalie_blade")]
        public ReskinRegistry.ReskinEntry redGoalieBladeTape;
        [PresetField("Tape", "Goalie blade color")]
        public Color redGoalieBladeTapeColor = Color.white;

        [PresetField("Tape", "Goalie shaft mode")]
        public string redGoalieShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Goalie shaft", ReskinType = "tape_goalie_shaft")]
        public ReskinRegistry.ReskinEntry redGoalieShaftTape;
        [PresetField("Tape", "Goalie shaft color")]
        public Color redGoalieShaftTapeColor = Color.white;

        // UI section
        [PresetField("Team Colors", "Enabled")]
        public bool teamColorsEnabled = false;
        [PresetField("Team Colors", "Color")]
        public Color blueTeamColor = new Color(0.231f, 0.510f, 0.965f, 1f); // #3b82f6
        [PresetField("Team Colors", "Color")]
        public Color redTeamColor = new Color(0.820f, 0.200f, 0.200f, 1f);  // #d13333
        [PresetField("Team Colors", "Indicator enabled")]
        public bool teamIndicatorEnabled = false;
        [PresetField("Team Colors", "Name")]
        public string blueTeamName = "";
        [PresetField("Team Colors", "Name")]
        public string redTeamName = "";

        // Minimap section
        [PresetField("Minimap", "Number color")]
        public Color blueMinimapNumberColor = Color.white;
        [PresetField("Minimap", "Number color")]
        public Color redMinimapNumberColor = Color.white;
        [PresetField("Minimap", "Puck color")]
        public Color minimapPuckColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Minimap", "Player scale")]
        public float minimapPlayerScale = 1f;
        [PresetField("Minimap", "Puck scale")]
        public float minimapPuckScale = 1f;
        [PresetField("Minimap", "Refresh rate")]
        public int minimapRefreshRate = 60;
        [PresetField("Minimap", "Local player icon enabled")]
        public bool localPlayerMinimapIconEnabled = false;
        [PresetField("Minimap", "Local player icon color")]
        public Color blueLocalPlayerMinimapIconColor = new Color(0f, 1f, 0f, 1f);
        [PresetField("Minimap", "Local player icon color")]
        public Color redLocalPlayerMinimapIconColor = new Color(0f, 1f, 0f, 1f);

        // Chat section
        [PresetField("Chat", "Height")]
        public float chatHeight = 300f; // game default
        [PresetField("Chat", "Background")]
        public bool chatBackground = false;
        [PresetField("Chat", "Quick chat X")]
        public float quickChatX = 0f;
        [PresetField("Chat", "Quick chat Y")]
        public float quickChatY = 50f;
        [PresetField("Chat", "Render all emojis")]
        public bool chatRenderAllEmojis = true;

        // Shadows section (CrispyShadows)
        [PresetField("Shadows", "Enabled")]
        public bool crispyShadowsEnabled = true;
        [PresetField("Shadows", "Resolution")]
        public int shadowResolution = 8192;
        [PresetField("Shadows", "Distance")]
        public float shadowDistance = 50f;
        [PresetField("Shadows", "Cascade count")]
        public int shadowCascadeCount = 4;
        [PresetField("Shadows", "Soft shadows")]
        public bool shadowSoftShadows = true;

        // Skybox section
        [PresetField("Skybox", "Atmosphere thickness")]
        public float skyboxAtmosphereThickness = 1;
        [PresetField("Skybox", "Exposure")]
        public float skyboxExposure = 1.3f;
        [PresetField("Skybox", "Sun disk")]
        public float skyboxSunDisk = 1;
        [PresetField("Skybox", "Sun size")]
        public float skyboxSunSize = 0.04f;
        [PresetField("Skybox", "Sun size convergence")]
        public float skyboxSunSizeConvergence = 5;
        [PresetField("Skybox", "Ground color")]
        public Color skyboxGroundColor = new Color(0.369f, 0.349f, 0.341f, 1f);
        [PresetField("Skybox", "Sky tint")]
        public Color skyboxSkyTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Puck FX section
        [PresetField("Puck FX", "Outline color")]
        public Color puckFXOutlineColor = Color.white;
        [PresetField("Puck FX", "Outline kernel size")]
        public int puckFXOutlineKernelSize = 1;
        [PresetField("Puck FX", "Elevation indicator color")]
        public Color puckFXElevationIndicatorColor = new Color(0f, 0f, 0f, 1f);
        [PresetField("Puck FX", "Verticality line color")]
        public Color puckFXVerticalityLineColor = new Color(0f, 0f, 0f, 0.8f);
        [PresetField("Puck FX", "Verticality line start alpha")]
        public float puckFXVerticalityLineStartAlpha = 0.5f;
        [PresetField("Puck FX", "Verticality line end alpha")]
        public float puckFXVerticalityLineEndAlpha = 1f;
        [PresetField("Puck FX", "Trail enabled")]
        public bool puckFXTrailEnabled = false;
        [PresetField("Puck FX", "Trail color")]
        public Color puckFXTrailColor = Color.black;
        [PresetField("Puck FX", "Trail start width")]
        public float puckFXTrailStartWidth = 0.1f;
        [PresetField("Puck FX", "Trail end width")]
        public float puckFXTrailEndWidth = 0f;
        [PresetField("Puck FX", "Trail lifetime")]
        public float puckFXTrailLifetime = 0.6f;
        [PresetField("Puck FX", "Trail start alpha")]
        public float puckFXTrailStartAlpha = 0f;
        [PresetField("Puck FX", "Trail end alpha")]
        public float puckFXTrailEndAlpha = 1f;
        [PresetField("Puck FX", "Silhouette color")]
        public Color puckFXSilhouetteColor = new Color(1f, 1f, 1f, 0.502f);

        // QoL config lives in its own side-car files now (reskinprofiles/
        // QoL.json + ServerPrefs.json) so visual profiles stay shareable
        // without leaking toggles or per-server credentials. See QoLStorage.

        // Gloss Remover section
        [PresetField("Gloss", "Enabled")]
        public bool glossRemoverEnabled = false;
        [PresetField("Gloss", "Smoothness")]
        public float glossSmoothness = 0.5f;
        [PresetField("Gloss", "Affect sticks")]
        public bool glossAffectSticks = true;
        [PresetField("Gloss", "Affect players")]
        public bool glossAffectPlayers = true;
        [PresetField("Gloss", "Affect pucks")]
        public bool glossAffectPucks = true;
    }
    
    /// <summary>
    /// A lightweight, serializable reference to a specific reskin entry.
    /// </summary>
    [Serializable]
    private class ReskinReference
    {
        [JsonProperty("packId")]
        public string PackId { get; set; }

        [JsonProperty("entryName")]
        public string EntryName { get; set; }
        
        [JsonProperty("workshopId")]
        public ulong WorkshopId { get; set; }
    }
    
    /// <summary>
    /// The data structure that is actually saved to and loaded from the JSON file.
    /// </summary>
    [Serializable]
    private class SerializableProfile
    {
        // STICKS
        [JsonProperty("stickAttackerBlueRef")]
        public ReskinReference StickAttackerBlueRef { get; set; }
        [JsonProperty("stickAttackerBluePersonalRef")]
        public ReskinReference StickAttackerBluePersonalRef { get; set; }
        [JsonProperty("stickAttackerRedRef")]
        public ReskinReference StickAttackerRedRef { get; set; }
        [JsonProperty("stickAttackerRedPersonalRef")]
        public ReskinReference StickAttackerRedPersonalRef { get; set; }
  
        [JsonProperty("stickGoalieBlueRef")]
        public ReskinReference StickGoalieBlueRef { get; set; }
        [JsonProperty("stickGoalieBluePersonalRef")]
        public ReskinReference StickGoalieBluePersonalRef { get; set; }
        [JsonProperty("stickGoalieRedRef")]
        public ReskinReference StickGoalieRedRef { get; set; }
        [JsonProperty("stickGoalieRedPersonalRef")]
        public ReskinReference StickGoalieRedPersonalRef { get; set; }

        [JsonProperty("blueGoalieHelmetRef")]
        public ReskinReference BlueGoalieHelmetRef { get; set; }
    
        [JsonProperty("redGoalieHelmetRef")]
        public ReskinReference RedGoalieHelmetRef { get; set; }

        [JsonProperty("blueSkaterTorsoRef")]
        public ReskinReference BlueSkaterTorsoRef { get; set; }
        [JsonProperty("blueSkaterGroinRef")]
        public ReskinReference BlueSkaterGroinRef { get; set; }
        [JsonProperty("blueGoalieTorsoRef")]
        public ReskinReference BlueGoalieTorsoRef { get; set; }
        [JsonProperty("blueGoalieGroinRef")]
        public ReskinReference BlueGoalieGroinRef { get; set; }
        [JsonProperty("redSkaterTorsoRef")]
        public ReskinReference RedSkaterTorsoRef { get; set; }
        [JsonProperty("redSkaterGroinRef")]
        public ReskinReference RedSkaterGroinRef { get; set; }
        [JsonProperty("redGoalieTorsoRef")]
        public ReskinReference RedGoalieTorsoRef { get; set; }
        [JsonProperty("redGoalieGroinRef")]
        public ReskinReference RedGoalieGroinRef { get; set; }
        
        [JsonProperty("blueLegPadLeftRef")]
        public ReskinReference BlueLegPadLeftRef { get; set; }
        [JsonProperty("blueLegPadRightRef")]
        public ReskinReference BlueLegPadRightRef { get; set; }
        [JsonProperty("redLegPadLeftRef")]
        public ReskinReference RedLegPadLeftRef { get; set; }
        [JsonProperty("redLegPadRightRef")]
        public ReskinReference RedLegPadRightRef { get; set; }
        [JsonProperty("blueLegPadDefaultColor")]
        public SerializableColor BlueLegPadDefaultColor { get; set; }
        [JsonProperty("redLegPadDefaultColor")]
        public SerializableColor RedLegPadDefaultColor { get; set; }

        [JsonProperty("blueGoalieHelmetColor")]
        public SerializableColor BlueGoalieHelmetColor { get; set; }
        [JsonProperty("redGoalieHelmetColor")]
        public SerializableColor RedGoalieHelmetColor { get; set; }

        [JsonProperty("blueGoalieMaskRef")]
        public ReskinReference BlueGoalieMaskRef { get; set; }
        [JsonProperty("redGoalieMaskRef")]
        public ReskinReference RedGoalieMaskRef { get; set; }
        [JsonProperty("blueGoalieMaskColor")]
        public SerializableColor BlueGoalieMaskColor { get; set; }
        [JsonProperty("redGoalieMaskColor")]
        public SerializableColor RedGoalieMaskColor { get; set; }

        [JsonProperty("blueGoalieCageColor")]
        public SerializableColor BlueGoalieCageColor { get; set; }
        [JsonProperty("redGoalieCageColor")]
        public SerializableColor RedGoalieCageColor { get; set; }

        [JsonProperty("blueSkaterHelmetRef")]
        public ReskinReference BlueSkaterHelmetRef { get; set; }
        [JsonProperty("redSkaterHelmetRef")]
        public ReskinReference RedSkaterHelmetRef { get; set; }
        [JsonProperty("blueSkaterHelmetColor")]
        public SerializableColor BlueSkaterHelmetColor { get; set; }
        [JsonProperty("redSkaterHelmetColor")]
        public SerializableColor RedSkaterHelmetColor { get; set; }

        [JsonProperty("blueSkaterLetteringColor")]
        public SerializableColor BlueSkaterLetteringColor { get; set; }
        [JsonProperty("redSkaterLetteringColor")]
        public SerializableColor RedSkaterLetteringColor { get; set; }
        [JsonProperty("blueGoalieLetteringColor")]
        public SerializableColor BlueGoalieLetteringColor { get; set; }
        [JsonProperty("redGoalieLetteringColor")]
        public SerializableColor RedGoalieLetteringColor { get; set; }

        [JsonProperty("blueSkaterNumberOutlineColor")]
        public SerializableColor BlueSkaterNumberOutlineColor { get; set; }
        [JsonProperty("redSkaterNumberOutlineColor")]
        public SerializableColor RedSkaterNumberOutlineColor { get; set; }
        [JsonProperty("blueGoalieNumberOutlineColor")]
        public SerializableColor BlueGoalieNumberOutlineColor { get; set; }
        [JsonProperty("redGoalieNumberOutlineColor")]
        public SerializableColor RedGoalieNumberOutlineColor { get; set; }
        [JsonProperty("blueSkaterNumberOutlineWidth")]
        public float? BlueSkaterNumberOutlineWidth { get; set; }
        [JsonProperty("redSkaterNumberOutlineWidth")]
        public float? RedSkaterNumberOutlineWidth { get; set; }
        [JsonProperty("blueGoalieNumberOutlineWidth")]
        public float? BlueGoalieNumberOutlineWidth { get; set; }
        [JsonProperty("redGoalieNumberOutlineWidth")]
        public float? RedGoalieNumberOutlineWidth { get; set; }

        // ARENA
        [JsonProperty("fullArenaEnabled")]
        public bool? FullArenaEnabled { get; set; }
    
        [JsonProperty("fullArenaBundle")]
        public string FullArenaBundle { get; set; }
    
        [JsonProperty("fullArenaPrefab")]
            public string FullArenaPrefab { get; set; }
        [JsonProperty("fullArenaWorkshopId")]
            public string FullArenaWorkshopId { get; set; }
        [JsonProperty("crowdEnabled")]
        public bool? CrowdEnabled { get; set; }
        [JsonProperty("scoreboardEnabled")]
        public bool? ScoreboardEnabled { get; set; }
        [JsonProperty("glassEnabled")]
        public bool? GlassEnabled { get; set; }
        [JsonProperty("hangarEnabled")]
        public bool? HangarEnabled { get; set; }
        
        [JsonProperty("iceRef")]
        public ReskinReference IceRef { get; set; }
        [JsonProperty("iceSmoothness")]
        public float? IceSmoothness { get; set; }
        [JsonProperty("glassSmoothness")]
        public float? GlassSmoothness { get; set; }
        [JsonProperty("pillarsColor")]
        public SerializableColor PillarsColor { get; set; }
        [JsonProperty("spectatorDensity")]
        public float? SpectatorDensity { get; set; }
        
        
        [JsonProperty("boardsBorderTopColor")]
        public SerializableColor BoardsBorderTopColor { get; set; }
        [JsonProperty("boardsMiddleColor")]
        public SerializableColor BoardsMiddleColor { get; set; }
        [JsonProperty("boardsBorderBottomColor")]
        public SerializableColor BoardsBorderBottomColor { get; set; }
        [JsonProperty("netRef")]
        public ReskinReference NetRef { get; set; }

        // STICK TAPE
        [JsonProperty("blueSkaterBladeTapeMode")]
        public string BlueSkaterBladeTapeMode { get; set; }
        [JsonProperty("blueSkaterBladeTapeRef")]
        public ReskinReference BlueSkaterBladeTapeRef { get; set; }
        [JsonProperty("blueSkaterBladeTapeColor")]
        public SerializableColor BlueSkaterBladeTapeColor { get; set; }

        [JsonProperty("blueSkaterShaftTapeMode")]
        public string BlueSkaterShaftTapeMode { get; set; }
        [JsonProperty("blueSkaterShaftTapeRef")]
        public ReskinReference BlueSkaterShaftTapeRef { get; set; }
        [JsonProperty("blueSkaterShaftTapeColor")]
        public SerializableColor BlueSkaterShaftTapeColor { get; set; }

        [JsonProperty("blueGoalieBladeTapeMode")]
        public string BlueGoalieBladeTapeMode { get; set; }
        [JsonProperty("blueGoalieBladeTapeRef")]
        public ReskinReference BlueGoalieBladeTapeRef { get; set; }
        [JsonProperty("blueGoalieBladeTapeColor")]
        public SerializableColor BlueGoalieBladeTapeColor { get; set; }

        [JsonProperty("blueGoalieShaftTapeMode")]
        public string BlueGoalieShaftTapeMode { get; set; }
        [JsonProperty("blueGoalieShaftTapeRef")]
        public ReskinReference BlueGoalieShaftTapeRef { get; set; }
        [JsonProperty("blueGoalieShaftTapeColor")]
        public SerializableColor BlueGoalieShaftTapeColor { get; set; }

        [JsonProperty("redSkaterBladeTapeMode")]
        public string RedSkaterBladeTapeMode { get; set; }
        [JsonProperty("redSkaterBladeTapeRef")]
        public ReskinReference RedSkaterBladeTapeRef { get; set; }
        [JsonProperty("redSkaterBladeTapeColor")]
        public SerializableColor RedSkaterBladeTapeColor { get; set; }

        [JsonProperty("redSkaterShaftTapeMode")]
        public string RedSkaterShaftTapeMode { get; set; }
        [JsonProperty("redSkaterShaftTapeRef")]
        public ReskinReference RedSkaterShaftTapeRef { get; set; }
        [JsonProperty("redSkaterShaftTapeColor")]
        public SerializableColor RedSkaterShaftTapeColor { get; set; }

        [JsonProperty("redGoalieBladeTapeMode")]
        public string RedGoalieBladeTapeMode { get; set; }
        [JsonProperty("redGoalieBladeTapeRef")]
        public ReskinReference RedGoalieBladeTapeRef { get; set; }
        [JsonProperty("redGoalieBladeTapeColor")]
        public SerializableColor RedGoalieBladeTapeColor { get; set; }

        [JsonProperty("redGoalieShaftTapeMode")]
        public string RedGoalieShaftTapeMode { get; set; }
        [JsonProperty("redGoalieShaftTapeRef")]
        public ReskinReference RedGoalieShaftTapeRef { get; set; }
        [JsonProperty("redGoalieShaftTapeColor")]
        public SerializableColor RedGoalieShaftTapeColor { get; set; }

        // PUCKS
        [JsonProperty("puckRef")]
        public ReskinReference PuckRef { get; set; }
        [JsonProperty("puckListRef")]
        public List<ReskinReference> PuckListRef { get; set; } = new List<ReskinReference>();

        // TEAM COLORS
        [JsonProperty("teamColorsEnabled")]
        public bool? TeamColorsEnabled { get; set; }
        [JsonProperty("blueTeamColor")]
        public SerializableColor BlueTeamColor { get; set; }
        [JsonProperty("redTeamColor")]
        public SerializableColor RedTeamColor { get; set; }
        [JsonProperty("teamIndicatorEnabled")]
        public bool? TeamIndicatorEnabled { get; set; }
        [JsonProperty("blueTeamName")]
        public string BlueTeamName { get; set; }
        [JsonProperty("redTeamName")]
        public string RedTeamName { get; set; }

        // MINIMAP
        [JsonProperty("blueMinimapNumberColor")]
        public SerializableColor BlueMinimapNumberColor { get; set; }
        [JsonProperty("redMinimapNumberColor")]
        public SerializableColor RedMinimapNumberColor { get; set; }
        [JsonProperty("minimapPuckColor")]
        public SerializableColor MinimapPuckColor { get; set; }
        [JsonProperty("minimapPlayerScale")]
        public float? MinimapPlayerScale { get; set; }
        [JsonProperty("minimapPuckScale")]
        public float? MinimapPuckScale { get; set; }
        [JsonProperty("minimapRefreshRate")]
        public int? MinimapRefreshRate { get; set; }
        [JsonProperty("localPlayerMinimapIconEnabled")]
        public bool? LocalPlayerMinimapIconEnabled { get; set; }
        [JsonProperty("blueLocalPlayerMinimapIconColor")]
        public SerializableColor BlueLocalPlayerMinimapIconColor { get; set; }
        [JsonProperty("redLocalPlayerMinimapIconColor")]
        public SerializableColor RedLocalPlayerMinimapIconColor { get; set; }

        // CHAT
        [JsonProperty("chatHeight")]
        public float? ChatHeight { get; set; }
        [JsonProperty("chatBackground")]
        public bool? ChatBackground { get; set; }
        [JsonProperty("quickChatX")]
        public float? QuickChatX { get; set; }
        [JsonProperty("quickChatY")]
        public float? QuickChatY { get; set; }
        [JsonProperty("chatRenderAllEmojis")]
        public bool? ChatRenderAllEmojis { get; set; }

        // SHADOWS (CrispyShadows)
        [JsonProperty("crispyShadowsEnabled")]
        public bool? CrispyShadowsEnabled { get; set; }
        [JsonProperty("shadowResolution")]
        public int? ShadowResolution { get; set; }
        [JsonProperty("shadowDistance")]
        public float? ShadowDistance { get; set; }
        [JsonProperty("shadowCascadeCount")]
        public int? ShadowCascadeCount { get; set; }
        [JsonProperty("shadowSoftShadows")]
        public bool? ShadowSoftShadows { get; set; }

        // SKYBOX
        [JsonProperty("skyboxAtmosphereThickness")]
        public float? SkyboxAtmosphereThickness { get; set; }
        [JsonProperty("skyboxExposure")]
        public float? SkyboxExposure { get; set; }
        [JsonProperty("skyboxSunDisk")]
        public float? SkyboxSunDisk { get; set; }
        [JsonProperty("skyboxSunSize")]
        public float? SkyboxSunSize { get; set; }
        [JsonProperty("skyboxSunSizeConvergence")]
        public float? SkyboxSunSizeConvergence { get; set; }
        [JsonProperty("skyboxGroundColor")]
        public SerializableColor SkyboxGroundColor { get; set; }
        [JsonProperty("skyboxSkyTint")]
        public SerializableColor SkyboxSkyTint { get; set; }

        // PUCK FX
        [JsonProperty("puckFXOutlineColor")]
        public SerializableColor PuckFXOutlineColor { get; set; }
        [JsonProperty("puckFXOutlineKernelSize")]
        public int? PuckFXOutlineKernelSize { get; set; }
        [JsonProperty("puckFXElevationIndicatorColor")]
        public SerializableColor PuckFXElevationIndicatorColor { get; set; }
        [JsonProperty("puckFXVerticalityLineColor")]
        public SerializableColor PuckFXVerticalityLineColor { get; set; }
        [JsonProperty("puckFXVerticalityLineStartAlpha")]
        public float? PuckFXVerticalityLineStartAlpha { get; set; }
        [JsonProperty("puckFXVerticalityLineEndAlpha")]
        public float? PuckFXVerticalityLineEndAlpha { get; set; }
        [JsonProperty("puckFXTrailEnabled")]
        public bool? PuckFXTrailEnabled { get; set; }
        [JsonProperty("puckFXTrailColor")]
        public SerializableColor PuckFXTrailColor { get; set; }
        [JsonProperty("puckFXTrailStartWidth")]
        public float? PuckFXTrailStartWidth { get; set; }
        [JsonProperty("puckFXTrailEndWidth")]
        public float? PuckFXTrailEndWidth { get; set; }
        [JsonProperty("puckFXTrailLifetime")]
        public float? PuckFXTrailLifetime { get; set; }
        [JsonProperty("puckFXTrailStartAlpha")]
        public float? PuckFXTrailStartAlpha { get; set; }
        [JsonProperty("puckFXTrailEndAlpha")]
        public float? PuckFXTrailEndAlpha { get; set; }
        [JsonProperty("puckFXSilhouetteColor")]
        public SerializableColor PuckFXSilhouetteColor { get; set; }

        // QoL is persisted by QoLStorage (config/ToastersReskinLoaderQoL.json
        // + config/ToastersReskinLoaderServerPrefs.json). Do not add a
        // playerQoL property here — it would re-couple the visual reskin
        // profile to QoL state and re-introduce the share-leak risk the
        // split was meant to avoid.
      
        // Glossiness
        [JsonProperty("glossRemoverEnabled")]
        public bool? GlossRemoverEnabled { get; set; }
        [JsonProperty("glossSmoothness")]
        public float? GlossSmoothness { get; set; }
        [JsonProperty("glossAffectSticks")]
        public bool? GlossAffectSticks { get; set; }
        [JsonProperty("glossAffectPlayers")]
        public bool? GlossAffectPlayers { get; set; }
        [JsonProperty("glossAffectPucks")]
        public bool? GlossAffectPucks { get; set; }
    }
}

/// <summary>
/// A simple, serializable representation of a UnityEngine.Color.
/// This avoids the self-referencing loop issue with Newtonsoft.Json.
/// </summary>
[Serializable]
public class SerializableColor
{
    public float r, g, b, a;

    // A default constructor for deserialization
    public SerializableColor() { }

    // A constructor to easily convert from a Unity Color
    public SerializableColor(Color color)
    {
        r = color.r;
        g = color.g;
        b = color.b;
        a = color.a;
    }

    // An explicit conversion operator to easily convert back to a Unity Color
    public static explicit operator Color(SerializableColor sc)
    {
        return new Color(sc.r, sc.g, sc.b, sc.a);
    }
}