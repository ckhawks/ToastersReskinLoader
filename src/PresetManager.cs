using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ToasterReskinLoader.swappers;
using UnityEngine;

namespace ToasterReskinLoader;

/// <summary>
/// Manages saving and loading of full profile presets.
/// Each preset is a serializable snapshot of the current Profile stored as an individual JSON file.
/// Presets live in the reskinprofiles/presets/ folder alongside the main profile.
/// </summary>
public static class PresetManager
{
    private static readonly string PresetsFolder = Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
        "reskinprofiles",
        "presets");

    private const string FileExtension = ".json";

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include
    };

    private static void EnsureFolder()
    {
        if (!Directory.Exists(PresetsFolder))
            Directory.CreateDirectory(PresetsFolder);
    }

    private static string GetFilePath(string presetName)
    {
        string safe = string.Join("_", presetName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(PresetsFolder, safe + FileExtension);
    }

    public static List<PresetInfo> GetPresets()
    {
        EnsureFolder();
        var presets = new List<PresetInfo>();

        foreach (var file in Directory.GetFiles(PresetsFolder, $"*{FileExtension}"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var data = JsonConvert.DeserializeObject<PresetData>(json);
                if (data != null)
                {
                    data.Name = data.Name ?? Path.GetFileNameWithoutExtension(file);
                    data.FilePath = file;
                    presets.Add(new PresetInfo(data.Name, file, data.SavedAt));
                }
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"Skipping corrupt preset '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return presets;
    }

    public static void SavePreset(string name)
    {
        EnsureFolder();
        var data = PresetData.FromCurrentProfile(name);
        string json = JsonConvert.SerializeObject(data, JsonSettings);
        string path = GetFilePath(name);
        File.WriteAllText(path, json);
        Plugin.Log($"Preset '{name}' saved to {path}");
    }

    public static bool LoadPreset(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Plugin.LogWarning($"Preset file not found: {filePath}");
                return false;
            }

            string json = File.ReadAllText(filePath);
            var data = JsonConvert.DeserializeObject<PresetData>(json);
            if (data == null)
            {
                Plugin.LogError($"Failed to deserialize preset: {filePath}");
                return false;
            }

            data.ApplyToCurrentProfile();
            Plugin.Log($"Preset '{data.Name}' loaded from {filePath}");

            ReskinProfileManager.LoadTexturesForActiveReskins();
            SwapperManager.SetAll();
            PuckFXSwapper.ApplyAll();
            ChangingRoomHelper.Scan();

            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to load preset '{filePath}': {ex.Message}");
            return false;
        }
    }

    public static bool LoadPresetByName(string name)
    {
        string path = GetFilePath(name);
        return LoadPreset(path);
    }

    public static bool DeletePreset(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Plugin.Log($"Preset deleted: {filePath}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to delete preset '{filePath}': {ex.Message}");
            return false;
        }
    }

    public static bool RenamePreset(string oldFilePath, string newName)
    {
        try
        {
            if (!File.Exists(oldFilePath)) return false;

            string newPath = GetFilePath(newName);
            if (File.Exists(newPath))
            {
                Plugin.LogWarning($"Cannot rename: a preset named '{newName}' already exists.");
                return false;
            }

            string json = File.ReadAllText(oldFilePath);
            var data = JsonConvert.DeserializeObject<PresetData>(json);
            if (data == null) return false;
            data.Name = newName;
            string newJson = JsonConvert.SerializeObject(data, JsonSettings);
            File.WriteAllText(newPath, newJson);
            File.Delete(oldFilePath);

            Plugin.Log($"Preset renamed to '{newName}'");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to rename preset: {ex.Message}");
            return false;
        }
    }

    public static bool PresetExists(string name)
    {
        return File.Exists(GetFilePath(name));
    }

    // ── Data container ──────────────────────────────────────────────────

    [Serializable]
    public class PresetData
    {
        [JsonProperty("presetName")]
        public string Name { get; set; }

        [JsonProperty("savedAt")]
        public string SavedAt { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Not serialized. Set at runtime for UI convenience.
        /// </summary>
        [JsonIgnore]
        public string FilePath { get; set; }

        // Sticks
        [JsonProperty("stickAttackerBlueRef")]
        public ReskinProfileManager.ReskinReference StickAttackerBlueRef { get; set; }
        [JsonProperty("stickAttackerBluePersonalRef")]
        public ReskinProfileManager.ReskinReference StickAttackerBluePersonalRef { get; set; }
        [JsonProperty("stickAttackerRedRef")]
        public ReskinProfileManager.ReskinReference StickAttackerRedRef { get; set; }
        [JsonProperty("stickAttackerRedPersonalRef")]
        public ReskinProfileManager.ReskinReference StickAttackerRedPersonalRef { get; set; }
        [JsonProperty("stickGoalieBlueRef")]
        public ReskinProfileManager.ReskinReference StickGoalieBlueRef { get; set; }
        [JsonProperty("stickGoalieBluePersonalRef")]
        public ReskinProfileManager.ReskinReference StickGoalieBluePersonalRef { get; set; }
        [JsonProperty("stickGoalieRedRef")]
        public ReskinProfileManager.ReskinReference StickGoalieRedRef { get; set; }
        [JsonProperty("stickGoalieRedPersonalRef")]
        public ReskinProfileManager.ReskinReference StickGoalieRedPersonalRef { get; set; }

        // Jerseys
        [JsonProperty("blueSkaterTorsoRef")]
        public ReskinProfileManager.ReskinReference BlueSkaterTorsoRef { get; set; }
        [JsonProperty("blueSkaterGroinRef")]
        public ReskinProfileManager.ReskinReference BlueSkaterGroinRef { get; set; }
        [JsonProperty("blueGoalieTorsoRef")]
        public ReskinProfileManager.ReskinReference BlueGoalieTorsoRef { get; set; }
        [JsonProperty("blueGoalieGroinRef")]
        public ReskinProfileManager.ReskinReference BlueGoalieGroinRef { get; set; }
        [JsonProperty("redSkaterTorsoRef")]
        public ReskinProfileManager.ReskinReference RedSkaterTorsoRef { get; set; }
        [JsonProperty("redSkaterGroinRef")]
        public ReskinProfileManager.ReskinReference RedSkaterGroinRef { get; set; }
        [JsonProperty("redGoalieTorsoRef")]
        public ReskinProfileManager.ReskinReference RedGoalieTorsoRef { get; set; }
        [JsonProperty("redGoalieGroinRef")]
        public ReskinProfileManager.ReskinReference RedGoalieGroinRef { get; set; }

        // Goalie pads
        [JsonProperty("blueLegPadLeftRef")]
        public ReskinProfileManager.ReskinReference BlueLegPadLeftRef { get; set; }
        [JsonProperty("blueLegPadRightRef")]
        public ReskinProfileManager.ReskinReference BlueLegPadRightRef { get; set; }
        [JsonProperty("redLegPadLeftRef")]
        public ReskinProfileManager.ReskinReference RedLegPadLeftRef { get; set; }
        [JsonProperty("redLegPadRightRef")]
        public ReskinProfileManager.ReskinReference RedLegPadRightRef { get; set; }
        [JsonProperty("blueLegPadDefaultColor")]
        public SerializableColor BlueLegPadDefaultColor { get; set; }
        [JsonProperty("redLegPadDefaultColor")]
        public SerializableColor RedLegPadDefaultColor { get; set; }

        // Helmets & masks
        [JsonProperty("blueGoalieHelmetRef")]
        public ReskinProfileManager.ReskinReference BlueGoalieHelmetRef { get; set; }
        [JsonProperty("redGoalieHelmetRef")]
        public ReskinProfileManager.ReskinReference RedGoalieHelmetRef { get; set; }
        [JsonProperty("blueGoalieHelmetColor")]
        public SerializableColor BlueGoalieHelmetColor { get; set; }
        [JsonProperty("redGoalieHelmetColor")]
        public SerializableColor RedGoalieHelmetColor { get; set; }
        [JsonProperty("blueGoalieMaskRef")]
        public ReskinProfileManager.ReskinReference BlueGoalieMaskRef { get; set; }
        [JsonProperty("redGoalieMaskRef")]
        public ReskinProfileManager.ReskinReference RedGoalieMaskRef { get; set; }
        [JsonProperty("blueGoalieMaskColor")]
        public SerializableColor BlueGoalieMaskColor { get; set; }
        [JsonProperty("redGoalieMaskColor")]
        public SerializableColor RedGoalieMaskColor { get; set; }
        [JsonProperty("blueGoalieCageColor")]
        public SerializableColor BlueGoalieCageColor { get; set; }
        [JsonProperty("redGoalieCageColor")]
        public SerializableColor RedGoalieCageColor { get; set; }
        [JsonProperty("blueSkaterHelmetRef")]
        public ReskinProfileManager.ReskinReference BlueSkaterHelmetRef { get; set; }
        [JsonProperty("redSkaterHelmetRef")]
        public ReskinProfileManager.ReskinReference RedSkaterHelmetRef { get; set; }
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

        // Jersey number outline widths
        [JsonProperty("blueSkaterNumberOutlineWidth2")]
        public float? BlueSkaterNumberOutlineWidth2 { get; set; }
        [JsonProperty("redSkaterNumberOutlineWidth2")]
        public float? RedSkaterNumberOutlineWidth2 { get; set; }
        [JsonProperty("blueGoalieNumberOutlineWidth2")]
        public float? BlueGoalieNumberOutlineWidth2 { get; set; }
        [JsonProperty("redGoalieNumberOutlineWidth2")]
        public float? RedGoalieNumberOutlineWidth2 { get; set; }

        // Puck
        [JsonProperty("puckRef")]
        public ReskinProfileManager.ReskinReference PuckRef { get; set; }
        [JsonProperty("puckListRef")]
        public List<ReskinProfileManager.ReskinReference> PuckListRef { get; set; }

        // Arena
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
        [JsonProperty("hangarEnabled")]
        public bool? HangarEnabled { get; set; }
        [JsonProperty("glassEnabled")]
        public bool? GlassEnabled { get; set; }
        [JsonProperty("scoreboardEnabled")]
        public bool? ScoreboardEnabled { get; set; }
        [JsonProperty("iceRef")]
        public ReskinProfileManager.ReskinReference IceRef { get; set; }
        [JsonProperty("iceSmoothness")]
        public float? IceSmoothness { get; set; }
        [JsonProperty("boardsBorderTopColor")]
        public SerializableColor BoardsBorderTopColor { get; set; }
        [JsonProperty("boardsMiddleColor")]
        public SerializableColor BoardsMiddleColor { get; set; }
        [JsonProperty("boardsBorderBottomColor")]
        public SerializableColor BoardsBorderBottomColor { get; set; }
        [JsonProperty("glassSmoothness")]
        public float? GlassSmoothness { get; set; }
        [JsonProperty("pillarsColor")]
        public SerializableColor PillarsColor { get; set; }
        [JsonProperty("spectatorDensity")]
        public float? SpectatorDensity { get; set; }
        [JsonProperty("netRef")]
        public ReskinProfileManager.ReskinReference NetRef { get; set; }

        // Stick tape
        [JsonProperty("blueSkaterBladeTapeMode")] public string BlueSkaterBladeTapeMode { get; set; }
        [JsonProperty("blueSkaterBladeTapeRef")] public ReskinProfileManager.ReskinReference BlueSkaterBladeTapeRef { get; set; }
        [JsonProperty("blueSkaterBladeTapeColor")] public SerializableColor BlueSkaterBladeTapeColor { get; set; }
        [JsonProperty("blueSkaterShaftTapeMode")] public string BlueSkaterShaftTapeMode { get; set; }
        [JsonProperty("blueSkaterShaftTapeRef")] public ReskinProfileManager.ReskinReference BlueSkaterShaftTapeRef { get; set; }
        [JsonProperty("blueSkaterShaftTapeColor")] public SerializableColor BlueSkaterShaftTapeColor { get; set; }
        [JsonProperty("blueGoalieBladeTapeMode")] public string BlueGoalieBladeTapeMode { get; set; }
        [JsonProperty("blueGoalieBladeTapeRef")] public ReskinProfileManager.ReskinReference BlueGoalieBladeTapeRef { get; set; }
        [JsonProperty("blueGoalieBladeTapeColor")] public SerializableColor BlueGoalieBladeTapeColor { get; set; }
        [JsonProperty("blueGoalieShaftTapeMode")] public string BlueGoalieShaftTapeMode { get; set; }
        [JsonProperty("blueGoalieShaftTapeRef")] public ReskinProfileManager.ReskinReference BlueGoalieShaftTapeRef { get; set; }
        [JsonProperty("blueGoalieShaftTapeColor")] public SerializableColor BlueGoalieShaftTapeColor { get; set; }
        [JsonProperty("redSkaterBladeTapeMode")] public string RedSkaterBladeTapeMode { get; set; }
        [JsonProperty("redSkaterBladeTapeRef")] public ReskinProfileManager.ReskinReference RedSkaterBladeTapeRef { get; set; }
        [JsonProperty("redSkaterBladeTapeColor")] public SerializableColor RedSkaterBladeTapeColor { get; set; }
        [JsonProperty("redSkaterShaftTapeMode")] public string RedSkaterShaftTapeMode { get; set; }
        [JsonProperty("redSkaterShaftTapeRef")] public ReskinProfileManager.ReskinReference RedSkaterShaftTapeRef { get; set; }
        [JsonProperty("redSkaterShaftTapeColor")] public SerializableColor RedSkaterShaftTapeColor { get; set; }
        [JsonProperty("redGoalieBladeTapeMode")] public string RedGoalieBladeTapeMode { get; set; }
        [JsonProperty("redGoalieBladeTapeRef")] public ReskinProfileManager.ReskinReference RedGoalieBladeTapeRef { get; set; }
        [JsonProperty("redGoalieBladeTapeColor")] public SerializableColor RedGoalieBladeTapeColor { get; set; }
        [JsonProperty("redGoalieShaftTapeMode")] public string RedGoalieShaftTapeMode { get; set; }
        [JsonProperty("redGoalieShaftTapeRef")] public ReskinProfileManager.ReskinReference RedGoalieShaftTapeRef { get; set; }
        [JsonProperty("redGoalieShaftTapeColor")] public SerializableColor RedGoalieShaftTapeColor { get; set; }

        // Team colors
        [JsonProperty("teamColorsEnabled")] public bool? TeamColorsEnabled { get; set; }
        [JsonProperty("blueTeamColor")] public SerializableColor BlueTeamColor { get; set; }
        [JsonProperty("redTeamColor")] public SerializableColor RedTeamColor { get; set; }
        [JsonProperty("teamIndicatorEnabled")] public bool? TeamIndicatorEnabled { get; set; }
        [JsonProperty("blueTeamName")] public string BlueTeamName { get; set; }
        [JsonProperty("redTeamName")] public string RedTeamName { get; set; }

        // Minimap
        [JsonProperty("blueMinimapNumberColor")] public SerializableColor BlueMinimapNumberColor { get; set; }
        [JsonProperty("redMinimapNumberColor")] public SerializableColor RedMinimapNumberColor { get; set; }
        [JsonProperty("minimapPuckColor")] public SerializableColor MinimapPuckColor { get; set; }
        [JsonProperty("minimapPlayerScale")] public float? MinimapPlayerScale { get; set; }
        [JsonProperty("minimapPuckScale")] public float? MinimapPuckScale { get; set; }
        [JsonProperty("minimapRefreshRate")] public int? MinimapRefreshRate { get; set; }
        [JsonProperty("localPlayerMinimapIconEnabled")] public bool? LocalPlayerMinimapIconEnabled { get; set; }
        [JsonProperty("blueLocalPlayerMinimapIconColor")] public SerializableColor BlueLocalPlayerMinimapIconColor { get; set; }
        [JsonProperty("redLocalPlayerMinimapIconColor")] public SerializableColor RedLocalPlayerMinimapIconColor { get; set; }

        // Chat
        [JsonProperty("chatHeight")] public float? ChatHeight { get; set; }
        [JsonProperty("chatBackground")] public bool? ChatBackground { get; set; }
        [JsonProperty("quickChatX")] public float? QuickChatX { get; set; }
        [JsonProperty("quickChatY")] public float? QuickChatY { get; set; }
        [JsonProperty("chatRenderAllEmojis")] public bool? ChatRenderAllEmojis { get; set; }

        // Shadows
        [JsonProperty("crispyShadowsEnabled")] public bool? CrispyShadowsEnabled { get; set; }
        [JsonProperty("shadowResolution")] public int? ShadowResolution { get; set; }
        [JsonProperty("shadowDistance")] public float? ShadowDistance { get; set; }
        [JsonProperty("shadowCascadeCount")] public int? ShadowCascadeCount { get; set; }
        [JsonProperty("shadowSoftShadows")] public bool? ShadowSoftShadows { get; set; }

        // Skybox
        [JsonProperty("skyboxAtmosphereThickness")] public float? SkyboxAtmosphereThickness { get; set; }
        [JsonProperty("skyboxExposure")] public float? SkyboxExposure { get; set; }
        [JsonProperty("skyboxSunDisk")] public float? SkyboxSunDisk { get; set; }
        [JsonProperty("skyboxSunSize")] public float? SkyboxSunSize { get; set; }
        [JsonProperty("skyboxSunSizeConvergence")] public float? SkyboxSunSizeConvergence { get; set; }
        [JsonProperty("skyboxGroundColor")] public SerializableColor SkyboxGroundColor { get; set; }
        [JsonProperty("skyboxSkyTint")] public SerializableColor SkyboxSkyTint { get; set; }

        // Puck FX
        [JsonProperty("puckFXOutlineColor")] public SerializableColor PuckFXOutlineColor { get; set; }
        [JsonProperty("puckFXOutlineKernelSize")] public int? PuckFXOutlineKernelSize { get; set; }
        [JsonProperty("puckFXElevationIndicatorColor")] public SerializableColor PuckFXElevationIndicatorColor { get; set; }
        [JsonProperty("puckFXVerticalityLineColor")] public SerializableColor PuckFXVerticalityLineColor { get; set; }
        [JsonProperty("puckFXVerticalityLineStartAlpha")] public float? PuckFXVerticalityLineStartAlpha { get; set; }
        [JsonProperty("puckFXVerticalityLineEndAlpha")] public float? PuckFXVerticalityLineEndAlpha { get; set; }
        [JsonProperty("puckFXTrailEnabled")] public bool? PuckFXTrailEnabled { get; set; }
        [JsonProperty("puckFXTrailColor")] public SerializableColor PuckFXTrailColor { get; set; }
        [JsonProperty("puckFXTrailStartWidth")] public float? PuckFXTrailStartWidth { get; set; }
        [JsonProperty("puckFXTrailEndWidth")] public float? PuckFXTrailEndWidth { get; set; }
        [JsonProperty("puckFXTrailLifetime")] public float? PuckFXTrailLifetime { get; set; }
        [JsonProperty("puckFXTrailStartAlpha")] public float? PuckFXTrailStartAlpha { get; set; }
        [JsonProperty("puckFXTrailEndAlpha")] public float? PuckFXTrailEndAlpha { get; set; }
        [JsonProperty("puckFXSilhouetteColor")] public SerializableColor PuckFXSilhouetteColor { get; set; }

        // Gloss
        [JsonProperty("glossRemoverEnabled")] public bool? GlossRemoverEnabled { get; set; }
        [JsonProperty("glossSmoothness")] public float? GlossSmoothness { get; set; }
        [JsonProperty("glossAffectSticks")] public bool? GlossAffectSticks { get; set; }
        [JsonProperty("glossAffectPlayers")] public bool? GlossAffectPlayers { get; set; }
        [JsonProperty("glossAffectPucks")] public bool? GlossAffectPucks { get; set; }

        public static PresetData FromCurrentProfile(string name)
        {
            var p = ReskinProfileManager.currentProfile;
            return new PresetData
            {
                Name = name,
                SavedAt = DateTime.UtcNow.ToString("o"),
                Version = 1,

                StickAttackerBlueRef = CreateRef(p.stickAttackerBlue),
                StickAttackerBluePersonalRef = CreateRef(p.stickAttackerBluePersonal),
                StickAttackerRedRef = CreateRef(p.stickAttackerRed),
                StickAttackerRedPersonalRef = CreateRef(p.stickAttackerRedPersonal),
                StickGoalieBlueRef = CreateRef(p.stickGoalieBlue),
                StickGoalieBluePersonalRef = CreateRef(p.stickGoalieBluePersonal),
                StickGoalieRedRef = CreateRef(p.stickGoalieRed),
                StickGoalieRedPersonalRef = CreateRef(p.stickGoalieRed),

                BlueSkaterTorsoRef = CreateRef(p.blueSkaterTorso),
                BlueSkaterGroinRef = CreateRef(p.blueSkaterGroin),
                BlueGoalieTorsoRef = CreateRef(p.blueGoalieTorso),
                BlueGoalieGroinRef = CreateRef(p.blueGoalieGroin),
                RedSkaterTorsoRef = CreateRef(p.redSkaterTorso),
                RedSkaterGroinRef = CreateRef(p.redSkaterGroin),
                RedGoalieTorsoRef = CreateRef(p.redGoalieTorso),
                RedGoalieGroinRef = CreateRef(p.redGoalieGroin),

                BlueLegPadLeftRef = CreateRef(p.blueLegPadLeft),
                BlueLegPadRightRef = CreateRef(p.blueLegPadRight),
                RedLegPadLeftRef = CreateRef(p.redLegPadLeft),
                RedLegPadRightRef = CreateRef(p.redLegPadRight),
                BlueLegPadDefaultColor = new SerializableColor(p.blueLegPadDefaultColor),
                RedLegPadDefaultColor = new SerializableColor(p.redLegPadDefaultColor),

                BlueGoalieHelmetRef = CreateRef(p.blueGoalieHelmet),
                RedGoalieHelmetRef = CreateRef(p.redGoalieHelmet),
                BlueGoalieHelmetColor = new SerializableColor(p.blueGoalieHelmetColor),
                RedGoalieHelmetColor = new SerializableColor(p.redGoalieHelmetColor),
                BlueGoalieMaskRef = CreateRef(p.blueGoalieMask),
                RedGoalieMaskRef = CreateRef(p.redGoalieMask),
                BlueGoalieMaskColor = new SerializableColor(p.blueGoalieMaskColor),
                RedGoalieMaskColor = new SerializableColor(p.redGoalieMaskColor),
                BlueGoalieCageColor = new SerializableColor(p.blueGoalieCageColor),
                RedGoalieCageColor = new SerializableColor(p.redGoalieCageColor),
                BlueSkaterHelmetRef = CreateRef(p.blueSkaterHelmet),
                RedSkaterHelmetRef = CreateRef(p.redSkaterHelmet),
                BlueSkaterHelmetColor = new SerializableColor(p.blueSkaterHelmetColor),
                RedSkaterHelmetColor = new SerializableColor(p.redSkaterHelmetColor),
                BlueSkaterLetteringColor = new SerializableColor(p.blueSkaterLetteringColor),
                RedSkaterLetteringColor = new SerializableColor(p.redSkaterLetteringColor),
                BlueGoalieLetteringColor = new SerializableColor(p.blueGoalieLetteringColor),
                RedGoalieLetteringColor = new SerializableColor(p.redGoalieLetteringColor),
                BlueSkaterNumberOutlineColor = new SerializableColor(p.blueSkaterNumberOutlineColor),
                RedSkaterNumberOutlineColor = new SerializableColor(p.redSkaterNumberOutlineColor),
                BlueGoalieNumberOutlineColor = new SerializableColor(p.blueGoalieNumberOutlineColor),
                RedGoalieNumberOutlineColor = new SerializableColor(p.redGoalieNumberOutlineColor),
                BlueSkaterNumberOutlineWidth = p.blueSkaterNumberOutlineWidth,
                RedSkaterNumberOutlineWidth = p.redSkaterNumberOutlineWidth,
                BlueGoalieNumberOutlineWidth = p.blueGoalieNumberOutlineWidth,
                RedGoalieNumberOutlineWidth = p.redGoalieNumberOutlineWidth,

                PuckRef = CreateRef(p.puck),
                PuckListRef = p.puckList?.Select(x => CreateRef(x)).ToList(),

                FullArenaEnabled = p.fullArenaEnabled,
                FullArenaBundle = p.fullArenaBundle,
                FullArenaPrefab = p.fullArenaPrefab,
                FullArenaWorkshopId = p.fullArenaWorkshopId,
                CrowdEnabled = p.crowdEnabled,
                HangarEnabled = p.hangarEnabled,
                GlassEnabled = p.glassEnabled,
                ScoreboardEnabled = p.scoreboardEnabled,
                IceRef = CreateRef(p.ice),
                IceSmoothness = p.iceSmoothness,
                BoardsBorderTopColor = new SerializableColor(p.boardsBorderTopColor),
                BoardsMiddleColor = new SerializableColor(p.boardsMiddleColor),
                BoardsBorderBottomColor = new SerializableColor(p.boardsBorderBottomColor),
                GlassSmoothness = p.glassSmoothness,
                PillarsColor = new SerializableColor(p.pillarsColor),
                SpectatorDensity = p.spectatorDensity,
                NetRef = CreateRef(p.net),

                BlueSkaterBladeTapeMode = p.blueSkaterBladeTapeMode,
                BlueSkaterBladeTapeRef = CreateRef(p.blueSkaterBladeTape),
                BlueSkaterBladeTapeColor = new SerializableColor(p.blueSkaterBladeTapeColor),
                BlueSkaterShaftTapeMode = p.blueSkaterShaftTapeMode,
                BlueSkaterShaftTapeRef = CreateRef(p.blueSkaterShaftTape),
                BlueSkaterShaftTapeColor = new SerializableColor(p.blueSkaterShaftTapeColor),
                BlueGoalieBladeTapeMode = p.blueGoalieBladeTapeMode,
                BlueGoalieBladeTapeRef = CreateRef(p.blueGoalieBladeTape),
                BlueGoalieBladeTapeColor = new SerializableColor(p.blueGoalieBladeTapeColor),
                BlueGoalieShaftTapeMode = p.blueGoalieShaftTapeMode,
                BlueGoalieShaftTapeRef = CreateRef(p.blueGoalieShaftTape),
                BlueGoalieShaftTapeColor = new SerializableColor(p.blueGoalieShaftTapeColor),
                RedSkaterBladeTapeMode = p.redSkaterBladeTapeMode,
                RedSkaterBladeTapeRef = CreateRef(p.redSkaterBladeTape),
                RedSkaterBladeTapeColor = new SerializableColor(p.redSkaterBladeTapeColor),
                RedSkaterShaftTapeMode = p.redSkaterShaftTapeMode,
                RedSkaterShaftTapeRef = CreateRef(p.redSkaterShaftTape),
                RedSkaterShaftTapeColor = new SerializableColor(p.redSkaterShaftTapeColor),
                RedGoalieBladeTapeMode = p.redGoalieBladeTapeMode,
                RedGoalieBladeTapeRef = CreateRef(p.redGoalieBladeTape),
                RedGoalieBladeTapeColor = new SerializableColor(p.redGoalieBladeTapeColor),
                RedGoalieShaftTapeMode = p.redGoalieShaftTapeMode,
                RedGoalieShaftTapeRef = CreateRef(p.redGoalieShaftTape),
                RedGoalieShaftTapeColor = new SerializableColor(p.redGoalieShaftTapeColor),

                TeamColorsEnabled = p.teamColorsEnabled,
                BlueTeamColor = new SerializableColor(p.blueTeamColor),
                RedTeamColor = new SerializableColor(p.redTeamColor),
                TeamIndicatorEnabled = p.teamIndicatorEnabled,
                BlueTeamName = p.blueTeamName,
                RedTeamName = p.redTeamName,

                BlueMinimapNumberColor = new SerializableColor(p.blueMinimapNumberColor),
                RedMinimapNumberColor = new SerializableColor(p.redMinimapNumberColor),
                MinimapPuckColor = new SerializableColor(p.minimapPuckColor),
                MinimapPlayerScale = p.minimapPlayerScale,
                MinimapPuckScale = p.minimapPuckScale,
                MinimapRefreshRate = p.minimapRefreshRate,
                LocalPlayerMinimapIconEnabled = p.localPlayerMinimapIconEnabled,
                BlueLocalPlayerMinimapIconColor = new SerializableColor(p.blueLocalPlayerMinimapIconColor),
                RedLocalPlayerMinimapIconColor = new SerializableColor(p.redLocalPlayerMinimapIconColor),

                ChatHeight = p.chatHeight,
                ChatBackground = p.chatBackground,
                QuickChatX = p.quickChatX,
                QuickChatY = p.quickChatY,
                ChatRenderAllEmojis = p.chatRenderAllEmojis,

                CrispyShadowsEnabled = p.crispyShadowsEnabled,
                ShadowResolution = p.shadowResolution,
                ShadowDistance = p.shadowDistance,
                ShadowCascadeCount = p.shadowCascadeCount,
                ShadowSoftShadows = p.shadowSoftShadows,

                SkyboxAtmosphereThickness = p.skyboxAtmosphereThickness,
                SkyboxExposure = p.skyboxExposure,
                SkyboxSunDisk = p.skyboxSunDisk,
                SkyboxSunSize = p.skyboxSunSize,
                SkyboxSunSizeConvergence = p.skyboxSunSizeConvergence,
                SkyboxGroundColor = new SerializableColor(p.skyboxGroundColor),
                SkyboxSkyTint = new SerializableColor(p.skyboxSkyTint),

                PuckFXOutlineColor = new SerializableColor(p.puckFXOutlineColor),
                PuckFXOutlineKernelSize = p.puckFXOutlineKernelSize,
                PuckFXElevationIndicatorColor = new SerializableColor(p.puckFXElevationIndicatorColor),
                PuckFXVerticalityLineColor = new SerializableColor(p.puckFXVerticalityLineColor),
                PuckFXVerticalityLineStartAlpha = p.puckFXVerticalityLineStartAlpha,
                PuckFXVerticalityLineEndAlpha = p.puckFXVerticalityLineEndAlpha,
                PuckFXTrailEnabled = p.puckFXTrailEnabled,
                PuckFXTrailColor = new SerializableColor(p.puckFXTrailColor),
                PuckFXTrailStartWidth = p.puckFXTrailStartWidth,
                PuckFXTrailEndWidth = p.puckFXTrailEndWidth,
                PuckFXTrailLifetime = p.puckFXTrailLifetime,
                PuckFXTrailStartAlpha = p.puckFXTrailStartAlpha,
                PuckFXTrailEndAlpha = p.puckFXTrailEndAlpha,
                PuckFXSilhouetteColor = new SerializableColor(p.puckFXSilhouetteColor),

                GlossRemoverEnabled = p.glossRemoverEnabled,
                GlossSmoothness = p.glossSmoothness,
                GlossAffectSticks = p.glossAffectSticks,
                GlossAffectPlayers = p.glossAffectPlayers,
                GlossAffectPucks = p.glossAffectPucks,
            };
        }

        public void ApplyToCurrentProfile()
        {
            var p = ReskinProfileManager.currentProfile;
            var def = new ReskinProfileManager.Profile();

            p.stickAttackerBlue = FindEntry(StickAttackerBlueRef, "stick_attacker");
            p.stickAttackerBluePersonal = FindEntry(StickAttackerBluePersonalRef, "stick_attacker");
            p.stickAttackerRed = FindEntry(StickAttackerRedRef, "stick_attacker");
            p.stickAttackerRedPersonal = FindEntry(StickAttackerRedPersonalRef, "stick_attacker");
            p.stickGoalieBlue = FindEntry(StickGoalieBlueRef, "stick_goalie");
            p.stickGoalieBluePersonal = FindEntry(StickGoalieBluePersonalRef, "stick_goalie");
            p.stickGoalieRed = FindEntry(StickGoalieRedRef, "stick_goalie");
            p.stickGoalieRedPersonal = FindEntry(StickGoalieRedPersonalRef, "stick_goalie");

            p.blueSkaterTorso = FindEntry(BlueSkaterTorsoRef, "jersey_torso");
            p.blueSkaterGroin = FindEntry(BlueSkaterGroinRef, "jersey_groin");
            p.blueGoalieTorso = FindEntry(BlueGoalieTorsoRef, "jersey_torso");
            p.blueGoalieGroin = FindEntry(BlueGoalieGroinRef, "jersey_groin");
            p.redSkaterTorso = FindEntry(RedSkaterTorsoRef, "jersey_torso");
            p.redSkaterGroin = FindEntry(RedSkaterGroinRef, "jersey_groin");
            p.redGoalieTorso = FindEntry(RedGoalieTorsoRef, "jersey_torso");
            p.redGoalieGroin = FindEntry(RedGoalieGroinRef, "jersey_groin");

            p.blueLegPadLeft = FindEntry(BlueLegPadLeftRef, "legpad");
            p.blueLegPadRight = FindEntry(BlueLegPadRightRef, "legpad");
            p.redLegPadLeft = FindEntry(RedLegPadLeftRef, "legpad");
            p.redLegPadRight = FindEntry(RedLegPadRightRef, "legpad");
            p.blueLegPadDefaultColor = BlueLegPadDefaultColor != null ? (Color)BlueLegPadDefaultColor : def.blueLegPadDefaultColor;
            p.redLegPadDefaultColor = RedLegPadDefaultColor != null ? (Color)RedLegPadDefaultColor : def.redLegPadDefaultColor;

            p.blueGoalieHelmet = FindEntry(BlueGoalieHelmetRef, "helmet");
            p.redGoalieHelmet = FindEntry(RedGoalieHelmetRef, "helmet");
            p.blueGoalieHelmetColor = BlueGoalieHelmetColor != null ? (Color)BlueGoalieHelmetColor : def.blueGoalieHelmetColor;
            p.redGoalieHelmetColor = RedGoalieHelmetColor != null ? (Color)RedGoalieHelmetColor : def.redGoalieHelmetColor;
            p.blueGoalieMask = FindEntry(BlueGoalieMaskRef, "goalie_mask");
            p.redGoalieMask = FindEntry(RedGoalieMaskRef, "goalie_mask");
            p.blueGoalieMaskColor = BlueGoalieMaskColor != null ? (Color)BlueGoalieMaskColor : def.blueGoalieMaskColor;
            p.redGoalieMaskColor = RedGoalieMaskColor != null ? (Color)RedGoalieMaskColor : def.redGoalieMaskColor;
            p.blueGoalieCageColor = BlueGoalieCageColor != null ? (Color)BlueGoalieCageColor : def.blueGoalieCageColor;
            p.redGoalieCageColor = RedGoalieCageColor != null ? (Color)RedGoalieCageColor : def.redGoalieCageColor;
            p.blueSkaterHelmet = FindEntry(BlueSkaterHelmetRef, "helmet");
            p.redSkaterHelmet = FindEntry(RedSkaterHelmetRef, "helmet");
            p.blueSkaterHelmetColor = BlueSkaterHelmetColor != null ? (Color)BlueSkaterHelmetColor : def.blueSkaterHelmetColor;
            p.redSkaterHelmetColor = RedSkaterHelmetColor != null ? (Color)RedSkaterHelmetColor : def.redSkaterHelmetColor;
            p.blueSkaterLetteringColor = BlueSkaterLetteringColor != null ? (Color)BlueSkaterLetteringColor : def.blueSkaterLetteringColor;
            p.redSkaterLetteringColor = RedSkaterLetteringColor != null ? (Color)RedSkaterLetteringColor : def.redSkaterLetteringColor;
            p.blueGoalieLetteringColor = BlueGoalieLetteringColor != null ? (Color)BlueGoalieLetteringColor : def.blueGoalieLetteringColor;
            p.redGoalieLetteringColor = RedGoalieLetteringColor != null ? (Color)RedGoalieLetteringColor : def.redGoalieLetteringColor;
            p.blueSkaterNumberOutlineColor = BlueSkaterNumberOutlineColor != null ? (Color)BlueSkaterNumberOutlineColor : def.blueSkaterNumberOutlineColor;
            p.redSkaterNumberOutlineColor = RedSkaterNumberOutlineColor != null ? (Color)RedSkaterNumberOutlineColor : def.redSkaterNumberOutlineColor;
            p.blueGoalieNumberOutlineColor = BlueGoalieNumberOutlineColor != null ? (Color)BlueGoalieNumberOutlineColor : def.blueGoalieNumberOutlineColor;
            p.redGoalieNumberOutlineColor = RedGoalieNumberOutlineColor != null ? (Color)RedGoalieNumberOutlineColor : def.redGoalieNumberOutlineColor;
            p.blueSkaterNumberOutlineWidth = BlueSkaterNumberOutlineWidth ?? def.blueSkaterNumberOutlineWidth;
            p.redSkaterNumberOutlineWidth = RedSkaterNumberOutlineWidth ?? def.redSkaterNumberOutlineWidth;
            p.blueGoalieNumberOutlineWidth = BlueGoalieNumberOutlineWidth ?? def.blueGoalieNumberOutlineWidth;
            p.redGoalieNumberOutlineWidth = RedGoalieNumberOutlineWidth ?? def.redGoalieNumberOutlineWidth;

            p.puck = FindEntry(PuckRef, "puck");
            p.puckList = PuckListRef?.Select(r => FindEntry(r, "puck")).Where(e => e != null).ToList()
                         ?? new List<ReskinRegistry.ReskinEntry>();

            p.fullArenaEnabled = FullArenaEnabled ?? def.fullArenaEnabled;
            p.fullArenaBundle = FullArenaBundle ?? def.fullArenaBundle;
            p.fullArenaPrefab = FullArenaPrefab ?? def.fullArenaPrefab;
            p.fullArenaWorkshopId = FullArenaWorkshopId ?? def.fullArenaWorkshopId;
            p.crowdEnabled = CrowdEnabled ?? def.crowdEnabled;
            p.hangarEnabled = HangarEnabled ?? def.hangarEnabled;
            p.glassEnabled = GlassEnabled ?? def.glassEnabled;
            p.scoreboardEnabled = ScoreboardEnabled ?? def.scoreboardEnabled;
            p.ice = FindEntry(IceRef, "rink_ice");
            p.iceSmoothness = IceSmoothness ?? def.iceSmoothness;
            p.boardsBorderTopColor = BoardsBorderTopColor != null ? (Color)BoardsBorderTopColor : def.boardsBorderTopColor;
            p.boardsMiddleColor = BoardsMiddleColor != null ? (Color)BoardsMiddleColor : def.boardsMiddleColor;
            p.boardsBorderBottomColor = BoardsBorderBottomColor != null ? (Color)BoardsBorderBottomColor : def.boardsBorderBottomColor;
            p.glassSmoothness = GlassSmoothness ?? def.glassSmoothness;
            p.pillarsColor = PillarsColor != null ? (Color)PillarsColor : def.pillarsColor;
            p.spectatorDensity = SpectatorDensity ?? def.spectatorDensity;
            p.net = FindEntry(NetRef, "net");

            p.blueSkaterBladeTapeMode = BlueSkaterBladeTapeMode ?? def.blueSkaterBladeTapeMode;
            p.blueSkaterBladeTape = FindEntry(BlueSkaterBladeTapeRef, "tape_attacker_blade");
            p.blueSkaterBladeTapeColor = BlueSkaterBladeTapeColor != null ? (Color)BlueSkaterBladeTapeColor : def.blueSkaterBladeTapeColor;
            p.blueSkaterShaftTapeMode = BlueSkaterShaftTapeMode ?? def.blueSkaterShaftTapeMode;
            p.blueSkaterShaftTape = FindEntry(BlueSkaterShaftTapeRef, "tape_attacker_shaft");
            p.blueSkaterShaftTapeColor = BlueSkaterShaftTapeColor != null ? (Color)BlueSkaterShaftTapeColor : def.blueSkaterShaftTapeColor;
            p.blueGoalieBladeTapeMode = BlueGoalieBladeTapeMode ?? def.blueGoalieBladeTapeMode;
            p.blueGoalieBladeTape = FindEntry(BlueGoalieBladeTapeRef, "tape_goalie_blade");
            p.blueGoalieBladeTapeColor = BlueGoalieBladeTapeColor != null ? (Color)BlueGoalieBladeTapeColor : def.blueGoalieBladeTapeColor;
            p.blueGoalieShaftTapeMode = BlueGoalieShaftTapeMode ?? def.blueGoalieShaftTapeMode;
            p.blueGoalieShaftTape = FindEntry(BlueGoalieShaftTapeRef, "tape_goalie_shaft");
            p.blueGoalieShaftTapeColor = BlueGoalieShaftTapeColor != null ? (Color)BlueGoalieShaftTapeColor : def.blueGoalieShaftTapeColor;
            p.redSkaterBladeTapeMode = RedSkaterBladeTapeMode ?? def.redSkaterBladeTapeMode;
            p.redSkaterBladeTape = FindEntry(RedSkaterBladeTapeRef, "tape_attacker_blade");
            p.redSkaterBladeTapeColor = RedSkaterBladeTapeColor != null ? (Color)RedSkaterBladeTapeColor : def.redSkaterBladeTapeColor;
            p.redSkaterShaftTapeMode = RedSkaterShaftTapeMode ?? def.redSkaterShaftTapeMode;
            p.redSkaterShaftTape = FindEntry(RedSkaterShaftTapeRef, "tape_attacker_shaft");
            p.redSkaterShaftTapeColor = RedSkaterShaftTapeColor != null ? (Color)RedSkaterShaftTapeColor : def.redSkaterShaftTapeColor;
            p.redGoalieBladeTapeMode = RedGoalieBladeTapeMode ?? def.redGoalieBladeTapeMode;
            p.redGoalieBladeTape = FindEntry(RedGoalieBladeTapeRef, "tape_goalie_blade");
            p.redGoalieBladeTapeColor = RedGoalieBladeTapeColor != null ? (Color)RedGoalieBladeTapeColor : def.redGoalieBladeTapeColor;
            p.redGoalieShaftTapeMode = RedGoalieShaftTapeMode ?? def.redGoalieShaftTapeMode;
            p.redGoalieShaftTape = FindEntry(RedGoalieShaftTapeRef, "tape_goalie_shaft");
            p.redGoalieShaftTapeColor = RedGoalieShaftTapeColor != null ? (Color)RedGoalieShaftTapeColor : def.redGoalieShaftTapeColor;

            p.teamColorsEnabled = TeamColorsEnabled ?? def.teamColorsEnabled;
            p.blueTeamColor = BlueTeamColor != null ? (Color)BlueTeamColor : def.blueTeamColor;
            p.redTeamColor = RedTeamColor != null ? (Color)RedTeamColor : def.redTeamColor;
            p.teamIndicatorEnabled = TeamIndicatorEnabled ?? def.teamIndicatorEnabled;
            p.blueTeamName = BlueTeamName ?? def.blueTeamName;
            p.redTeamName = RedTeamName ?? def.redTeamName;

            p.blueMinimapNumberColor = BlueMinimapNumberColor != null ? (Color)BlueMinimapNumberColor : def.blueMinimapNumberColor;
            p.redMinimapNumberColor = RedMinimapNumberColor != null ? (Color)RedMinimapNumberColor : def.redMinimapNumberColor;
            p.minimapPuckColor = MinimapPuckColor != null ? (Color)MinimapPuckColor : def.minimapPuckColor;
            p.minimapPlayerScale = MinimapPlayerScale ?? def.minimapPlayerScale;
            p.minimapPuckScale = MinimapPuckScale ?? def.minimapPuckScale;
            p.minimapRefreshRate = MinimapRefreshRate ?? def.minimapRefreshRate;
            p.localPlayerMinimapIconEnabled = LocalPlayerMinimapIconEnabled ?? def.localPlayerMinimapIconEnabled;
            p.blueLocalPlayerMinimapIconColor = BlueLocalPlayerMinimapIconColor != null ? (Color)BlueLocalPlayerMinimapIconColor : def.blueLocalPlayerMinimapIconColor;
            p.redLocalPlayerMinimapIconColor = RedLocalPlayerMinimapIconColor != null ? (Color)RedLocalPlayerMinimapIconColor : def.redLocalPlayerMinimapIconColor;

            p.chatHeight = ChatHeight ?? def.chatHeight;
            p.chatBackground = ChatBackground ?? def.chatBackground;
            p.quickChatX = QuickChatX ?? def.quickChatX;
            p.quickChatY = QuickChatY ?? def.quickChatY;
            p.chatRenderAllEmojis = ChatRenderAllEmojis ?? def.chatRenderAllEmojis;

            p.crispyShadowsEnabled = CrispyShadowsEnabled ?? def.crispyShadowsEnabled;
            p.shadowResolution = ShadowResolution ?? def.shadowResolution;
            p.shadowDistance = ShadowDistance ?? def.shadowDistance;
            p.shadowCascadeCount = ShadowCascadeCount ?? def.shadowCascadeCount;
            p.shadowSoftShadows = ShadowSoftShadows ?? def.shadowSoftShadows;

            p.skyboxAtmosphereThickness = SkyboxAtmosphereThickness ?? def.skyboxAtmosphereThickness;
            p.skyboxExposure = SkyboxExposure ?? def.skyboxExposure;
            p.skyboxSunDisk = SkyboxSunDisk ?? def.skyboxSunDisk;
            p.skyboxSunSize = SkyboxSunSize ?? def.skyboxSunSize;
            p.skyboxSunSizeConvergence = SkyboxSunSizeConvergence ?? def.skyboxSunSizeConvergence;
            p.skyboxGroundColor = SkyboxGroundColor != null ? (Color)SkyboxGroundColor : def.skyboxGroundColor;
            p.skyboxSkyTint = SkyboxSkyTint != null ? (Color)SkyboxSkyTint : def.skyboxSkyTint;

            p.puckFXOutlineColor = PuckFXOutlineColor != null ? (Color)PuckFXOutlineColor : def.puckFXOutlineColor;
            p.puckFXOutlineKernelSize = PuckFXOutlineKernelSize ?? def.puckFXOutlineKernelSize;
            p.puckFXElevationIndicatorColor = PuckFXElevationIndicatorColor != null ? (Color)PuckFXElevationIndicatorColor : def.puckFXElevationIndicatorColor;
            p.puckFXVerticalityLineColor = PuckFXVerticalityLineColor != null ? (Color)PuckFXVerticalityLineColor : def.puckFXVerticalityLineColor;
            p.puckFXVerticalityLineStartAlpha = PuckFXVerticalityLineStartAlpha ?? def.puckFXVerticalityLineStartAlpha;
            p.puckFXVerticalityLineEndAlpha = PuckFXVerticalityLineEndAlpha ?? def.puckFXVerticalityLineEndAlpha;
            p.puckFXTrailEnabled = PuckFXTrailEnabled ?? def.puckFXTrailEnabled;
            p.puckFXTrailColor = PuckFXTrailColor != null ? (Color)PuckFXTrailColor : def.puckFXTrailColor;
            p.puckFXTrailStartWidth = PuckFXTrailStartWidth ?? def.puckFXTrailStartWidth;
            p.puckFXTrailEndWidth = PuckFXTrailEndWidth ?? def.puckFXTrailEndWidth;
            p.puckFXTrailLifetime = PuckFXTrailLifetime ?? def.puckFXTrailLifetime;
            p.puckFXTrailStartAlpha = PuckFXTrailStartAlpha ?? def.puckFXTrailStartAlpha;
            p.puckFXTrailEndAlpha = PuckFXTrailEndAlpha ?? def.puckFXTrailEndAlpha;
            p.puckFXSilhouetteColor = PuckFXSilhouetteColor != null ? (Color)PuckFXSilhouetteColor : def.puckFXSilhouetteColor;

            p.glossRemoverEnabled = GlossRemoverEnabled ?? def.glossRemoverEnabled;
            p.glossSmoothness = GlossSmoothness ?? def.glossSmoothness;
            p.glossAffectSticks = GlossAffectSticks ?? def.glossAffectSticks;
            p.glossAffectPlayers = GlossAffectPlayers ?? def.glossAffectPlayers;
            p.glossAffectPucks = GlossAffectPucks ?? def.glossAffectPucks;

            ReskinProfileManager.SaveProfile();
        }

        private static ReskinRegistry.ReskinEntry FindEntry(ReskinProfileManager.ReskinReference reference, string type)
        {
            if (reference == null || string.IsNullOrEmpty(reference.PackId))
                return null;

            var pack = ReskinRegistry.reskinPacks.FirstOrDefault(pack => pack.UniqueId == reference.PackId);
            if (pack == null) return null;

            return pack.Reskins?.FirstOrDefault(entry => entry.Name == reference.EntryName && entry.Type == type);
        }

        private static ReskinProfileManager.ReskinReference CreateRef(ReskinRegistry.ReskinEntry entry)
        {
            if (entry?.ParentPack == null)
                return null;

            return new ReskinProfileManager.ReskinReference
            {
                PackId = entry.ParentPack.UniqueId,
                EntryName = entry.Name,
                WorkshopId = entry.ParentPack.WorkshopId,
            };
        }
    }

    public class PresetInfo
    {
        public string Name { get; }
        public string FilePath { get; }
        public string SavedAt { get; }

        public PresetInfo(string name, string filePath, string savedAt)
        {
            Name = name;
            FilePath = filePath;
            SavedAt = savedAt;
        }

        public string GetDisplayDate()
        {
            if (DateTime.TryParse(SavedAt, out var dt))
                return dt.ToLocalTime().ToString("g");
            return SavedAt ?? "Unknown";
        }
    }
}
