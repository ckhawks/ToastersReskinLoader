using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;
using ToasterReskinLoader.social; // FriendsListHelper (FriendInfo/ServerPreviewData), ServerNameFetcher

namespace ToasterReskinLoader.social.friendspanel;

/// <summary>
/// A persistent right-side board on the main menu that shows which servers your
/// friends are on, grouped one card per server (name, players/max, lock icon,
/// the friends on it, and a Join button). Independent of the UIFriends modal
/// that BetterFriendsList enhances.
///
/// Data comes from Steam rich presence. Thanks to BFL_RichPresenceIpFixPatch,
/// friends who run this mod broadcast a real "ip:port"; vanilla friends broadcast
/// ":port" (empty ip). We group the former by server and resolve names via the
/// TCP preview ping; the latter can't be resolved, so they land in a "Playing
/// (No TRL)" bucket.
///
/// Mounting/refresh is driven by <see cref="FriendsBoardDriver"/> (a MonoBehaviour)
/// rather than Steam callbacks, to avoid the per-friend callback storm that a
/// 500-friend list would otherwise cause.
/// </summary>
public static class FriendsBoard
{
    private const int RefreshIntervalSeconds = 15;
    private const int PanelWidth = 320;

    // Match the game's UI (solid, square). Values from Puck UI/Theme/_globals.scss.
    private static readonly Color PanelColor = new Color(0.196f, 0.196f, 0.196f);   // #323232 window
    private static readonly Color CardColor = new Color(0.157f, 0.157f, 0.157f);    // recessed well
    private static readonly Color ButtonColor = new Color(0.251f, 0.251f, 0.251f);  // #404040 button
    private static readonly Color TeamBlue = new Color(0.231f, 0.510f, 0.965f);     // #3b82f6
    private static readonly Color TeamRed = new Color(0.819f, 0.200f, 0.200f);      // #d13333

    internal static bool EnabledFlag { get; private set; }

    private static VisualElement _panel;
    private static ScrollView _cards;
    private static Label _emptyLabel;
    private static Label _statusLabel;
    private static FriendsBoardDriver _driver;

    private static float _lastRefreshTime;
    private static string _lastStatusText = "";

    // Resolved server previews keyed by "ip:port". Populated by the async ping.
    private static readonly Dictionary<string, ServerPreviewData> _previews = new();
    // Time.unscaledTime when each preview was last resolved, so player counts /
    // lock status can be re-pinged instead of frozen at first resolution.
    private static readonly Dictionary<string, float> _previewAge = new();
    private const float PreviewTtlSeconds = 45f;
    private static bool _pingInProgress;

    public static void Enable()
    {
        if (EnabledFlag) return;
        EnabledFlag = true;

        if (_driver == null)
        {
            var go = new GameObject("ToasterReskinLoader_FriendsBoardDriver");
            _driver = go.AddComponent<FriendsBoardDriver>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        Plugin.Log("FriendsBoard enabled.");
    }

    public static void Disable()
    {
        if (!EnabledFlag) return;
        EnabledFlag = false;

        if (_driver != null)
        {
            UnityEngine.Object.Destroy(_driver.gameObject);
            _driver = null;
        }

        if (_panel != null)
        {
            _panel.RemoveFromHierarchy();
            _panel = null;
            _cards = null;
            _emptyLabel = null;
        }

        Plugin.Log("FriendsBoard disabled.");
    }

    // Called every frame by the driver: (re)attach the panel if the main menu
    // UI exists and we're not currently mounted.
    internal static void EnsureMounted()
    {
        if (_panel != null && _panel.parent != null)
            return;

        var mainMenu = UIManager.Instance != null ? UIManager.Instance.MainMenu : null;
        var host = mainMenu != null ? mainMenu.View : null;
        if (host == null)
            return;

        Build();
        host.Add(_panel);
        Refresh();
    }

    // Updates the "Updated Ns ago" line. Called every frame by the driver but
    // only writes when the text actually changes, so it's cheap.
    internal static void TickStatus()
    {
        if (_statusLabel == null || _panel == null || _panel.parent == null)
            return;

        string text;
        if (_pingInProgress)
            text = "Refreshing…";
        else if (_lastRefreshTime <= 0f)
            text = "";
        else
        {
            int secs = Mathf.Max(0, (int)(Time.unscaledTime - _lastRefreshTime));
            if (secs < 3) text = "Updated just now";
            else if (secs < 60) text = $"Updated {secs}s ago";
            else text = $"Updated {secs / 60}m ago";
        }

        if (text != _lastStatusText)
        {
            _lastStatusText = text;
            _statusLabel.text = text;
        }
    }

    internal static void RefreshIfVisible()
    {
        if (_panel == null || _panel.parent == null)
            return;

        var mainMenu = UIManager.Instance != null ? UIManager.Instance.MainMenu : null;
        if (mainMenu == null || !mainMenu.IsVisible)
            return;

        Refresh();
    }

    private static void Build()
    {
        _panel = new VisualElement { name = "ToasterFriendsBoard" };
        _panel.style.position = Position.Absolute;
        _panel.style.right = 24;
        // Vertically centered: anchor the top at 50% then shift up by half the
        // panel's own height (matches the probe panel's placement).
        _panel.style.top = Length.Percent(50);
        _panel.style.translate = new Translate(0, Length.Percent(-50));
        _panel.style.width = PanelWidth;
        _panel.style.maxHeight = Length.Percent(72);
        _panel.style.paddingTop = 12;
        _panel.style.paddingBottom = 12;
        _panel.style.paddingLeft = 12;
        _panel.style.paddingRight = 12;
        _panel.style.backgroundColor = new StyleColor(PanelColor);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 8;

        var title = new Label("FRIENDS")
        {
            style =
            {
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 16,
                flexGrow = 1,
            }
        };
        header.Add(title);

        // "Updated Ns ago" sits to the left of the refresh button.
        _statusLabel = new Label("")
        {
            style =
            {
                color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)),
                fontSize = 11,
                marginRight = 8,
            }
        };
        header.Add(_statusLabel);

        var refreshBtn = new Button(ForceRefresh) { text = "⟳" };
        refreshBtn.tooltip = "Refresh now";
        refreshBtn.style.fontSize = 16;
        refreshBtn.style.color = Color.white;
        refreshBtn.style.backgroundColor = new StyleColor(ButtonColor);
        refreshBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
        refreshBtn.style.paddingLeft = 8;
        refreshBtn.style.paddingRight = 8;
        // The ⟳ glyph renders low in its line box; extra bottom padding over
        // top padding lifts it to visual center.
        refreshBtn.style.paddingTop = 1;
        refreshBtn.style.paddingBottom = 3;
        refreshBtn.style.marginLeft = 0;
        refreshBtn.style.marginRight = 0;
        // Game buttons invert on hover (white bg, black text).
        refreshBtn.RegisterCallback<MouseEnterEvent>(_ =>
        {
            refreshBtn.style.backgroundColor = new StyleColor(Color.white);
            refreshBtn.style.color = Color.black;
        });
        refreshBtn.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            refreshBtn.style.backgroundColor = new StyleColor(ButtonColor);
            refreshBtn.style.color = Color.white;
        });
        header.Add(refreshBtn);

        _panel.Add(header);

        _emptyLabel = new Label("No friends in game.")
        {
            style =
            {
                color = new StyleColor(new Color(0.65f, 0.65f, 0.65f)),
                fontSize = 13,
                whiteSpace = WhiteSpace.Normal,
            }
        };
        _panel.Add(_emptyLabel);

        _cards = new ScrollView(ScrollViewMode.Vertical);
        _cards.style.flexGrow = 1;
        _panel.Add(_cards);
    }

    // Manual refresh: force every visible server to re-ping (ignore the TTL)
    // and rebuild immediately.
    private static void ForceRefresh()
    {
        lock (_previews)
            _previewAge.Clear();
        Refresh();
    }

    private static void Refresh()
    {
        if (_cards == null) return;

        _lastRefreshTime = Time.unscaledTime;

        var groups = GatherGroups(out var noTrl, out int inMenus);

        _cards.Clear();

        bool anything = groups.Count > 0 || noTrl.Count > 0;
        _emptyLabel.style.display = anything ? DisplayStyle.None : DisplayStyle.Flex;

        // Servers with the most friends first, then by name.
        foreach (var g in groups
            .OrderByDescending(g => g.Friends.Count)
            .ThenBy(g => PreviewName(g)))
        {
            _cards.Add(BuildServerCard(g));
        }

        if (noTrl.Count > 0)
            _cards.Add(BuildNoTrlCard(noTrl));

        if (inMenus > 0)
        {
            var footer = new Label($"+{inMenus} in menus")
            {
                style =
                {
                    color = new StyleColor(new Color(0.55f, 0.55f, 0.55f)),
                    fontSize = 12,
                    marginTop = 6,
                    unityFontStyleAndWeight = FontStyle.Italic,
                }
            };
            _cards.Add(footer);
        }

        // Resolve any server names we don't have cached yet.
        RequestPreviews(groups);
    }

    private static List<ServerGroup> GatherGroups(out List<BoardFriend> noTrl, out int inMenus)
    {
        var groups = new Dictionary<string, ServerGroup>();
        noTrl = new List<BoardFriend>();
        inMenus = 0;

        if (!SteamManager.IsInitialized)
            return new List<ServerGroup>();

        var puckAppId = SteamUtils.GetAppID();
        int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

        for (int i = 0; i < count; i++)
        {
            var sid = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);

            FriendGameInfo_t gameInfo;
            bool isInGame = SteamFriends.GetFriendGamePlayed(sid, out gameInfo);
            if (!isInGame || gameInfo.m_gameID.AppID() != puckAppId)
                continue;

            SteamFriends.RequestFriendRichPresence(sid);

            string pg = SteamFriends.GetFriendRichPresence(sid, "steam_player_group");
            var friend = new BoardFriend
            {
                SteamId = sid.ToString(),
                Username = SteamFriends.GetFriendPersonaName(sid),
                Team = SteamFriends.GetFriendRichPresence(sid, "team"),
                Role = SteamFriends.GetFriendRichPresence(sid, "role"),
                Avatar = SteamIntegrationManager.GetAvatar(sid.ToString(), AvatarSize.Small),
            };

            if (string.IsNullOrEmpty(pg))
            {
                // In Puck but not on a server (changing room / menus).
                inMenus++;
                continue;
            }

            int colonIdx = pg.LastIndexOf(':');
            // colonIdx <= 0 means empty IP (":30611") — a vanilla friend we can't resolve.
            if (colonIdx <= 0)
            {
                noTrl.Add(friend);
                continue;
            }

            if (!groups.TryGetValue(pg, out var group))
            {
                ushort.TryParse(pg.Substring(colonIdx + 1), out var port);
                group = new ServerGroup
                {
                    Endpoint = pg,
                    Ip = pg.Substring(0, colonIdx),
                    Port = port,
                    Friends = new List<BoardFriend>(),
                };
                lock (_previews)
                    _previews.TryGetValue(pg, out group.Preview);
                groups[pg] = group;
            }
            group.Friends.Add(friend);
        }

        return groups.Values.ToList();
    }

    private static void RequestPreviews(List<ServerGroup> groups)
    {
        if (_pingInProgress) return;

        float now = Time.unscaledTime;
        var missing = new HashSet<string>();
        foreach (var g in groups)
        {
            lock (_previews)
            {
                bool stale = !_previews.ContainsKey(g.Endpoint)
                    || !_previewAge.TryGetValue(g.Endpoint, out var t)
                    || (now - t) > PreviewTtlSeconds;
                if (stale)
                    missing.Add(g.Endpoint);
            }
        }
        if (missing.Count == 0) return;

        _pingInProgress = true;
        ServerNameFetcher.FetchServerNames(missing, results =>
        {
            _pingInProgress = false;
            if (results != null && results.Count > 0)
            {
                float stamp = Time.unscaledTime;
                lock (_previews)
                {
                    foreach (var kvp in results)
                    {
                        _previews[kvp.Key] = kvp.Value;
                        _previewAge[kvp.Key] = stamp;
                    }
                }

                // Re-render with the freshly resolved names / counts.
                RefreshIfVisible();
            }
        });
    }

    private static string PreviewName(ServerGroup g)
    {
        return g.Preview != null && !string.IsNullOrEmpty(g.Preview.name)
            ? g.Preview.name
            : "￿"; // sort unresolved names last
    }

    private static VisualElement BuildServerCard(ServerGroup g)
    {
        var card = NewCard();

        // Header: name .... count  lock
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 4;

        string name = g.Preview != null && !string.IsNullOrEmpty(g.Preview.name)
            ? StripSizeTags(g.Preview.name).Trim()
            : "Resolving…";
        var nameLabel = new Label(name)
        {
            style =
            {
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 15,
                flexGrow = 1,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis,
                whiteSpace = WhiteSpace.NoWrap,
            }
        };
        header.Add(nameLabel);

        if (g.Preview != null)
        {
            var countLabel = new Label($"{g.Preview.players}/{g.Preview.maxPlayers}")
            {
                style =
                {
                    color = new StyleColor(new Color(0.75f, 0.75f, 0.75f)),
                    fontSize = 13,
                    marginLeft = 6,
                }
            };
            header.Add(countLabel);

            if (g.Preview.isPasswordProtected)
            {
                var lockLabel = new Label("🔒") // 🔒
                {
                    style = { fontSize = 13, marginLeft = 4 }
                };
                header.Add(lockLabel);
            }
        }
        card.Add(header);

        foreach (var f in g.Friends.OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase))
            card.Add(BuildFriendRow(f));

        card.Add(BuildJoinButton(g));
        return card;
    }

    private static VisualElement BuildNoTrlCard(List<BoardFriend> friends)
    {
        var card = NewCard();

        var header = new Label("Playing (No TRL)")
        {
            style =
            {
                color = new StyleColor(new Color(0.8f, 0.8f, 0.8f)),
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 15,
                marginBottom = 4,
            }
        };
        card.Add(header);

        foreach (var f in friends.OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase))
            card.Add(BuildFriendRow(f));

        var note = new Label("Server unknown — they aren't running the mod.")
        {
            style =
            {
                color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)),
                fontSize = 11,
                whiteSpace = WhiteSpace.Normal,
                marginTop = 4,
            }
        };
        card.Add(note);
        return card;
    }

    private static VisualElement BuildFriendRow(BoardFriend f)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 4;

        if (f.Avatar != null)
        {
            var avatar = new Image
            {
                image = f.Avatar,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 24, height = 24, marginRight = 8, flexShrink = 0 }
            };
            row.Add(avatar);
        }

        var nameLabel = new Label(f.Username)
        {
            style =
            {
                color = new StyleColor(new Color(0.9f, 0.9f, 0.9f)),
                fontSize = 16,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis,
                whiteSpace = WhiteSpace.NoWrap,
                flexShrink = 1,
            }
        };
        row.Add(nameLabel);

        AppendTeamRole(row, f);
        return row;
    }

    // Renders the friend's team/role. Team "Team Blue"/"Team Red" is shown as
    // "Blue"/"Red" in the team color; an absent/"None" role means they're
    // spectating, shown as a single "Spectating" tag instead of "None".
    private static void AppendTeamRole(VisualElement row, BoardFriend f)
    {
        string role = f.Role ?? "";
        bool spectating = role.Length == 0 || role.Equals("None", StringComparison.OrdinalIgnoreCase);

        if (spectating)
        {
            row.Add(new Label("Spectating")
            {
                style =
                {
                    color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                    fontSize = 12,
                    marginLeft = 8,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    flexShrink = 0,
                }
            });
            return;
        }

        string team = f.Team ?? "";
        if (team.StartsWith("Team ", StringComparison.OrdinalIgnoreCase))
            team = team.Substring(5);
        bool teamKnown = team.Length > 0 && !team.Equals("None", StringComparison.OrdinalIgnoreCase);

        if (teamKnown)
        {
            Color teamColor =
                team.Equals("Red", StringComparison.OrdinalIgnoreCase) ? TeamRed :
                team.Equals("Blue", StringComparison.OrdinalIgnoreCase) ? TeamBlue :
                new Color(0.7f, 0.7f, 0.7f);
            row.Add(new Label(team)
            {
                style =
                {
                    color = new StyleColor(teamColor),
                    fontSize = 12,
                    marginLeft = 8,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 0,
                }
            });
        }

        row.Add(new Label(role)
        {
            style =
            {
                color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
                fontSize = 12,
                marginLeft = teamKnown ? 4 : 8,
                flexShrink = 0,
            }
        });
    }

    private static Button BuildJoinButton(ServerGroup g)
    {
        var idle = new Color(0.20f, 0.52f, 0.30f);
        var hover = new Color(0.26f, 0.62f, 0.37f);

        string ip = g.Ip;
        ushort port = g.Port;
        var btn = new Button(() =>
        {
            Plugin.Log($"FriendsBoard: joining {ip}:{port}");
            EventManager.TriggerEvent("Event_OnMainMenuClickJoinServer", new Dictionary<string, object>
            {
                { "ipAddress", ip },
                { "port", port },
                { "password", "" },
            });
        })
        {
            text = "JOIN",
        };
        btn.style.marginTop = 8;
        btn.style.alignSelf = Align.FlexStart;
        btn.style.backgroundColor = new StyleColor(idle);
        btn.style.color = Color.white;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.fontSize = 14;
        btn.style.paddingLeft = 16;
        btn.style.paddingRight = 16;
        btn.style.paddingTop = 4;
        btn.style.paddingBottom = 4;
        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = new StyleColor(hover));
        btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = new StyleColor(idle));
        return btn;
    }

    private static VisualElement NewCard()
    {
        var card = new VisualElement();
        card.style.backgroundColor = new StyleColor(CardColor);
        card.style.paddingTop = 8;
        card.style.paddingBottom = 8;
        card.style.paddingLeft = 8;
        card.style.paddingRight = 8;
        card.style.marginBottom = 8;
        return card;
    }

    private static string StripSizeTags(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return System.Text.RegularExpressions.Regex.Replace(
            name, "</?size[^>]*>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private class BoardFriend
    {
        public string SteamId;
        public string Username;
        public string Team;
        public string Role;
        public Texture2D Avatar;
    }

    private class ServerGroup
    {
        public string Endpoint;
        public string Ip;
        public ushort Port;
        public List<BoardFriend> Friends;
        public ServerPreviewData Preview;
    }
}

/// <summary>
/// Drives <see cref="FriendsBoard"/>: mounts the panel once the main-menu UI
/// exists and refreshes it on a fixed interval. Deliberately poll-based (not
/// Steam-callback-based) so a large friends list can't trigger a refresh storm.
/// </summary>
internal class FriendsBoardDriver : MonoBehaviour
{
    private const float RefreshInterval = 15f;
    private float _nextRefresh;

    private void Update()
    {
        if (!FriendsBoard.EnabledFlag)
            return;

        try
        {
            FriendsBoard.EnsureMounted();
            FriendsBoard.TickStatus();

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                FriendsBoard.RefreshIfVisible();
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"FriendsBoard driver error: {ex.Message}");
        }
    }
}
