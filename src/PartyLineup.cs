using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Steamworks;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader;

/// <summary>
/// Shows party members' player models behind the local player in the locker room.
/// Clones the existing LockerRoomPlayer/LockerRoomStick scene objects and applies
/// fetched appearance data + randomized cosmetics.
/// </summary>
public static class PartyLineup
{
    // ── Tracking ────────────────────────────────────────────────────
    private class PartyMemberSlot
    {
        public LockerRoomPlayer PlayerClone;
        public LockerRoomStick StickClone;
        public string SteamId;
        public ulong Key; // unique key for GenderSwapper/HatSwapper tracking
    }

    private static readonly Dictionary<string, PartyMemberSlot> slots = new();
    private static readonly HashSet<LockerRoomPlayer> partyPlayerClones = new();
    private static readonly HashSet<LockerRoomStick> partyStickClones = new();
    private static bool initialized;

    // Reflection for accessing private/serialized fields on clones
    private static readonly FieldInfo _playerMeshField = typeof(LockerRoomPlayer)
        .GetField("playerMesh", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _attackerStickMeshField = typeof(LockerRoomStick)
        .GetField("attackerStickMesh", BindingFlags.Instance | BindingFlags.NonPublic);

    // For reading valid IDs from serialized lists
    private static readonly FieldInfo _jerseysField = typeof(PlayerTorso)
        .GetField("jerseys", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _headgearField = typeof(PlayerHead)
        .GetField("headgear", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _skinsField = typeof(StickMesh)
        .GetField("skins", BindingFlags.Instance | BindingFlags.NonPublic);

    // ── Slot positions (V-formation behind the main player) ─────────
    private static readonly Vector3[] SlotPositions = new Vector3[]
    {
        new Vector3(-1.8f, 0f, -1.2f),   // back-left
        new Vector3(1.8f, 0f, -1.2f),    // back-right
        new Vector3(-3.2f, 0f, -2.2f),   // far back-left
        new Vector3(3.2f, 0f, -2.2f),    // far back-right
    };

    // Slight rotation offsets so they don't all face exactly the same way
    private static readonly float[] SlotRotationY = new float[] { 5f, -5f, 10f, -10f };

    // Known cosmetic IDs for randomization
    private static readonly int[] BeardIds = { -1, -1, -1, 1536, 1537, 1538, 1539, 1540 };
    private static readonly int[] MustacheIds = { -1, -1, -1, 1024, 1025, 1026, 1027, 1029, 1030 };

    // ── Public API for clone filtering ──────────────────────────────

    public static bool IsPartyPlayerClone(LockerRoomPlayer player) => partyPlayerClones.Contains(player);
    public static bool IsPartyStickClone(LockerRoomStick stick) => partyStickClones.Contains(stick);

    // ── Lifecycle ───────────────────────────────────────────────────

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        EventManager.AddEventListener("Event_OnPlayerPartyDataChanged",
            new Action<Dictionary<string, object>>(OnPartyDataChanged));
    }

    public static void Cleanup()
    {
        DestroyAll();
        if (initialized)
        {
            EventManager.RemoveEventListener("Event_OnPlayerPartyDataChanged",
                new Action<Dictionary<string, object>>(OnPartyDataChanged));
            initialized = false;
        }
    }

    /// <summary>
    /// Called from SwapperManager.OnSceneLoaded.
    /// Rebuilds lineup when returning to locker room, destroys when leaving.
    /// </summary>
    public static void OnSceneChanged(string sceneName, MonoBehaviour runner)
    {
        if (sceneName == "locker_room")
        {
            // Delay one frame so scene objects are ready
            runner.StartCoroutine(RebuildAfterDelay());
        }
        else
        {
            DestroyAll();
        }
    }

    private static IEnumerator RebuildAfterDelay()
    {
        yield return null; // wait one frame
        var partyData = BackendManager.PlayerState.PartyData;
        if (partyData?.memberSteamIds != null)
            RebuildLineup(partyData.memberSteamIds);
    }

    // ── Event Handlers ──────────────────────────────────────────────

    private static void OnPartyDataChanged(Dictionary<string, object> message)
    {
        if (!ChangingRoomHelper.IsInMainMenu()) return;

        var newPartyData = message.ContainsKey("newPlayerPartyData")
            ? message["newPlayerPartyData"] as PlayerPartyData
            : null;

        if (newPartyData?.memberSteamIds == null)
        {
            DestroyAll();
            return;
        }

        RebuildLineup(newPartyData.memberSteamIds);
    }

    // ── Core Logic ──────────────────────────────────────────────────

    private static void RebuildLineup(string[] memberSteamIds)
    {
        if (!ChangingRoomHelper.IsInMainMenu()) return;

        // Filter out the local player
        string localSteamId = SteamUser.GetSteamID().ToString();
        var otherMembers = memberSteamIds
            .Where(id => id != localSteamId)
            .Take(4) // max 4 slots
            .ToList();

        // Remove slots for members who left
        var toRemove = slots.Keys.Where(id => !otherMembers.Contains(id)).ToList();
        foreach (var id in toRemove)
            DestroySlot(id);

        // Create slots for new members
        var newIds = new List<string>();
        int slotIndex = 0;
        foreach (var steamId in otherMembers)
        {
            if (!slots.ContainsKey(steamId))
            {
                CreateSlot(steamId, slotIndex);
                newIds.Add(steamId);
            }
            slotIndex++;
        }

        // Fetch appearances for new members
        if (newIds.Count > 0)
        {
            AppearanceAPI.GetAppearances(newIds, results =>
            {
                foreach (var kvp in results)
                {
                    if (kvp.Value != null && slots.TryGetValue(kvp.Key, out var slot))
                        ApplyAppearance(slot, kvp.Value);
                }
            });
        }

        Plugin.Log($"[PartyLineup] Lineup updated: {slots.Count} party members shown");
    }

    private static void CreateSlot(string steamId, int slotIndex)
    {
        ChangingRoomHelper.Scan();

        // Find the original player and stick to clone
        var origPlayer = Object.FindObjectsByType<LockerRoomPlayer>(FindObjectsSortMode.None)
            .FirstOrDefault(p => !partyPlayerClones.Contains(p));
        var origStick = Object.FindObjectsByType<LockerRoomStick>(FindObjectsSortMode.None)
            .FirstOrDefault(s => !partyStickClones.Contains(s));

        if (origPlayer == null)
        {
            Plugin.LogError("[PartyLineup] Could not find original LockerRoomPlayer to clone");
            return;
        }

        // Clone player
        var playerClone = Object.Instantiate(origPlayer);
        playerClone.enabled = false; // disable Update() (mouse rotation)
        // Destroy the controller immediately so it doesn't register duplicate event listeners
        // or re-apply local player settings in Start(). OnDestroy unregisters the events.
        var playerController = playerClone.GetComponent<LockerRoomPlayerController>();
        if (playerController != null) Object.DestroyImmediate(playerController);
        partyPlayerClones.Add(playerClone);

        // Position relative to the original
        Vector3 basePos = origPlayer.transform.position;
        Vector3 offset = slotIndex < SlotPositions.Length
            ? SlotPositions[slotIndex] : new Vector3(0, 0, -3f);
        playerClone.transform.position = basePos + offset;

        float rotY = origPlayer.transform.rotation.eulerAngles.y +
            (slotIndex < SlotRotationY.Length ? SlotRotationY[slotIndex] : 0f);
        playerClone.transform.rotation = Quaternion.Euler(0, rotY, 0);

        // The LockerRoomStick is a child of the LockerRoomPlayer in the scene,
        // so Instantiate(origPlayer) already cloned it. Find it inside the clone.
        LockerRoomStick stickClone = playerClone.GetComponentInChildren<LockerRoomStick>();
        if (stickClone != null)
        {
            stickClone.enabled = false;
            var stickController = stickClone.GetComponent<LockerRoomStickController>();
            if (stickController != null) Object.DestroyImmediate(stickController);
            partyStickClones.Add(stickClone);
        }

        // Track the slot
        ulong key = ulong.MaxValue - (ulong)slotIndex;
        var slot = new PartyMemberSlot
        {
            PlayerClone = playerClone,
            StickClone = stickClone,
            SteamId = steamId,
            Key = key,
        };
        slots[steamId] = slot;

        // Set username
        string username = SteamIntegrationManager.GetUsername(steamId);
        if (!string.IsNullOrEmpty(username))
            playerClone.SetUsername(username);
        playerClone.SetNumber("");

        // Immediately show only attacker stick
        if (stickClone != null)
            stickClone.ShowRoleStick(PlayerRole.Attacker);

        // Delay cosmetic application by one frame so cloned renderers are fully initialized
        MonoBehaviourSingleton<UIManager>.Instance.StartCoroutine(ApplyCosmeticsDelayed(slot));

        Plugin.Log($"[PartyLineup] Created slot {slotIndex} for {username} ({steamId})");
    }

    private static IEnumerator ApplyCosmeticsDelayed(PartyMemberSlot slot)
    {
        yield return null; // wait one frame
        if (slot.PlayerClone == null) yield break;

        // Reset the clone to vanilla state first.
        // Instantiate copies TRL's modifications (gender swap meshes, custom textures, hats).
        // We need to undo those before applying the party member's own cosmetics.
        ResetCloneToVanilla(slot);

        yield return null; // another frame for cleanup to take effect
        if (slot.PlayerClone == null) yield break;

        RandomizeCosmetics(slot);
    }

    /// <summary>
    /// Removes TRL-spawned objects from the clone (gender swap meshes, hats)
    /// so we start from a clean vanilla state before applying party cosmetics.
    /// </summary>
    private static void ResetCloneToVanilla(PartyMemberSlot slot)
    {
        var playerMesh = _playerMeshField?.GetValue(slot.PlayerClone) as PlayerMesh;
        if (playerMesh == null) return;

        // Destroy any cloned gender swap objects (female torso/groin siblings)
        // GenderSwapper creates children named like the prefab — look for non-original meshes
        // under PlayerTorso and PlayerGroin
        DestroyNonOriginalMeshChildren(playerMesh.PlayerTorso?.transform, "torso");
        DestroyNonOriginalMeshChildren(playerMesh.PlayerGroin?.transform, "groin");

        // Re-enable the original torso/groin meshes (GenderSwapper may have disabled them)
        if (playerMesh.PlayerTorso != null)
        {
            var origTorso = playerMesh.PlayerTorso.transform.Find("torso");
            if (origTorso != null)
            {
                var renderer = origTorso.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = true;
            }
        }
        if (playerMesh.PlayerGroin != null)
        {
            var origGroin = playerMesh.PlayerGroin.transform.Find("groin");
            if (origGroin != null)
            {
                var renderer = origGroin.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = true;
            }
        }

        // Remove any cloned hat objects (they'll be children of the helmet or torso)
        // Hat GameObjects spawned by HatSwapper are tracked by key, but the clone
        // has a copy of the original's hat as an untracked child. Find and destroy it.
        DestroyHatClones(playerMesh);
    }

    private static void DestroyNonOriginalMeshChildren(Transform parent, string originalChildName)
    {
        if (parent == null) return;
        var toDestroy = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            // Keep only the original mesh child (e.g. "torso" or "groin").
            // Destroy anything else with a renderer — GenderSwapper clones like "torsoFemale(Clone)",
            // "torsoMale(Clone)", etc. Also skip non-mesh children (text, bones).
            if (child.name != originalChildName && child.name != "Username" && child.name != "Number"
                && (child.GetComponent<MeshRenderer>() != null || child.GetComponent<MeshFilter>() != null))
            {
                Plugin.LogDebug($"[PartyLineup] Destroying cloned mesh child: '{child.name}' under '{parent.name}'");
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var obj in toDestroy)
            Object.DestroyImmediate(obj);
    }

    private static void DestroyHatClones(PlayerMesh playerMesh)
    {
        if (playerMesh.PlayerHead == null) return;
        // Hats are attached to helmet transforms. They have names containing "(Clone)"
        // and are not part of the original headgear hierarchy.
        var renderers = playerMesh.PlayerHead.GetComponentsInChildren<Transform>(true);
        var toDestroy = new List<GameObject>();
        foreach (var t in renderers)
        {
            if (t.name.Contains("(Clone)") && t.parent != null)
                toDestroy.Add(t.gameObject);
        }
        foreach (var obj in toDestroy)
            Object.DestroyImmediate(obj);

        // Also check torso for torso-attached hats
        if (playerMesh.PlayerTorso != null)
        {
            var torsoChildren = playerMesh.PlayerTorso.GetComponentsInChildren<Transform>(true);
            toDestroy.Clear();
            foreach (var t in torsoChildren)
            {
                if (t.name.Contains("(Clone)") && t.parent == playerMesh.PlayerTorso.transform)
                    toDestroy.Add(t.gameObject);
            }
            foreach (var obj in toDestroy)
                Object.DestroyImmediate(obj);
        }
    }

    private static void RandomizeCosmetics(PartyMemberSlot slot)
    {
        var rng = new System.Random(slot.SteamId.GetHashCode());
        var playerMesh = _playerMeshField?.GetValue(slot.PlayerClone) as PlayerMesh;
        if (playerMesh == null) return;

        PlayerTeam team = rng.Next(2) == 0 ? PlayerTeam.Blue : PlayerTeam.Red;

        // Pick a random valid jersey ID from the serialized list
        try
        {
            var jerseys = _jerseysField?.GetValue(playerMesh.PlayerTorso) as System.Collections.IList;
            if (jerseys != null && jerseys.Count > 0)
            {
                var jersey = jerseys[rng.Next(jerseys.Count)];
                int jerseyId = (int)jersey.GetType().GetField("ID").GetValue(jersey);
                slot.PlayerClone.SetJerseyID(jerseyId, team);
            }
        }
        catch (Exception e) { Plugin.LogDebug($"[PartyLineup] Jersey randomize error: {e.Message}"); }

        // Pick a random valid headgear ID for attacker role
        try
        {
            var headgear = _headgearField?.GetValue(playerMesh.PlayerHead) as System.Collections.IList;
            if (headgear != null && headgear.Count > 0)
            {
                // Filter to attacker headgear
                var validIds = new List<int>();
                foreach (var h in headgear)
                {
                    var roleMethod = h.GetType().GetMethod("IsForRole");
                    if (roleMethod != null && (bool)roleMethod.Invoke(h, new object[] { PlayerRole.Attacker }))
                        validIds.Add((int)h.GetType().GetField("ID").GetValue(h));
                }
                if (validIds.Count > 0)
                    slot.PlayerClone.SetHeadgearID(validIds[rng.Next(validIds.Count)], PlayerRole.Attacker);
            }
        }
        catch (Exception e) { Plugin.LogDebug($"[PartyLineup] Headgear randomize error: {e.Message}"); }

        // Random facial hair
        slot.PlayerClone.SetBeardID(BeardIds[rng.Next(BeardIds.Length)]);
        slot.PlayerClone.SetMustacheID(MustacheIds[rng.Next(MustacheIds.Length)]);

        // Show attacker stick with a random valid skin
        if (slot.StickClone != null)
        {
            slot.StickClone.ShowRoleStick(PlayerRole.Attacker);
            try
            {
                var stickMesh = _attackerStickMeshField?.GetValue(slot.StickClone) as StickMesh;
                if (stickMesh != null)
                {
                    var skins = _skinsField?.GetValue(stickMesh) as System.Collections.IList;
                    if (skins != null && skins.Count > 0)
                    {
                        // Filter to matching team
                        var validIds = new List<int>();
                        foreach (var s in skins)
                        {
                            var teamMethod = s.GetType().GetMethod("IsForTeam");
                            if (teamMethod != null && (bool)teamMethod.Invoke(s, new object[] { team }))
                                validIds.Add((int)s.GetType().GetField("ID").GetValue(s));
                        }
                        if (validIds.Count > 0)
                            slot.StickClone.SetSkinID(validIds[rng.Next(validIds.Count)], team, PlayerRole.Attacker);
                    }
                }
            }
            catch (Exception e) { Plugin.LogDebug($"[PartyLineup] Stick randomize error: {e.Message}"); }
        }

        // Set leg pads inactive (attacker doesn't have them)
        slot.PlayerClone.SetLegsPadsActive(false);
    }

    private static void ApplyAppearance(PartyMemberSlot slot, AppearanceAPI.AppearanceData data)
    {
        if (slot.PlayerClone == null) return;

        try
        {
            var playerMesh = _playerMeshField?.GetValue(slot.PlayerClone) as PlayerMesh;
            if (playerMesh == null) return;

            // Body type
            GenderSwapper.ApplyToPlayerMesh(playerMesh, data.bodyType == 1, slot.Key);

            // Skin tone and hair color
            if (playerMesh.PlayerHead != null)
                GenderSwapper.ApplyHeadColors(playerMesh.PlayerHead, data.skinTone, data.hairColor);

            // Hat
            if (data.hatId > 0)
                HatSwapper.AttachToPlayerMesh(playerMesh, data.hatId, slot.Key);

            Plugin.LogDebug($"[PartyLineup] Applied appearance to {slot.SteamId}: body={data.bodyType}, hat={data.hatId}");
        }
        catch (Exception e)
        {
            Plugin.LogError($"[PartyLineup] Error applying appearance to {slot.SteamId}: {e.Message}");
        }
    }

    // ── Cleanup ─────────────────────────────────────────────────────

    private static void DestroySlot(string steamId)
    {
        if (!slots.TryGetValue(steamId, out var slot)) return;

        // Clean up GenderSwapper/HatSwapper tracking
        GenderSwapper.RemoveForKey(slot.Key);
        HatSwapper.RemoveFromPlayer(slot.Key);

        if (slot.PlayerClone != null)
        {
            partyPlayerClones.Remove(slot.PlayerClone);
            Object.Destroy(slot.PlayerClone.gameObject);
        }
        if (slot.StickClone != null)
        {
            partyStickClones.Remove(slot.StickClone);
            Object.Destroy(slot.StickClone.gameObject);
        }

        slots.Remove(steamId);
    }

    private static string GetPath(Transform t, Transform root)
    {
        var parts = new List<string>();
        while (t != null && t != root)
        {
            parts.Insert(0, t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    public static void DestroyAll()
    {
        foreach (var steamId in slots.Keys.ToList())
            DestroySlot(steamId);
        slots.Clear();
        partyPlayerClones.Clear();
        partyStickClones.Clear();
    }
}
