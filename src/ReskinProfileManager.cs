// ReskinProfileManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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

        try
        {
            Plugin.Log($"Loading reskin profile from: {ProfilePath}");
            string json = File.ReadAllText(ProfilePath);

            // One-time safety backup of the pre-existing file before the new serializer ever
            // rewrites it, so an upgrade can always be rolled back by hand.
            WriteOneTimeBackup(json);

            // The Profile class is now the on-disk shape (see ProfileContractResolver); fields
            // absent from the JSON keep their initializer defaults, replacing the old per-field
            // "?? defaultProfile.x" hydration.
            var loaded = JsonConvert.DeserializeObject<Profile>(json, ProfileSerializerSettings);
            if (loaded == null)
            {
                Plugin.LogError("Failed to deserialize profile (file might be empty or corrupt). Loading default profile.");
                currentProfile = new Profile();
                return;
            }

            ApplyLegacyMigrations(loaded, json);
            currentProfile = loaded;
            Plugin.Log("Reskin profile loaded successfully.");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"Failed to load reskin profile: {ex.Message}. Creating a new default profile.");
            currentProfile = new Profile(); // Fallback to a default profile on error
        }
    }

    /// <summary>
    /// Writes a one-time .bak copy of the existing profile JSON the first time the new
    /// serializer runs, so the pre-upgrade file is always recoverable.
    /// </summary>
    private static void WriteOneTimeBackup(string json)
    {
        try
        {
            string backupPath = ProfilePath + ".bak";
            if (!File.Exists(backupPath))
            {
                File.WriteAllText(backupPath, json);
                Plugin.Log($"Wrote one-time profile backup: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogWarning($"Could not write profile backup: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies backward-compatibility migrations that aren't a simple field rename, reading the
    /// raw JSON for legacy keys the current Profile no longer declares.
    /// </summary>
    private static void ApplyLegacyMigrations(Profile profile, string rawJson)
    {
        JObject root;
        try { root = JObject.Parse(rawJson); }
        catch { return; }

        // Legacy single "teamColorsEnabled" toggle -> per-team flags. Only applied when the
        // per-team key was absent from the file (an explicit per-team value always wins).
        var legacyTeamColors = root["teamColorsEnabled"];
        if (legacyTeamColors != null && legacyTeamColors.Type == JTokenType.Boolean)
        {
            bool legacy = legacyTeamColors.Value<bool>();
            if (root["blueTeamColorEnabled"] == null) profile.blueTeamColorEnabled = legacy;
            if (root["redTeamColorEnabled"] == null) profile.redTeamColorEnabled = legacy;
        }

        // Legacy single puck -> randomizer list. The puck-list converter already populated
        // puckList from puckListRef; migrate the old single puckRef only when there was no list.
        bool hadPuckList = root["puckListRef"] is JArray arr && arr.Count > 0;
        if (!hadPuckList && profile.puck != null)
        {
            profile.puckList = new List<ReskinRegistry.ReskinEntry> { profile.puck };
            Plugin.Log("Migrated old single puck entry to new puck randomizer list");
        }
    }

    public static void SaveProfile()
    {
        try
        {
            // Profile serializes directly to the legacy wire shape via ProfileContractResolver.
            string json = JsonConvert.SerializeObject(currentProfile, ProfileSerializerSettings);
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
    public static List<ReskinRegistry.ReskinEntry> GetAllActiveReskinEntries()
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

        // Find the entry within that pack with the matching name and type. Prefer the type stored
        // in the reference (new profiles); fall back to the per-field type passed by the caller for
        // older profiles that predate reskinType being persisted.
        string lookupType = string.IsNullOrEmpty(reference.ReskinType) ? type : reference.ReskinType;
        var entry = pack.Reskins.FirstOrDefault(e => e.Name == reference.EntryName && e.Type == lookupType);

        if (entry == null)
        {
            Plugin.LogWarning($"Could not find reskin entry named '{reference.EntryName}' in pack '{pack.Name}'. The entry may have been removed from the pack.");
            return null; // Entry not found in pack
        }

        return entry;
    }
    
    // Sentinel pack id used to persist the "Default" (vanilla) puck in the randomizer list.
    // The Default entry has no ParentPack, so a normal reference can't be created for it;
    // this sentinel lets it round-trip through save/load instead of being silently dropped.
    private const string DefaultPuckPackId = "__trl_default_puck__";

    /// <summary>True if the entry represents the vanilla/default puck (no pack, no texture).</summary>
    public static bool IsDefaultPuckEntry(ReskinRegistry.ReskinEntry entry) =>
        entry != null && entry.ParentPack == null && string.IsNullOrEmpty(entry.Path);

    /// <summary>Creates a fresh "Default" puck entry for the randomizer list.</summary>
    public static ReskinRegistry.ReskinEntry CreateDefaultPuckEntry() =>
        new ReskinRegistry.ReskinEntry { Name = "Default", Path = null, Type = "puck" };

    /// <summary>
    /// Serializes a puck-list entry, emitting a sentinel reference for the Default puck
    /// (which CreateReferenceFromEntry can't represent because it has no parent pack).
    /// </summary>
    private static ReskinReference CreatePuckReference(ReskinRegistry.ReskinEntry entry)
    {
        if (IsDefaultPuckEntry(entry))
            return new ReskinReference { PackId = DefaultPuckPackId, EntryName = "Default", ReskinType = "puck" };
        return CreateReferenceFromEntry(entry);
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
            ReskinType = entry.Type,
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

        currentProfile.blueTeamColorEnabled = defaultValues.blueTeamColorEnabled;
        currentProfile.redTeamColorEnabled = defaultValues.redTeamColorEnabled;
        currentProfile.blueTeamColor = defaultValues.blueTeamColor;
        currentProfile.redTeamColor = defaultValues.redTeamColor;
        currentProfile.blueTeamName = defaultValues.blueTeamName;
        currentProfile.redTeamName = defaultValues.redTeamName;

        SaveProfile();

        ToasterReskinLoaderAPI.NotifyTeamColorsChanged();
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

    // Minimap reset moved to the QoL profile (MinimapSection).

    // ----------------------------------------------------------------------------------
    //  Serialization
    //
    //  The Profile class below IS the on-disk shape. A custom contract resolver maps each
    //  field to the exact wire format the mod has always used, so existing ReskinProfile.json
    //  files load unchanged:
    //    * field name == JSON key (camelCase), e.g. iceSmoothness, blueTeamColor
    //    * ReskinEntry / List<ReskinEntry> fields get a "Ref" suffix (stickAttackerBlueRef)
    //      and serialize as ReskinReference(s) instead of the live object
    //    * Color fields serialize as { r, g, b, a }
    //  Fields absent from the JSON keep their Profile() initializer defaults (Newtonsoft only
    //  overwrites members it finds), which replaces the old "?? defaultProfile.x" hydration.
    //  This collapses the former Profile + SerializableProfile + LoadProfile + SaveProfile
    //  quadruple-mirror into a single source of truth: the annotated Profile fields.
    // ----------------------------------------------------------------------------------

    private static readonly JsonSerializerSettings ProfileSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new ProfileContractResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
    };

    private class ProfileContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            // base supplies public properties (e.g. ReskinReference); Profile and
            // SerializableColor use public fields, so include those too.
            var members = base.GetSerializableMembers(objectType);
            foreach (var f in objectType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!members.Contains(f)) members.Add(f);
            return members;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            var fieldType = (member as FieldInfo)?.FieldType;

            if (fieldType == typeof(ReskinRegistry.ReskinEntry))
            {
                // Live entry -> "<name>Ref" as a ReskinReference. The reskin type comes from the
                // field's [PresetField] metadata (single source of truth), with a fallback for the
                // legacy untagged 'puck' field.
                prop.PropertyName = member.Name + "Ref";
                string type = member.GetCustomAttribute<PresetFieldAttribute>()?.ReskinType;
                if (string.IsNullOrEmpty(type) && member.Name == "puck") type = "puck";
                if (string.IsNullOrEmpty(type))
                    Plugin.LogWarning($"[Profile] ReskinEntry field '{member.Name}' has no reskin type; older-profile lookups for it may fail.");
                prop.Converter = new ReskinEntryRefConverter(type);
                prop.Readable = prop.Writable = true;
            }
            else if (fieldType == typeof(List<ReskinRegistry.ReskinEntry>))
            {
                prop.PropertyName = member.Name + "Ref";
                prop.Converter = new PuckListConverter();
                prop.Readable = prop.Writable = true;
            }
            else if (fieldType == typeof(Color))
            {
                prop.Converter = new ColorJsonConverter();
                prop.Readable = prop.Writable = true;
            }
            else if (member is FieldInfo)
            {
                prop.Readable = prop.Writable = true;
            }
            return prop;
        }
    }

    /// <summary>UnityEngine.Color &lt;-&gt; { r, g, b, a }, matching the legacy SerializableColor shape.</summary>
    private class ColorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Color);

        public override void WriteJson(JsonWriter w, object value, JsonSerializer s)
        {
            var c = (Color)value;
            w.WriteStartObject();
            w.WritePropertyName("r"); w.WriteValue(c.r);
            w.WritePropertyName("g"); w.WriteValue(c.g);
            w.WritePropertyName("b"); w.WriteValue(c.b);
            w.WritePropertyName("a"); w.WriteValue(c.a);
            w.WriteEndObject();
        }

        public override object ReadJson(JsonReader r, Type t, object existingValue, JsonSerializer s)
        {
            // Null/absent -> keep the field's initializer default (matches old "?? default").
            if (r.TokenType == JsonToken.Null) return existingValue;
            var o = JObject.Load(r);
            return new Color(
                o["r"]?.Value<float>() ?? 0f,
                o["g"]?.Value<float>() ?? 0f,
                o["b"]?.Value<float>() ?? 0f,
                o["a"]?.Value<float>() ?? 1f);
        }
    }

    /// <summary>Live ReskinEntry &lt;-&gt; ReskinReference; resolves against the registry on read.</summary>
    private class ReskinEntryRefConverter : JsonConverter
    {
        private readonly string _fieldType;
        public ReskinEntryRefConverter(string fieldType) { _fieldType = fieldType; }

        public override bool CanConvert(Type t) => t == typeof(ReskinRegistry.ReskinEntry);

        public override void WriteJson(JsonWriter w, object value, JsonSerializer s)
        {
            var reference = CreateReferenceFromEntry(value as ReskinRegistry.ReskinEntry);
            if (reference == null) { w.WriteNull(); return; }
            s.Serialize(w, reference);
        }

        public override object ReadJson(JsonReader r, Type t, object existingValue, JsonSerializer s)
        {
            if (r.TokenType == JsonToken.Null) return null;
            var reference = s.Deserialize<ReskinReference>(r);
            return FindEntryFromReference(reference, _fieldType);
        }
    }

    /// <summary>Randomizer puck list &lt;-&gt; list of ReskinReference, preserving the Default sentinel.</summary>
    private class PuckListConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(List<ReskinRegistry.ReskinEntry>);

        public override void WriteJson(JsonWriter w, object value, JsonSerializer s)
        {
            var list = value as List<ReskinRegistry.ReskinEntry> ?? new List<ReskinRegistry.ReskinEntry>();
            w.WriteStartArray();
            foreach (var entry in list)
            {
                var reference = CreatePuckReference(entry);
                if (reference != null) s.Serialize(w, reference);
            }
            w.WriteEndArray();
        }

        public override object ReadJson(JsonReader r, Type t, object existingValue, JsonSerializer s)
        {
            var result = new List<ReskinRegistry.ReskinEntry>();
            if (r.TokenType == JsonToken.Null) return result;
            var arr = JArray.Load(r);
            foreach (var item in arr)
            {
                var reference = item.ToObject<ReskinReference>();
                if (reference?.PackId == DefaultPuckPackId) { result.Add(CreateDefaultPuckEntry()); continue; }
                var entry = FindEntryFromReference(reference, "puck");
                if (entry != null) result.Add(entry);
            }
            return result;
        }
    }

    public class Profile
    {
        // Sticks section
        [PresetField("Sticks", "Team stick", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerBlue;
        [PresetField("Sticks", "Personal stick", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerBluePersonal;
        [PresetField("Sticks", "Team stick", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerRed;
        [PresetField("Sticks", "Personal stick", ReskinType = "stick_attacker")]
        public ReskinRegistry.ReskinEntry stickAttackerRedPersonal;
        [PresetField("Sticks", "Team stick", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieBlue;
        [PresetField("Sticks", "Personal stick", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieBluePersonal;
        [PresetField("Sticks", "Team stick", ReskinType = "stick_goalie")]
        public ReskinRegistry.ReskinEntry stickGoalieRed;
        [PresetField("Sticks", "Personal stick", ReskinType = "stick_goalie")]
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
        [PresetField("Goalies", "Left pad", ReskinType = "legpad", Role = PresetRole.Goalie)]
        public ReskinRegistry.ReskinEntry blueLegPadLeft;
        [PresetField("Goalies", "Right pad", ReskinType = "legpad", Role = PresetRole.Goalie)]
        public ReskinRegistry.ReskinEntry blueLegPadRight;
        [PresetField("Goalies", "Left pad", ReskinType = "legpad", Role = PresetRole.Goalie)]
        public ReskinRegistry.ReskinEntry redLegPadLeft;
        [PresetField("Goalies", "Right pad", ReskinType = "legpad", Role = PresetRole.Goalie)]
        public ReskinRegistry.ReskinEntry redLegPadRight;
        [PresetField("Goalies", "Pad default color", Role = PresetRole.Goalie)]
        public Color blueLegPadDefaultColor = new Color(0.151f, 0.151f, 0.151f, 1f);
        [PresetField("Goalies", "Pad default color", Role = PresetRole.Goalie)]
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
        [PresetField("Tape", "Blade mode")]
        public string blueSkaterBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Blade", ReskinType = "tape_attacker_blade")]
        public ReskinRegistry.ReskinEntry blueSkaterBladeTape;
        [PresetField("Tape", "Blade color")]
        public Color blueSkaterBladeTapeColor = Color.white;

        [PresetField("Tape", "Shaft mode")]
        public string blueSkaterShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Shaft", ReskinType = "tape_attacker_shaft")]
        public ReskinRegistry.ReskinEntry blueSkaterShaftTape;
        [PresetField("Tape", "Shaft color")]
        public Color blueSkaterShaftTapeColor = Color.white;

        // Blue Team Goalie
        [PresetField("Tape", "Blade mode")]
        public string blueGoalieBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Blade", ReskinType = "tape_goalie_blade")]
        public ReskinRegistry.ReskinEntry blueGoalieBladeTape;
        [PresetField("Tape", "Blade color")]
        public Color blueGoalieBladeTapeColor = Color.white;

        [PresetField("Tape", "Shaft mode")]
        public string blueGoalieShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Shaft", ReskinType = "tape_goalie_shaft")]
        public ReskinRegistry.ReskinEntry blueGoalieShaftTape;
        [PresetField("Tape", "Shaft color")]
        public Color blueGoalieShaftTapeColor = Color.white;

        // Red Team Skater
        [PresetField("Tape", "Blade mode")]
        public string redSkaterBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Blade", ReskinType = "tape_attacker_blade")]
        public ReskinRegistry.ReskinEntry redSkaterBladeTape;
        [PresetField("Tape", "Blade color")]
        public Color redSkaterBladeTapeColor = Color.white;

        [PresetField("Tape", "Shaft mode")]
        public string redSkaterShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Shaft", ReskinType = "tape_attacker_shaft")]
        public ReskinRegistry.ReskinEntry redSkaterShaftTape;
        [PresetField("Tape", "Shaft color")]
        public Color redSkaterShaftTapeColor = Color.white;

        // Red Team Goalie
        [PresetField("Tape", "Blade mode")]
        public string redGoalieBladeTapeMode = "Unchanged";
        [PresetField("Tape", "Blade", ReskinType = "tape_goalie_blade")]
        public ReskinRegistry.ReskinEntry redGoalieBladeTape;
        [PresetField("Tape", "Blade color")]
        public Color redGoalieBladeTapeColor = Color.white;

        [PresetField("Tape", "Shaft mode")]
        public string redGoalieShaftTapeMode = "Unchanged";
        [PresetField("Tape", "Shaft", ReskinType = "tape_goalie_shaft")]
        public ReskinRegistry.ReskinEntry redGoalieShaftTape;
        [PresetField("Tape", "Shaft color")]
        public Color redGoalieShaftTapeColor = Color.white;

        // Team colors (per-team enable — replaced the single teamColorsEnabled toggle)
        [PresetField("Team Colors", "Custom color enabled")]
        public bool blueTeamColorEnabled = false;
        [PresetField("Team Colors", "Custom color enabled")]
        public bool redTeamColorEnabled = false;
        [PresetField("Team Colors", "Color")]
        public Color blueTeamColor = new Color(0.231f, 0.510f, 0.965f, 1f); // #3b82f6
        [PresetField("Team Colors", "Color")]
        public Color redTeamColor = new Color(0.820f, 0.200f, 0.200f, 1f);  // #d13333
        // teamIndicatorEnabled moved to the QoL profile — see SettingsConfig.
        [PresetField("Team Colors", "Name")]
        public string blueTeamName = "";
        [PresetField("Team Colors", "Name")]
        public string redTeamName = "";

        // Minimap moved to the QoL profile (HUD) — see SettingsConfig.

        // Chat moved to the QoL profile (HUD) — see SettingsConfig.

        // Shadows moved to the QoL profile (personal/perf) — see SettingsConfig.

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
        public float puckFXTrailStartAlpha = 1f;
        [PresetField("Puck FX", "Trail end alpha")]
        public float puckFXTrailEndAlpha = 0f;
        [PresetField("Puck FX", "Silhouette color")]
        public Color puckFXSilhouetteColor = new Color(1f, 1f, 1f, 0.502f);

        // QoL config lives in its own side-car files now (reskinprofiles/
        // QoL.json + ServerPrefs.json) so visual profiles stay shareable
        // without leaking toggles or per-server credentials. See SettingsStorage.

        // Gloss moved to the QoL profile (personal/perf) — see SettingsConfig.
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

        // The reskin type (e.g. "stick_attacker") is now stored alongside the reference so
        // lookups don't depend on the caller knowing the field's type. Older profiles omit it
        // (NullValueHandling.Ignore keeps new files clean too); on load we fall back to the
        // per-field type supplied by the contract resolver. Fixes the long-standing lookup TODO.
        [JsonProperty("reskinType", NullValueHandling = NullValueHandling.Ignore)]
        public string ReskinType { get; set; }
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