using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Steamworks;
using UI;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

using ToasterReskinLoader.ui;

namespace ToasterReskinLoader.social;

/// <summary>
/// Friends list enhancement: rich-presence-aware status labels, status-aware sorting,
/// and per-friend Join buttons.
///
/// TODO: the "which server a friend is on" display (server-name lookup) is currently
/// NON-FUNCTIONAL — the rich-presence server data doesn't resolve, so it never shows.
/// What works today: clearer online/offline status and sorting online friends to the top.
/// Don't surface server-location in UI/settings copy until this is fixed.
///
/// Toggle via <see cref="Enable"/> / <see cref="Disable"/>. Will eventually be driven
/// by a settings config option; for now the call site in Plugin.cs decides.
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

    // Steam pushes a friend's rich presence asynchronously after
    // RequestFriendRichPresence (or when they change servers). Those arrivals
    // fire FriendRichPresenceUpdate_t — NOT PersonaStateChange — so without
    // this callback we'd only ever see a friend's server the one time the
    // 3-second delayed refresh in Enable() happens to catch it. This is what
    // kept the server-name detail stuck on "..." previously.
    private static Callback<FriendRichPresenceUpdate_t> _richPresenceCallback;

    // Puck's own AppID, cached so the rich-presence callback can filter out
    // updates from friends in OTHER games with a plain uint compare instead of
    // a P/Invoke per callback. With 500 friends the vast majority of RP traffic
    // is not Puck, so this early-out is the hot path.
    private static AppId_t _puckAppId;

    // Coalesce a burst of FriendRichPresenceUpdate_t (a playing friend sets ~9
    // keys, each firing separately) into a single refresh per frame. Callbacks
    // run on the main thread during RunCallbacks, so same-frame arrivals collect
    // here and flush once next Update — one re-sort, not one per key.
    private static readonly HashSet<string> _dirtyFriendIds = new HashSet<string>();
    private static bool _flushQueued;

    public static bool IsEnabled { get; private set; }

    public static void Enable()
    {
        if (IsEnabled) return;

        // NOTE: the empty-IP rich-presence fix used to be applied here. It now lives in
        // its own standalone feature (RichPresenceIpFix, toggled in the Fixes section)
        // so it works even when this feature is off.

        _harmony.Patch(
            AccessTools.Method(typeof(UIFriendsController), "Event_OnSteamConnected"),
            prefix: new HarmonyMethod(typeof(BFL_SteamConnectedPatch), nameof(BFL_SteamConnectedPatch.Prefix)));
        _harmony.Patch(
            AccessTools.Method(typeof(UIFriendsController), "Event_OnPersonaStateChange"),
            prefix: new HarmonyMethod(typeof(BFL_PersonaStateChangePatch), nameof(BFL_PersonaStateChangePatch.Prefix)));
        _harmony.Patch(
            AccessTools.Method(typeof(UIFriends), "SortFriends"),
            prefix: new HarmonyMethod(typeof(BFL_SortFriendsPatch), nameof(BFL_SortFriendsPatch.Prefix)));
        // Mirror the FRIENDS panel's visibility onto a dim backdrop so it
        // reads as the modal it effectively is. Patching UIView.Show/Hide
        // (virtual, inherited) instead of the IsVisible setter so we only
        // run on actual show/hide transitions, not every UIView property
        // write across the UI.
        _harmony.Patch(
            AccessTools.Method(typeof(UIView), nameof(UIView.Show)),
            postfix: new HarmonyMethod(typeof(BFL_FriendsVisibilityPatch), nameof(BFL_FriendsVisibilityPatch.Postfix)));
        _harmony.Patch(
            AccessTools.Method(typeof(UIView), nameof(UIView.Hide)),
            postfix: new HarmonyMethod(typeof(BFL_FriendsVisibilityPatch), nameof(BFL_FriendsVisibilityPatch.Postfix)));

        // Re-read + re-ping a friend as soon as their rich presence actually
        // arrives. Steamworks dispatches this on the main thread during
        // RunCallbacks, so it's safe to touch UI directly.
        if (SteamManager.IsInitialized)
        {
            _puckAppId = SteamUtils.GetAppID();
            _richPresenceCallback = Callback<FriendRichPresenceUpdate_t>.Create(OnFriendRichPresenceUpdate);
        }

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

    private static void OnFriendRichPresenceUpdate(FriendRichPresenceUpdate_t data)
    {
        if (!IsEnabled)
            return;

        try
        {
            // Cheap early-outs first — this fires for every friend's every RP
            // key change across ALL games, so with 500 friends it must stay
            // near-free for the cases we don't care about.

            // Not Puck? Ignore (friend is in another game / general presence).
            if (data.m_nAppID != _puckAppId)
                return;

            // Panel closed? Nothing to update — the next panel-open rebuild will
            // re-request and pick up whatever's current, so defer the work.
            if (!FriendsListHelper.FriendsPanelVisible)
                return;

            // Collect; a single flush next frame handles the whole burst.
            lock (_dirtyFriendIds)
                _dirtyFriendIds.Add(data.m_steamIDFriend.ToString());

            if (!_flushQueued)
            {
                _flushQueued = true;
                BFLMainThreadDispatcher.Enqueue(FlushDirtyFriends);
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"BFL rich-presence update failed: {ex.Message}");
        }
    }

    private static void FlushDirtyFriends()
    {
        _flushQueued = false;

        List<string> ids;
        lock (_dirtyFriendIds)
        {
            if (_dirtyFriendIds.Count == 0)
                return;
            ids = _dirtyFriendIds.ToList();
            _dirtyFriendIds.Clear();
        }

        // One scene scan for the whole batch, not one per callback.
        var controller = UnityEngine.Object.FindAnyObjectByType<UIFriendsController>();
        if (controller == null)
            return;

        Plugin.LogDebug($"BFL: flushing {ids.Count} rich-presence update(s)");
        foreach (var steamId in ids)
        {
            try
            {
                // requestPresence:false — data is already fresh (that's why the
                // callback fired); re-requesting would re-trigger this callback.
                FriendsListHelper.UpdateSingleFriend(controller, steamId, requestPresence: false);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"BFL flush of {steamId} failed: {ex.Message}");
            }
        }
    }

    public static void Disable()
    {
        if (!IsEnabled) return;
        _harmony.UnpatchSelf();
        _richPresenceCallback?.Unregister();
        _richPresenceCallback = null;
        lock (_dirtyFriendIds)
            _dirtyFriendIds.Clear();
        _flushQueued = false;
        FriendsListHelper.FriendsPanelVisible = false;
        FriendsListHelper.FriendInfoCache.Clear();
        FriendsListHelper.RemoveDimOverlay();
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

public static class BFL_FriendsVisibilityPatch
{
    public static void Postfix(UIView __instance)
    {
        if (__instance is UIFriends f)
        {
            // Cheap flag the RP callback reads to skip work while the panel is
            // closed — avoids a per-callback scene scan for visibility.
            FriendsListHelper.FriendsPanelVisible = f.IsVisible;
            FriendsListHelper.UpdateDimOverlay(f);
        }
    }
}

public static class FriendsListHelper
{
    private static readonly Dictionary<string, ServerPreviewData> _serverCache = new Dictionary<string, ServerPreviewData>();
    private static readonly Dictionary<ushort, string> _portToEndpointKey = new Dictionary<ushort, string>();
    private static bool _refreshInProgress = false;
    private static VisualElement _dimOverlay;

    // Set by BFL_FriendsVisibilityPatch on the FRIENDS panel's Show/Hide. The
    // rich-presence callback reads this to avoid doing (and scene-scanning for)
    // work while the panel is closed.
    public static bool FriendsPanelVisible;

    public static void UpdateDimOverlay(UIFriends uiFriends)
    {
        if (uiFriends == null) return;
        var view = uiFriends.View;
        if (view == null || view.parent == null) return;

        var host = view.parent;
        if (_dimOverlay == null || _dimOverlay.parent != host)
        {
            _dimOverlay = new VisualElement();
            _dimOverlay.name = "trl-bfl-dim";
            _dimOverlay.style.position = Position.Absolute;
            _dimOverlay.style.top = 0;
            _dimOverlay.style.left = 0;
            _dimOverlay.style.right = 0;
            _dimOverlay.style.bottom = 0;
            _dimOverlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            // Insert before FriendsView so it sits underneath in z-order
            // (UI Toolkit stacks later siblings on top). Default pickingMode
            // (Position) absorbs background clicks — matches existing modal
            // behavior of the panel.
            int viewIdx = host.IndexOf(view);
            host.Insert(Math.Max(0, viewIdx), _dimOverlay);
        }

        _dimOverlay.style.display = uiFriends.IsVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    public static void RemoveDimOverlay()
    {
        if (_dimOverlay != null)
        {
            _dimOverlay.RemoveFromHierarchy();
            _dimOverlay = null;
        }
    }

    public static readonly Dictionary<string, FriendInfo> FriendInfoCache = new Dictionary<string, FriendInfo>();

    // Case-insensitive substring filter on username. Empty string = show all.
    // Persisted across rebuilds so Steam state changes don't drop the filter.
    private static string _searchFilter = "";

    private static void EnsureSearchField(VisualElement friendsList)
    {
        if (friendsList == null) return;
        const string containerName = "trl-bfl-search-container";

        // Look for the FRIENDS title — search RECURSIVELY in each ancestor's
        // subtree (the title may be a grandchild, not a direct child), starting
        // from the "Friends" container that holds the list.
        Label titleLabel = null;
        VisualElement titleHost = null;
        for (var p = friendsList.parent; p != null && titleLabel == null; p = p.parent)
        {
            foreach (var lbl in p.Query<Label>().ToList())
            {
                if (lbl == null || string.IsNullOrEmpty(lbl.text)) continue;
                // Skip labels that live inside the friends list itself (those
                // are friend rows, not the header).
                bool insideList = false;
                for (var a = lbl.parent; a != null; a = a.parent)
                {
                    if (a == friendsList) { insideList = true; break; }
                }
                if (insideList) continue;

                string t = lbl.text.Trim();
                // Exact-match only — vanilla title is the uppercase word
                // "FRIENDS". Substring/StartsWith matches risk picking up
                // sibling labels like "Friends online: 3".
                if (t.Equals("FRIENDS", StringComparison.Ordinal) ||
                    t.Equals("Friends", StringComparison.Ordinal))
                {
                    titleLabel = lbl;
                    titleHost = lbl.parent;
                    break;
                }
            }
            // Don't walk above the panel root.
            if (p.parent == null) break;
        }

        VisualElement insertParent = titleHost ?? friendsList.parent;
        if (insertParent == null) return;
        if (insertParent.Q<VisualElement>(containerName) != null) return;

        // Mirror the mod menu's "Filter: [ ___ ]" layout: a row container that
        // grows to fill and pushes its contents right.
        var searchContainer = new VisualElement();
        searchContainer.name = containerName;
        searchContainer.style.flexDirection = FlexDirection.Row;
        searchContainer.style.alignItems = Align.Center;
        searchContainer.style.flexGrow = 1;
        searchContainer.style.justifyContent = Justify.FlexEnd;
        searchContainer.style.marginRight = 8;

        var searchLabel = new Label("Filter:");
        searchLabel.style.fontSize = 16;
        searchLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        searchLabel.style.marginRight = 6;
        searchContainer.Add(searchLabel);

        var field = new TextField();
        field.value = _searchFilter;
        field.style.width = 200;
        field.style.fontSize = 16;
        field.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            ToasterReskinLoader.ui.VanillaUIRetheme.RecolorTree(field);
            var input = field.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                input.style.color = Color.white;
                input.style.paddingLeft = 8;
                input.style.paddingRight = 8;
                input.style.paddingTop = 4;
                input.style.paddingBottom = 4;
            }
        });
        field.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            _searchFilter = evt.newValue ?? "";
            ApplyFilter(friendsList);
        });
        searchContainer.Add(field);

        if (titleLabel != null && titleHost != null)
        {
            // The host might be column-laid-out by vanilla; force it to row so
            // the search container sits next to the FRIENDS label instead of
            // dropping below it. Keep cross-axis centered to vertically align.
            titleHost.style.flexDirection = FlexDirection.Row;
            titleHost.style.alignItems = Align.Center;
            int titleIdx = titleHost.IndexOf(titleLabel);
            titleHost.Insert(titleIdx + 1, searchContainer);
        }
        else
        {
            int listIdx = insertParent.IndexOf(friendsList);
            insertParent.Insert(Math.Max(0, listIdx), searchContainer);
        }
    }

    private static void ApplyFilter(VisualElement friendsList)
    {
        if (friendsList == null) return;
        string q = (_searchFilter ?? "").Trim();
        bool empty = q.Length == 0;
        foreach (var child in friendsList.Children())
        {
            string username = null;
            // Match the username label by name. The status / detail labels we
            // inject also live in the row, so Q<Label>() (first descendant)
            // would drift if the row template ever reorders.
            var lbl = child.Q<Label>("UsernameLabel");
            if (lbl != null) username = lbl.text;
            bool match = empty
                || (!string.IsNullOrEmpty(username)
                    && username.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            child.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

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

            // Puck's rich presence carries an empty IP (host never sets
            // ServerManager.IpAddress), so probe Steam's own game-server tracking
            // as an alternate IP source. If m_unGameIP is non-zero we can ping it.
            if (isInPuck && Plugin.modSettings.DebugLoggingModeEnabled)
                Plugin.LogDebug(
                    $"BFL GameInfo {username}: gameIP={FormatSteamGameIp(gameInfo.m_unGameIP)} " +
                    $"gamePort={gameInfo.m_usGamePort} queryPort={gameInfo.m_usQueryPort} " +
                    $"lobby={gameInfo.m_steamIDLobby}");

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

            var fi = new FriendInfo
            {
                SteamId = steamIdStr,
                Username = username,
                State = state,
                IsInPuck = isInPuck,
                IsInOtherGame = isInGame && !isInPuck,
                Presence = presence
            };
            friends.Add(fi);

            if (isInPuck)
                LogPresenceDiagnostics("rebuild", steamId, fi);
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

        // Walk friends once: render status labels and detect missing server
        // previews in the same pass so LookupServerPreview (and its lock) is
        // only hit once per friend.
        bool needsServerNames = false;
        if (friendsMap != null)
        {
            foreach (var friend in friends)
            {
                ServerPreviewData cachedPreview = null;
                bool hasGroup = friend.Presence != null && !string.IsNullOrEmpty(friend.Presence.PlayerGroup);
                if (hasGroup)
                    cachedPreview = LookupServerPreview(friend.Presence.PlayerGroup);

                if (friend.IsInPuck && hasGroup && cachedPreview == null)
                    needsServerNames = true;

                if (friendsMap.TryGetValue(friend.SteamId, out var container))
                    AddStatusLabels(container, friend, cachedPreview);
            }
        }
        else
        {
            foreach (var f in friends)
            {
                if (f.IsInPuck && f.Presence != null &&
                    !string.IsNullOrEmpty(f.Presence.PlayerGroup) &&
                    LookupServerPreview(f.Presence.PlayerGroup) == null)
                {
                    needsServerNames = true;
                    break;
                }
            }
        }

        EnsureSearchField(friendsList);
        ApplyFilter(friendsList);

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

    // requestPresence=false is used by the rich-presence callback path: the
    // callback ONLY fires because Steam already has fresh data, so re-requesting
    // there would fire the callback again — an endless UpdateSingleFriend loop.
    public static void UpdateSingleFriend(UIFriendsController controller, string steamId, bool requestPresence = true)
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

        if (requestPresence)
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

        if (isInPuck)
            LogPresenceDiagnostics("single", sid, info);

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

        // The copied icon-button classes leave the button transparent — give it
        // a solid "positive action" green so it reads as a real button.
        var idle = new Color(0.20f, 0.52f, 0.30f);
        var hover = new Color(0.26f, 0.62f, 0.37f);
        joinBtn.style.backgroundColor = new StyleColor(idle);
        joinBtn.style.color = Color.white;
        joinBtn.style.paddingLeft = 10;
        joinBtn.style.paddingRight = 10;
        joinBtn.style.paddingTop = 3;
        joinBtn.style.paddingBottom = 3;
        joinBtn.style.borderTopLeftRadius = 3;
        joinBtn.style.borderTopRightRadius = 3;
        joinBtn.style.borderBottomLeftRadius = 3;
        joinBtn.style.borderBottomRightRadius = 3;
        joinBtn.style.marginRight = 4;

        joinBtn.RegisterCallback<MouseEnterEvent>(_ => joinBtn.style.backgroundColor = new StyleColor(hover));
        joinBtn.RegisterCallback<MouseLeaveEvent>(_ => joinBtn.style.backgroundColor = new StyleColor(idle));

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

    // Passive diagnostic: dump every rich-presence key/value Steam actually
    // hands us for a friend, plus the resolved server preview. Gated behind the
    // Developer "Debug Logging" toggle so it never spams normal users. This is
    // the fast way to see WHICH half is failing (empty steam_player_group vs.
    // TCP ping) instead of guessing.
    // FriendGameInfo_t.m_unGameIP is a uint in host byte order (Steamworks.NET
    // hands it back already flipped). 0 means Steam has no server IP for them.
    internal static string FormatSteamGameIp(uint ip)
    {
        if (ip == 0) return "0.0.0.0";
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }

    internal static void LogPresenceDiagnostics(string context, CSteamID sid, FriendInfo friend)
    {
        if (!Plugin.modSettings.DebugLoggingModeEnabled)
            return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"BFL RP[{context}] {friend.Username} ({sid}) inPuck={friend.IsInPuck}");

            int keyCount = SteamFriends.GetFriendRichPresenceKeyCount(sid);
            sb.Append($" keys={keyCount}:");
            for (int i = 0; i < keyCount; i++)
            {
                string key = SteamFriends.GetFriendRichPresenceKeyByIndex(sid, i);
                string val = SteamFriends.GetFriendRichPresence(sid, key);
                sb.Append($" [{key}={val}]");
            }

            if (friend.Presence != null && !string.IsNullOrEmpty(friend.Presence.PlayerGroup))
            {
                var preview = LookupServerPreview(friend.Presence.PlayerGroup);
                sb.Append(preview != null
                    ? $" => preview name=\"{preview.name}\" {preview.players}/{preview.maxPlayers}"
                    : $" => preview NOT resolved for group '{friend.Presence.PlayerGroup}'");
            }

            Plugin.LogDebug(sb.ToString());
        }
        catch (Exception ex)
        {
            Plugin.LogDebug($"BFL RP diag failed: {ex.Message}");
        }
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

    // Puck server names can embed TMP rich-text size tags (e.g. "<size=12>...</size>")
    // that would render literally in our plain UITK detail label. Strip them.
    private static string StripSizeTags(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return System.Text.RegularExpressions.Regex.Replace(
            name, "</?size[^>]*>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string GetDetailText(FriendInfo friend, ServerPreviewData preview)
    {
        if (!friend.IsInPuck || friend.Presence == null)
            return null;

        var parts = new List<string>();

        if (preview != null && !string.IsNullOrEmpty(preview.name))
        {
            string name = StripSizeTags(preview.name).Trim();
            if (name.Length > 0)
                parts.Add(name);
        }

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
    // Max simultaneous preview pings. Bounds thread use while keeping resolution
    // fast when friends are spread across many servers.
    private const int PingConcurrency = 8;

    public static void FetchServerNames(Action<Dictionary<string, ServerPreviewData>> callback)
    {
        // Capture the callback in the closures below rather than a shared static field, so two
        // overlapping FetchServerNames calls (e.g. back-to-back refreshes) don't clobber each
        // other's callback — each invocation keeps its own.
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

        FetchServerNames(endpoints, callback);
    }

    /// Ping an explicit set of "ip:port" endpoints for their server preview.
    /// Used by FriendsBoard, which builds its own endpoint set independent of
    /// FriendInfoCache. Endpoints with an empty IP (vanilla friends: ":30611")
    /// are skipped — nothing to ping.
    public static void FetchServerNames(ICollection<string> endpointSet, Action<Dictionary<string, ServerPreviewData>> callback)
    {
        var endpoints = new HashSet<string>();
        if (endpointSet != null)
        {
            foreach (var addr in endpointSet)
            {
                if (string.IsNullOrEmpty(addr)) continue;
                int colonIdx = addr.LastIndexOf(':');
                // colonIdx > 0 also rejects ":30611" (empty IP), where colonIdx == 0.
                if (colonIdx > 0)
                    endpoints.Add(addr);
            }
        }

        if (endpoints.Count == 0)
        {
            callback?.Invoke(new Dictionary<string, ServerPreviewData>());
            return;
        }

        Task.Run(() =>
        {
            // Ping concurrently with a bounded worker pool, mirroring
            // UIServerBrowser: a sequential loop with 3s+3s timeouts each would
            // take minutes when many friends are spread across many servers (or
            // some are unreachable). Each worker blocks on its own I/O and drains
            // a shared queue, so wall-clock is ~ceil(N / PingConcurrency) pings.
            var queue = new ConcurrentQueue<string>(endpoints);
            var results = new ConcurrentDictionary<string, ServerPreviewData>();
            int workerCount = Math.Max(1, Math.Min(PingConcurrency, endpoints.Count));

            var workers = new Task[workerCount];
            for (int w = 0; w < workerCount; w++)
            {
                workers[w] = Task.Run(() =>
                {
                    while (queue.TryDequeue(out var addr))
                    {
                        try
                        {
                            int colonIdx = addr.LastIndexOf(':');
                            string ip = addr.Substring(0, colonIdx);
                            if (!ushort.TryParse(addr.Substring(colonIdx + 1), out var port))
                                continue;

                            var preview = PingServer(new EndPoint(ip, port), 3000, 3000);
                            if (preview != null)
                            {
                                results[addr] = preview;
                                Plugin.LogDebug($"BFL ping OK {addr} => \"{preview.name}\" {preview.players}/{preview.maxPlayers}");
                            }
                            else
                            {
                                Plugin.LogDebug($"BFL ping {addr} connected-or-timed-out but returned no preview");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.LogDebug($"BFL ping failed for {addr}: {ex.Message}");
                        }
                    }
                });
            }

            Task.WaitAll(workers);

            var final = new Dictionary<string, ServerPreviewData>(results);
            BFLMainThreadDispatcher.Enqueue(() => callback?.Invoke(final));
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
