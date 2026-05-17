using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Steamworks;
using UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

/// <summary>
/// Friends list enhancement: rich-presence-aware status labels, server-name lookups,
/// status-aware sorting, and per-friend Join buttons.
///
/// Toggle via <see cref="Enable"/> / <see cref="Disable"/>. Will eventually be driven
/// by a QoL config option; for now the call site in Plugin.cs decides.
/// </summary>
public static class BetterFriendsList
{
    private static readonly Harmony _harmony = new Harmony(Plugin.MOD_GUID + ".bfl");

    // Cached reflection handles. Resolved once via AccessTools and reused so
    // the hot paths (SortFriends prefix, friends-list rebuild) don't pay the
    // GetField cost on every call.
    internal static readonly System.Reflection.FieldInfo UIFriendsController_uiFriends =
        AccessTools.Field(typeof(UIFriendsController), "uiFriends");
    internal static readonly System.Reflection.FieldInfo UIFriends_friendsMap =
        AccessTools.Field(typeof(UIFriends), "friendsMap");
    internal static readonly System.Reflection.FieldInfo UIFriends_friendsList =
        AccessTools.Field(typeof(UIFriends), "friendsList");

    public static bool IsEnabled { get; private set; }

    public static void Enable()
    {
        if (IsEnabled) return;

        _harmony.Patch(
            AccessTools.Method(typeof(UIFriendsController), "Event_OnSteamConnected"),
            prefix: new HarmonyMethod(typeof(BFL_SteamConnectedPatch), nameof(BFL_SteamConnectedPatch.Prefix)));
        _harmony.Patch(
            AccessTools.Method(typeof(UIFriendsController), "Event_OnPersonaStateChange"),
            prefix: new HarmonyMethod(typeof(BFL_PersonaStateChangePatch), nameof(BFL_PersonaStateChangePatch.Prefix)));
        _harmony.Patch(
            AccessTools.Method(typeof(UIFriends), "SortFriends"),
            prefix: new HarmonyMethod(typeof(BFL_SortFriendsPatch), nameof(BFL_SortFriendsPatch.Prefix)));

        IsEnabled = true;
        Plugin.Log("BetterFriendsList enabled.");

        // Event_OnSteamConnected has likely already fired before we patched; force a refresh.
        try
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<UIFriendsController>();
            if (controller != null)
            {
                FriendsListHelper.RebuildFriendsList(controller);

                // Rich presence data arrives async — second refresh after a short delay.
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    BFLMainThreadDispatcher.Enqueue(() =>
                    {
                        try
                        {
                            var ctrl = UnityEngine.Object.FindAnyObjectByType<UIFriendsController>();
                            if (ctrl != null)
                                FriendsListHelper.RebuildFriendsList(ctrl);
                        }
                        catch (Exception ex)
                        {
                            Plugin.LogError($"BFL delayed refresh failed: {ex.Message}");
                        }
                    });
                });
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"BFL initial refresh failed: {ex.Message}");
        }
    }

    public static void Disable()
    {
        if (!IsEnabled) return;
        _harmony.UnpatchSelf();
        FriendsListHelper.FriendInfoCache.Clear();
        IsEnabled = false;
        Plugin.Log("BetterFriendsList disabled.");

        // Rebuild the friends list using the vanilla flow so the BFL-added
        // status/detail labels and Join buttons disappear immediately.
        try
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<UIFriendsController>();
            if (controller == null)
                return;

            var uiFriends = BetterFriendsList.UIFriendsController_uiFriends
                .GetValue(controller) as UIFriends;
            if (uiFriends != null)
            {
                var friendsMap = BetterFriendsList.UIFriends_friendsMap
                    .GetValue(uiFriends) as Dictionary<string, TemplateContainer>;
                var friendsList = BetterFriendsList.UIFriends_friendsList
                    .GetValue(uiFriends) as VisualElement;
                if (friendsList != null) friendsList.Clear();
                if (friendsMap != null) friendsMap.Clear();
            }

            // Now-unpatched method runs the vanilla rebuild.
            AccessTools.Method(typeof(UIFriendsController), "Event_OnSteamConnected")
                .Invoke(controller, new object[] { new Dictionary<string, object>() });
        }
        catch (Exception ex)
        {
            Plugin.LogError($"BFL vanilla rebuild on disable failed: {ex.Message}");
        }
    }
}

public static class BFL_SteamConnectedPatch
{
    public static bool Prefix(UIFriendsController __instance, Dictionary<string, object> message)
    {
        if (ApplicationManager.IsDedicatedGameServer)
            return false;

        FriendsListHelper.RebuildFriendsList(__instance);
        return false;
    }
}

public static class BFL_PersonaStateChangePatch
{
    public static bool Prefix(UIFriendsController __instance, Dictionary<string, object> message)
    {
        if (ApplicationManager.IsDedicatedGameServer)
            return false;

        string steamId = (string)message["steamId"];
        FriendsListHelper.UpdateSingleFriend(__instance, steamId);
        return false;
    }
}

public static class BFL_SortFriendsPatch
{
    public static bool Prefix(UIFriends __instance)
    {
        var friendsList = BetterFriendsList.UIFriends_friendsList
            .GetValue(__instance) as VisualElement;
        var friendsMap = BetterFriendsList.UIFriends_friendsMap
            .GetValue(__instance) as Dictionary<string, TemplateContainer>;

        if (friendsList == null || friendsMap == null)
            return true;

        var containerToSteamId = new Dictionary<VisualElement, string>();
        foreach (var kvp in friendsMap)
            containerToSteamId[kvp.Value] = kvp.Key;

        friendsList.Sort((a, b) =>
        {
            string steamIdA, steamIdB;
            containerToSteamId.TryGetValue(a, out steamIdA);
            containerToSteamId.TryGetValue(b, out steamIdB);

            FriendsListHelper.FriendInfo infoA = null, infoB = null;
            if (steamIdA != null) FriendsListHelper.FriendInfoCache.TryGetValue(steamIdA, out infoA);
            if (steamIdB != null) FriendsListHelper.FriendInfoCache.TryGetValue(steamIdB, out infoB);

            if (infoA == null && infoB == null) return 0;
            if (infoA == null) return 1;
            if (infoB == null) return -1;

            int cmp = infoB.IsInPuck.CompareTo(infoA.IsInPuck);
            if (cmp != 0) return cmp;

            bool aOnline = !infoA.IsInOtherGame && infoA.State == EPersonaState.k_EPersonaStateOnline;
            bool bOnline = !infoB.IsInOtherGame && infoB.State == EPersonaState.k_EPersonaStateOnline;
            cmp = bOnline.CompareTo(aOnline);
            if (cmp != 0) return cmp;

            bool aAway = infoA.State == EPersonaState.k_EPersonaStateBusy ||
                         infoA.State == EPersonaState.k_EPersonaStateAway ||
                         infoA.State == EPersonaState.k_EPersonaStateSnooze;
            bool bAway = infoB.State == EPersonaState.k_EPersonaStateBusy ||
                         infoB.State == EPersonaState.k_EPersonaStateAway ||
                         infoB.State == EPersonaState.k_EPersonaStateSnooze;
            cmp = bAway.CompareTo(aAway);
            if (cmp != 0) return cmp;

            bool aOffline = infoA.State == EPersonaState.k_EPersonaStateOffline;
            bool bOffline = infoB.State == EPersonaState.k_EPersonaStateOffline;
            cmp = aOffline.CompareTo(bOffline);
            if (cmp != 0) return cmp;

            return string.Compare(infoA.Username, infoB.Username, StringComparison.OrdinalIgnoreCase);
        });

        return false;
    }
}

public static class FriendsListHelper
{
    private static readonly Dictionary<string, ServerPreviewData> _serverCache = new Dictionary<string, ServerPreviewData>();
    private static readonly Dictionary<ushort, string> _portToEndpointKey = new Dictionary<ushort, string>();
    private static bool _refreshInProgress = false;

    public static readonly Dictionary<string, FriendInfo> FriendInfoCache = new Dictionary<string, FriendInfo>();

    public static void RebuildFriendsList(UIFriendsController controller)
    {
        var uiFriends = BetterFriendsList.UIFriendsController_uiFriends
            .GetValue(controller) as UIFriends;
        if (uiFriends == null)
            return;

        var friendsMap = BetterFriendsList.UIFriends_friendsMap
            .GetValue(uiFriends) as Dictionary<string, TemplateContainer>;
        var friendsList = BetterFriendsList.UIFriends_friendsList
            .GetValue(uiFriends) as VisualElement;

        if (friendsMap != null && friendsList != null)
        {
            friendsList.Clear();
            friendsMap.Clear();
        }

        if (!SteamManager.IsInitialized)
            return;

        var puckAppId = SteamUtils.GetAppID();
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        var friends = new List<FriendInfo>();

        for (int i = 0; i < friendCount; i++)
        {
            var steamId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            var state = SteamFriends.GetFriendPersonaState(steamId);

            string username = SteamFriends.GetFriendPersonaName(steamId);
            string steamIdStr = steamId.ToString();

            FriendGameInfo_t gameInfo;
            bool isInGame = SteamFriends.GetFriendGamePlayed(steamId, out gameInfo);
            bool isInPuck = isInGame && gameInfo.m_gameID.AppID() == puckAppId;

            SteamFriends.RequestFriendRichPresence(steamId);

            PuckPresence presence = null;
            if (isInPuck)
            {
                presence = new PuckPresence
                {
                    Status = SteamFriends.GetFriendRichPresence(steamId, "status"),
                    Team = SteamFriends.GetFriendRichPresence(steamId, "team"),
                    Role = SteamFriends.GetFriendRichPresence(steamId, "role"),
                    Score = SteamFriends.GetFriendRichPresence(steamId, "score"),
                    Phase = SteamFriends.GetFriendRichPresence(steamId, "phase"),
                    Connect = SteamFriends.GetFriendRichPresence(steamId, "connect"),
                    PlayerGroup = SteamFriends.GetFriendRichPresence(steamId, "steam_player_group"),
                    PlayerGroupSize = SteamFriends.GetFriendRichPresence(steamId, "steam_player_group_size"),
                };
            }

            friends.Add(new FriendInfo
            {
                SteamId = steamIdStr,
                Username = username,
                State = state,
                IsInPuck = isInPuck,
                IsInOtherGame = isInGame && !isInPuck,
                Presence = presence
            });
        }

        FriendInfoCache.Clear();
        foreach (var f in friends)
            FriendInfoCache[f.SteamId] = f;

        foreach (var friend in friends)
        {
            Texture2D avatar = SteamIntegrationManager.GetAvatar(friend.SteamId, AvatarSize.Medium);
            uiFriends.AddFriend(friend.SteamId, friend.Username, avatar);
        }

        friendsMap = BetterFriendsList.UIFriends_friendsMap
            .GetValue(uiFriends) as Dictionary<string, TemplateContainer>;

        if (friendsMap != null)
        {
            foreach (var friend in friends)
            {
                if (!friendsMap.ContainsKey(friend.SteamId))
                    continue;

                var container = friendsMap[friend.SteamId];
                ServerPreviewData cachedPreview = null;
                if (friend.Presence != null && !string.IsNullOrEmpty(friend.Presence.PlayerGroup))
                    cachedPreview = LookupServerPreview(friend.Presence.PlayerGroup);

                AddStatusLabels(container, friend, cachedPreview);
            }
        }

        bool needsServerNames = friends.Any(f =>
            f.IsInPuck && f.Presence != null &&
            !string.IsNullOrEmpty(f.Presence.PlayerGroup) &&
            LookupServerPreview(f.Presence.PlayerGroup) == null);

        if (needsServerNames && !_refreshInProgress)
        {
            _refreshInProgress = true;

            var uiFriendsRef = uiFriends;
            var friendsCopy = friends;

            ServerNameFetcher.FetchServerNames((results) =>
            {
                _refreshInProgress = false;

                lock (_serverCache)
                {
                    foreach (var kvp in results)
                    {
                        _serverCache[kvp.Key] = kvp.Value;
                        int colonIdx = kvp.Key.LastIndexOf(':');
                        if (colonIdx > 0)
                        {
                            ushort port;
                            if (ushort.TryParse(kvp.Key.Substring(colonIdx + 1), out port))
                                _portToEndpointKey[port] = kvp.Key;
                        }
                    }
                }

                UpdateServerLabels(uiFriendsRef, friendsCopy);
            });
        }
        else if (!needsServerNames)
        {
            UpdateServerLabels(uiFriends, friends);
        }
    }

    public static void UpdateSingleFriend(UIFriendsController controller, string steamId)
    {
        var uiFriends = BetterFriendsList.UIFriendsController_uiFriends
            .GetValue(controller) as UIFriends;
        if (uiFriends == null)
            return;

        bool isFriend = SteamIntegrationManager.IsFriend(steamId);
        bool isListed = uiFriends.IsFriendListed(steamId);

        if (!isFriend)
        {
            if (isListed)
            {
                uiFriends.RemoveFriend(steamId);
                FriendInfoCache.Remove(steamId);
            }
            return;
        }

        var puckAppId = SteamUtils.GetAppID();
        var sid = new CSteamID(ulong.Parse(steamId));

        FriendGameInfo_t gameInfo;
        bool isInGame = SteamFriends.GetFriendGamePlayed(sid, out gameInfo);
        bool isInPuck = isInGame && gameInfo.m_gameID.AppID() == puckAppId;

        SteamFriends.RequestFriendRichPresence(sid);

        PuckPresence presence = null;
        if (isInPuck)
        {
            presence = new PuckPresence
            {
                Status = SteamFriends.GetFriendRichPresence(sid, "status"),
                Team = SteamFriends.GetFriendRichPresence(sid, "team"),
                Role = SteamFriends.GetFriendRichPresence(sid, "role"),
                Score = SteamFriends.GetFriendRichPresence(sid, "score"),
                Phase = SteamFriends.GetFriendRichPresence(sid, "phase"),
                Connect = SteamFriends.GetFriendRichPresence(sid, "connect"),
                PlayerGroup = SteamFriends.GetFriendRichPresence(sid, "steam_player_group"),
                PlayerGroupSize = SteamFriends.GetFriendRichPresence(sid, "steam_player_group_size"),
            };
        }

        string username = SteamFriends.GetFriendPersonaName(sid);
        var state = SteamFriends.GetFriendPersonaState(sid);

        var info = new FriendInfo
        {
            SteamId = steamId,
            Username = username,
            State = state,
            IsInPuck = isInPuck,
            IsInOtherGame = isInGame && !isInPuck,
            Presence = presence
        };
        FriendInfoCache[steamId] = info;

        Texture2D avatar = SteamIntegrationManager.GetAvatar(steamId, AvatarSize.Medium);

        if (!isListed)
        {
            uiFriends.AddFriend(steamId, username, avatar);
        }
        else
        {
            uiFriends.UpdateFriend(steamId, username, avatar);
        }

        var friendsMap = BetterFriendsList.UIFriends_friendsMap
            .GetValue(uiFriends) as Dictionary<string, TemplateContainer>;

        if (friendsMap != null && friendsMap.ContainsKey(steamId))
        {
            var container = friendsMap[steamId];
            ServerPreviewData cachedPreview = null;
            if (presence != null && !string.IsNullOrEmpty(presence.PlayerGroup))
                cachedPreview = LookupServerPreview(presence.PlayerGroup);

            UpdateOrAddStatusLabels(container, info, cachedPreview);
        }

        AccessTools.Method(typeof(UIFriends), "SortFriends").Invoke(uiFriends, null);

        if (info.IsInPuck && presence != null &&
            !string.IsNullOrEmpty(presence.PlayerGroup) &&
            LookupServerPreview(presence.PlayerGroup) == null &&
            !_refreshInProgress)
        {
            _refreshInProgress = true;

            var uiFriendsRef = uiFriends;
            ServerNameFetcher.FetchServerNames((results) =>
            {
                _refreshInProgress = false;

                lock (_serverCache)
                {
                    foreach (var kvp in results)
                    {
                        _serverCache[kvp.Key] = kvp.Value;
                        int colonIdx = kvp.Key.LastIndexOf(':');
                        if (colonIdx > 0)
                        {
                            ushort port;
                            if (ushort.TryParse(kvp.Key.Substring(colonIdx + 1), out port))
                                _portToEndpointKey[port] = kvp.Key;
                        }
                    }
                }

                var currentMap = BetterFriendsList.UIFriends_friendsMap
                    .GetValue(uiFriendsRef) as Dictionary<string, TemplateContainer>;
                if (currentMap == null) return;

                foreach (var kvp in FriendInfoCache)
                {
                    var fi = kvp.Value;
                    if (!fi.IsInPuck || fi.Presence == null ||
                        string.IsNullOrEmpty(fi.Presence.PlayerGroup))
                        continue;

                    var preview = LookupServerPreview(fi.Presence.PlayerGroup);
                    if (preview == null) continue;

                    TemplateContainer container;
                    if (!currentMap.TryGetValue(fi.SteamId, out container)) continue;

                    var detailLabel = container.Q("DetailLabel");
                    if (detailLabel != null && detailLabel is Label label)
                    {
                        label.text = GetDetailText(fi, preview);
                    }
                }
            });
        }
    }

    private static void UpdateOrAddStatusLabels(TemplateContainer container, FriendInfo friend, ServerPreviewData preview)
    {
        string statusText = GetStatusText(friend);
        Color statusColor = GetStatusColor(friend);
        string detailText = GetDetailText(friend, preview);

        container.schedule.Execute(() =>
        {
            var friendElForMargin = container.Q<Friend>("Friend");
            if (friendElForMargin != null)
                ApplyInviteMargin(friendElForMargin);

            var statusLabel = container.Q<Label>("StatusLabel");
            if (statusLabel != null)
            {
                statusLabel.text = statusText;
                statusLabel.style.color = statusColor;

                var detailLabel = container.Q<Label>("DetailLabel");
                if (friend.IsInPuck)
                {
                    if (detailLabel != null)
                    {
                        detailLabel.text = detailText ?? "...";
                    }
                    else
                    {
                        var infoColumn = container.Q("InfoColumn");
                        if (infoColumn != null)
                        {
                            var newDetail = new Label(detailText ?? "...");
                            newDetail.name = "DetailLabel";
                            newDetail.style.fontSize = 9;
                            newDetail.style.color = new Color(0.7f, 0.7f, 0.7f);
                            newDetail.style.marginTop = -1;
                            newDetail.style.overflow = Overflow.Visible;
                            infoColumn.Add(newDetail);
                        }
                    }
                }
                else if (detailLabel != null)
                {
                    detailLabel.RemoveFromHierarchy();
                }

                var friendEl = container.Q<Friend>("Friend");
                if (friendEl != null)
                {
                    var existingJoin = friendEl.Q("JoinButton");
                    if (friend.IsInPuck && existingJoin == null)
                        AddJoinButton(friendEl, friend);
                    else if (!friend.IsInPuck && existingJoin != null)
                        existingJoin.RemoveFromHierarchy();
                }
                return;
            }

            AddStatusLabelsImmediate(container, friend, preview);
        });
    }

    private static void AddStatusLabelsImmediate(TemplateContainer container, FriendInfo friend, ServerPreviewData preview)
    {
        string statusText = GetStatusText(friend);
        Color statusColor = GetStatusColor(friend);
        string detailText = GetDetailText(friend, preview);

        var friendEl = container.Q<Friend>("Friend");
        if (friendEl == null)
            return;

        ApplyInviteMargin(friendEl);

        var userElement = friendEl.User;
        if (userElement == null)
            userElement = friendEl.Q<User>("User");
        if (userElement == null)
            return;

        var usernameLabel = userElement.Q<Label>("UsernameLabel");
        if (usernameLabel == null || userElement.Q("StatusLabel") != null)
            return;

        var parent = usernameLabel.parent;
        if (parent == null)
            return;

        int idx = parent.IndexOf(usernameLabel);

        var column = new VisualElement();
        column.name = "InfoColumn";
        column.style.flexDirection = FlexDirection.Column;
        column.style.flexGrow = 1;
        column.style.justifyContent = Justify.Center;

        parent.Remove(usernameLabel);
        column.Add(usernameLabel);

        var statusLabel = new Label(statusText);
        statusLabel.name = "StatusLabel";
        statusLabel.style.fontSize = 10;
        statusLabel.style.color = statusColor;
        statusLabel.style.marginTop = -2;
        column.Add(statusLabel);

        if (friend.IsInPuck)
        {
            var detailLabel = new Label(detailText ?? "...");
            detailLabel.name = "DetailLabel";
            detailLabel.style.fontSize = 9;
            detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            detailLabel.style.marginTop = -1;
            detailLabel.style.overflow = Overflow.Visible;
            column.Add(detailLabel);
        }

        parent.Insert(idx, column);

        AddJoinButton(friendEl, friend);

        container.style.height = StyleKeyword.Auto;
        container.style.overflow = Overflow.Visible;
        friendEl.style.height = StyleKeyword.Auto;
        friendEl.style.overflow = Overflow.Visible;

        var userContainer = container.Q("UserContainer");
        if (userContainer != null)
        {
            userContainer.style.height = StyleKeyword.Auto;
            userContainer.style.overflow = Overflow.Visible;
        }

        userElement.style.height = StyleKeyword.Auto;
        userElement.style.overflow = Overflow.Visible;
    }

    private static void ApplyInviteMargin(VisualElement friendEl)
    {
        var invite = friendEl.Q("InviteIconButtonContainer");
        if (invite != null)
            invite.style.marginRight = 16;
        else
            friendEl.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var inv = friendEl.Q("InviteIconButtonContainer");
                if (inv != null)
                    inv.style.marginRight = 16;
            });
    }

    private static void AddJoinButton(VisualElement friendEl, FriendInfo friend)
    {
        if (!friend.IsInPuck || friend.Presence == null)
            return;

        string pg = friend.Presence.PlayerGroup;
        if (string.IsNullOrEmpty(pg))
            return;

        int colonIdx = pg.LastIndexOf(':');
        if (colonIdx <= 0)
            return;

        ushort port;
        if (!ushort.TryParse(pg.Substring(colonIdx + 1), out port))
            return;

        if (friendEl.Q("JoinButtonContainer") != null)
            return;

        var inviteContainer = friendEl.Q("InviteIconButtonContainer");
        if (inviteContainer == null)
        {
            var capturedFriend = friend;
            var capturedEl = friendEl;
            friendEl.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (capturedEl.Q("JoinButtonContainer") != null) return;
                var invite = capturedEl.Q("InviteIconButtonContainer");
                if (invite != null)
                    InsertJoinButton(capturedEl, invite, capturedFriend);
            });
            return;
        }

        InsertJoinButton(friendEl, inviteContainer, friend);
    }

    private static void InsertJoinButton(VisualElement friendEl, VisualElement inviteContainer, FriendInfo friend)
    {
        string pg = friend.Presence.PlayerGroup;
        int colonIdx = pg.LastIndexOf(':');
        string ip = pg.Substring(0, colonIdx);
        ushort port;
        if (!ushort.TryParse(pg.Substring(colonIdx + 1), out port))
            return;

        var parent = inviteContainer.parent;
        if (parent == null)
            return;

        var joinBtn = new Button(() =>
        {
            Plugin.Log($"BFL: joining {ip}:{port}");
            EventManager.TriggerEvent("Event_OnMainMenuClickJoinServer", new Dictionary<string, object>
            {
                { "ipAddress", ip },
                { "port", port },
                { "password", "" }
            });
        });
        joinBtn.name = "JoinButtonContainer";
        joinBtn.text = "Join";

        foreach (var cls in inviteContainer.GetClasses())
            joinBtn.AddToClassList(cls);

        joinBtn.style.marginRight = 4;

        int inviteIdx = parent.IndexOf(inviteContainer);
        parent.Insert(inviteIdx, joinBtn);
    }

    private static void AddStatusLabels(TemplateContainer container, FriendInfo friend, ServerPreviewData preview)
    {
        string statusText = GetStatusText(friend);
        Color statusColor = GetStatusColor(friend);
        string detailText = GetDetailText(friend, preview);

        container.schedule.Execute(() =>
        {
            var friendEl = container.Q<Friend>("Friend");
            if (friendEl == null)
                return;

            ApplyInviteMargin(friendEl);

            var userElement = friendEl.User;
            if (userElement == null)
                userElement = friendEl.Q<User>("User");
            if (userElement == null)
                return;

            var usernameLabel = userElement.Q<Label>("UsernameLabel");
            if (usernameLabel == null || userElement.Q("StatusLabel") != null)
                return;

            var parent = usernameLabel.parent;
            if (parent == null)
                return;

            int idx = parent.IndexOf(usernameLabel);

            var column = new VisualElement();
            column.name = "InfoColumn";
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.justifyContent = Justify.Center;

            parent.Remove(usernameLabel);
            column.Add(usernameLabel);

            var statusLabel = new Label(statusText);
            statusLabel.name = "StatusLabel";
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = statusColor;
            statusLabel.style.marginTop = -2;

            column.Add(statusLabel);

            if (friend.IsInPuck)
            {
                var detailLabel = new Label(detailText ?? "...");
                detailLabel.name = "DetailLabel";
                detailLabel.style.fontSize = 9;
                detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                detailLabel.style.marginTop = -1;
                detailLabel.style.overflow = Overflow.Visible;
                column.Add(detailLabel);
            }

            parent.Insert(idx, column);

            AddJoinButton(friendEl, friend);

            container.style.height = StyleKeyword.Auto;
            container.style.overflow = Overflow.Visible;

            friendEl.style.height = StyleKeyword.Auto;
            friendEl.style.overflow = Overflow.Visible;

            var userContainer = container.Q("UserContainer");
            if (userContainer != null)
            {
                userContainer.style.height = StyleKeyword.Auto;
                userContainer.style.overflow = Overflow.Visible;
            }

            userElement.style.height = StyleKeyword.Auto;
            userElement.style.overflow = Overflow.Visible;
        });
    }

    public static ServerPreviewData LookupServerPreview(string playerGroup)
    {
        lock (_serverCache)
        {
            ServerPreviewData preview;
            if (_serverCache.TryGetValue(playerGroup, out preview))
                return preview;

            int colonIdx = playerGroup.LastIndexOf(':');
            if (colonIdx > 0)
            {
                ushort port;
                if (ushort.TryParse(playerGroup.Substring(colonIdx + 1), out port))
                {
                    string endpointKey;
                    if (_portToEndpointKey.TryGetValue(port, out endpointKey))
                    {
                        if (_serverCache.TryGetValue(endpointKey, out preview))
                            return preview;
                    }
                }
            }
        }
        return null;
    }

    private static void UpdateServerLabels(UIFriends uiFriends, List<FriendInfo> friends)
    {
        var friendsMap = BetterFriendsList.UIFriends_friendsMap
            .GetValue(uiFriends) as Dictionary<string, TemplateContainer>;

        if (friendsMap == null)
            return;

        foreach (var friend in friends)
        {
            if (friend.Presence == null || string.IsNullOrEmpty(friend.Presence.PlayerGroup))
                continue;

            if (!friendsMap.ContainsKey(friend.SteamId))
                continue;

            var preview = LookupServerPreview(friend.Presence.PlayerGroup);
            if (preview == null)
                continue;

            var container = friendsMap[friend.SteamId];
            var detailLabel = container.Q("DetailLabel");
            if (detailLabel != null && detailLabel is Label label)
            {
                label.text = GetDetailText(friend, preview);
            }
        }
    }

    private static string GetStatusText(FriendInfo friend)
    {
        if (friend.IsInPuck && friend.Presence != null)
        {
            string status = friend.Presence.Status;
            if (!string.IsNullOrEmpty(status))
                return status;
            return "Playing Puck";
        }
        if (friend.IsInOtherGame)
            return "In Other Game";

        switch (friend.State)
        {
            case EPersonaState.k_EPersonaStateOffline: return "Offline";
            case EPersonaState.k_EPersonaStateOnline: return "Online";
            case EPersonaState.k_EPersonaStateBusy: return "Busy";
            case EPersonaState.k_EPersonaStateAway: return "Away";
            case EPersonaState.k_EPersonaStateSnooze: return "Snooze";
            case EPersonaState.k_EPersonaStateLookingToTrade: return "Looking to Trade";
            case EPersonaState.k_EPersonaStateLookingToPlay: return "Looking to Play";
            default: return "Online";
        }
    }

    private static string GetDetailText(FriendInfo friend, ServerPreviewData preview)
    {
        if (!friend.IsInPuck || friend.Presence == null)
            return null;

        var parts = new List<string>();

        if (preview != null && !string.IsNullOrEmpty(preview.name))
            parts.Add(preview.name);

        string teamRole = "";
        if (!string.IsNullOrEmpty(friend.Presence.Team))
            teamRole += friend.Presence.Team;
        if (!string.IsNullOrEmpty(friend.Presence.Role))
        {
            if (teamRole.Length > 0) teamRole += " ";
            teamRole += friend.Presence.Role;
        }
        if (teamRole.Length > 0)
            parts.Add(teamRole);

        if (!string.IsNullOrEmpty(friend.Presence.Score))
        {
            string score = friend.Presence.Score.Trim().TrimStart('|').Trim();
            if (score.Length > 0)
                parts.Add(score);
        }

        if (preview != null)
            parts.Add($"{preview.players}/{preview.maxPlayers}");
        else if (!string.IsNullOrEmpty(friend.Presence.PlayerGroupSize))
            parts.Add(friend.Presence.PlayerGroupSize + (friend.Presence.PlayerGroupSize == "1" ? " player" : " players"));

        if (parts.Count == 0)
            return null;

        return string.Join(" | ", parts);
    }

    private static Color GetStatusColor(FriendInfo friend)
    {
        if (friend.IsInPuck) return new Color(0.4f, 0.9f, 0.4f);
        if (friend.IsInOtherGame) return new Color(0.6f, 0.8f, 0.6f);
        switch (friend.State)
        {
            case EPersonaState.k_EPersonaStateOffline: return new Color(0.5f, 0.5f, 0.5f);
            case EPersonaState.k_EPersonaStateOnline: return new Color(0.35f, 0.7f, 1f);
            case EPersonaState.k_EPersonaStateBusy: return new Color(1f, 0.5f, 0.5f);
            case EPersonaState.k_EPersonaStateAway:
            case EPersonaState.k_EPersonaStateSnooze: return new Color(1f, 0.8f, 0.3f);
            default: return new Color(0.35f, 0.7f, 1f);
        }
    }

    public class FriendInfo
    {
        public string SteamId;
        public string Username;
        public EPersonaState State;
        public bool IsInPuck;
        public bool IsInOtherGame;
        public PuckPresence Presence;
    }

    public class PuckPresence
    {
        public string Status;
        public string Team;
        public string Role;
        public string Score;
        public string Phase;
        public string Connect;
        public string PlayerGroup;
        public string PlayerGroupSize;
    }
}

/// <summary>
/// Fetches server names by directly TCP pinging the server addresses from friends' rich presence.
/// The game's TCP preview server runs on the same port as the game server.
/// </summary>
public static class ServerNameFetcher
{
    private static Action<Dictionary<string, ServerPreviewData>> _callback;

    public static void FetchServerNames(Action<Dictionary<string, ServerPreviewData>> callback)
    {
        _callback = callback;

        var endpoints = new HashSet<string>();
        foreach (var kvp in FriendsListHelper.FriendInfoCache)
        {
            var info = kvp.Value;
            if (info.IsInPuck && info.Presence != null && !string.IsNullOrEmpty(info.Presence.PlayerGroup))
            {
                string pg = info.Presence.PlayerGroup;
                int colonIdx = pg.LastIndexOf(':');
                if (colonIdx > 0)
                    endpoints.Add(pg);
            }
        }

        if (endpoints.Count == 0)
        {
            _callback?.Invoke(new Dictionary<string, ServerPreviewData>());
            _callback = null;
            return;
        }

        Task.Run(() =>
        {
            var results = new Dictionary<string, ServerPreviewData>();

            foreach (var addr in endpoints)
            {
                try
                {
                    int colonIdx = addr.LastIndexOf(':');
                    string ip = addr.Substring(0, colonIdx);
                    ushort port;
                    if (!ushort.TryParse(addr.Substring(colonIdx + 1), out port))
                        continue;

                    var ep = new EndPoint(ip, port);
                    var preview = PingServer(ep, 3000, 3000);
                    if (preview != null)
                        results[addr] = preview;
                }
                catch (Exception ex)
                {
                    Plugin.LogDebug($"BFL ping failed for {addr}: {ex.Message}");
                }
            }

            BFLMainThreadDispatcher.Enqueue(() =>
            {
                _callback?.Invoke(results);
                _callback = null;
            });
        });
    }

    private static ServerPreviewData PingServer(EndPoint endPoint, int connectTimeout, int responseTimeout)
    {
        var tcpClient = new TCPClient(endPoint, connectTimeout, 1000);
        ServerPreviewData previewData = null;
        var responseEvent = new ManualResetEventSlim(false);

        tcpClient.OnConnected += () =>
        {
            string msg = "{\"type\":0}";
            tcpClient.SendMessage(msg);
        };

        tcpClient.OnMessageReceived += (string message) =>
        {
            try
            {
                if (!message.Contains("\"type\":1"))
                    return;

                previewData = new ServerPreviewData
                {
                    name = ExtractJsonString(message, "name"),
                    players = ExtractJsonInt(message, "players"),
                    maxPlayers = ExtractJsonInt(message, "maxPlayers"),
                    isPasswordProtected = message.Contains("\"isPasswordProtected\":true"),
                    clientRequiredModIds = new string[0],
                };
                responseEvent.Set();
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"BFL parse error from {endPoint}: {ex.Message}");
            }
        };

        tcpClient.Connect();
        if (tcpClient.IsConnected)
        {
            responseEvent.Wait(responseTimeout);
            tcpClient.Disconnect();
        }

        return previewData;
    }

    private static string ExtractJsonString(string json, string key)
    {
        string search = "\"" + key + "\":\"";
        int start = json.IndexOf(search);
        if (start < 0) return "";
        start += search.Length;

        int end = start;
        while (end < json.Length)
        {
            if (json[end] == '\\') { end += 2; continue; }
            if (json[end] == '"') break;
            end++;
        }
        if (end >= json.Length) return "";

        string raw = json.Substring(start, end - start);

        var sb = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                if (next == 'u' && i + 5 < raw.Length)
                {
                    string hex = raw.Substring(i + 2, 4);
                    int code;
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                    {
                        sb.Append((char)code);
                        i += 5;
                        continue;
                    }
                }
                else if (next == '"') { sb.Append('"'); i++; continue; }
                else if (next == '\\') { sb.Append('\\'); i++; continue; }
                else if (next == '/') { sb.Append('/'); i++; continue; }
                else if (next == 'n') { sb.Append('\n'); i++; continue; }
                else if (next == 'r') { sb.Append('\r'); i++; continue; }
                else if (next == 't') { sb.Append('\t'); i++; continue; }
            }
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }

    private static int ExtractJsonInt(string json, string key)
    {
        string search = "\"" + key + "\":";
        int start = json.IndexOf(search);
        if (start < 0) return 0;
        start += search.Length;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            end++;
        int val;
        int.TryParse(json.Substring(start, end - start), out val);
        return val;
    }
}

/// <summary>
/// Local main-thread dispatcher for BFL's async callbacks. Kept private to this feature so
/// it can be deleted cleanly with the rest of the file. If a shared dispatcher lands in the
/// loader, swap calls over.
/// </summary>
internal class BFLMainThreadDispatcher : MonoBehaviour
{
    private static BFLMainThreadDispatcher _instance;
    private static readonly Queue<Action> _queue = new Queue<Action>();

    public static void Enqueue(Action action)
    {
        EnsureInstance();
        lock (_queue)
        {
            _queue.Enqueue(action);
        }
    }

    private static void EnsureInstance()
    {
        if (_instance == null)
        {
            var go = new GameObject("ToasterReskinLoader_BFL_Dispatcher");
            _instance = go.AddComponent<BFLMainThreadDispatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }

    private void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
            {
                try
                {
                    _queue.Dequeue()?.Invoke();
                }
                catch (Exception ex)
                {
                    Plugin.LogError($"BFL dispatcher error: {ex.Message}");
                }
            }
        }
    }
}
