// Quality of Life tab — small base-game UX patches plus a few opt-in
// extras (arena visual disable, in-game dev console). Single scrollable
// page; ordered so the common everyday-player UX fixes are at the top
// and the developer/arena-tweak controls are further down.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToasterReskinLoader.qol;
using ToasterReskinLoader.qol.beacon;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PlayerQoLSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        var description = UITools.CreateConfigurationLabel(
            "Small base-game UX patches plus optional arena visual disable and an in-game dev console.");
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(description);

        var runner = QoLRunner.Instance;
        if (runner == null)
        {
            var warn = UITools.CreateConfigurationLabel(
                "QoL runtime is not initialized yet. Reopen this menu after the game finishes loading.");
            warn.style.color = new Color(1f, 0.7f, 0.4f);
            warn.style.whiteSpace = WhiteSpace.Normal;
            contentScrollViewContent.Add(warn);
            return;
        }

        var cfg = runner.Config;

        // ── Base game ─────────────────────────────────────────────────────
        Header(contentScrollViewContent, "Base game");
        Note(contentScrollViewContent,
            "Small Harmony patches that polish vanilla menu and gameplay behavior.");

        ToggleRow(contentScrollViewContent, "Close secondary menus with ESC", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Open chat in any in-game phase", cfg.enableChatAnyInGamePhase,
            v => { cfg.enableChatAnyInGamePhase = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Enable scoreboard in any in-game phase", cfg.enableScoreboardAnyInGamePhase,
            v => { cfg.enableScoreboardAnyInGamePhase = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Show minimap for spectators", cfg.enableSpectatorMinimap,
            v => { cfg.enableSpectatorMinimap = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Show jersey number in player name", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Show player count on team select buttons", cfg.enableTeamButtonPlayerCount,
            v => { cfg.enableTeamButtonPlayerCount = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Text drop-shadow on all game UI", cfg.enableUiTextShadow,
            v =>
            {
                cfg.enableUiTextShadow = v;
                runner.SaveAndRefresh();
                UiTextShadow.RefreshForCurrentState();
            });
        ToggleRow(contentScrollViewContent, "Restore Unicode glyphs", cfg.enableUnicodeFontFallback,
            v =>
            {
                cfg.enableUnicodeFontFallback = v;
                runner.SaveAndRefresh();
                if (v) UnicodeFontFallback.Apply();
                else   UnicodeFontFallback.Disable();
            });

        // ── Chat ──────────────────────────────────────────────────────────
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Chat & Scoreboard");

        ToggleRow(contentScrollViewContent, "Drag-highlight and copy lines", cfg.enableChatDragSelect,
            v => { cfg.enableChatDragSelect = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Hide when inactive", cfg.enableHideInactiveChat,
            v =>
            {
                cfg.enableHideInactiveChat = v;
                runner.SaveAndRefresh();
                HideInactiveChat.RefreshVisualState();
            });
        ToggleRow(contentScrollViewContent, "Keep expired messages at full opacity", cfg.enableChatNoFade,
            v =>
            {
                cfg.enableChatNoFade = v;
                runner.SaveAndRefresh();
                HideInactiveChat.RefreshVisualState();
            });
        ToggleRow(contentScrollViewContent, "Invisible container", cfg.enableChatTransparentContainer,
            v =>
            {
                cfg.enableChatTransparentContainer = v;
                runner.SaveAndRefresh();
                HideInactiveChat.RefreshVisualState();
            });


        ToggleRow(contentScrollViewContent, "Clock shows milliseconds", cfg.enableScoreboardMilliseconds,
            v => { cfg.enableScoreboardMilliseconds = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Clock turns red in final 30s", cfg.enableScoreboardClockColor,
            v => { cfg.enableScoreboardClockColor = v; runner.SaveAndRefresh(); });

        // ── Server browser ────────────────────────────────────────────────
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Server Browser");

        ToggleRow(contentScrollViewContent, "Show filters inline", cfg.enableInlineServerBrowserFilters,
            v =>
            {
                cfg.enableInlineServerBrowserFilters = v;
                runner.SaveAndRefresh();
                if (v) InlineServerBrowserFilters.ReapplyInlineFiltersForCurrent();
                else   InlineServerBrowserFilters.UndoInlineFiltersForCurrent();
            });
        ToggleRow(contentScrollViewContent, "Remember filters between sessions", cfg.enableBrowserFilterPersistence,
            v => { cfg.enableBrowserFilterPersistence = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Auto-queue when joining a full server", cfg.enableServerSlotQueue,
            v => { cfg.enableServerSlotQueue = v; runner.SaveAndRefresh(); });
        ToggleRow(contentScrollViewContent, "Title-screen Quick Join button", cfg.enableMainMenuQuickJoin,
            v =>
            {
                cfg.enableMainMenuQuickJoin = v;
                runner.SaveAndRefresh();
                MainMenuButtons.RefreshForCurrentMenu();
            });
        ToggleRow(contentScrollViewContent, "Title-screen Server Browser button", cfg.enableMainMenuServerBrowser,
            v =>
            {
                cfg.enableMainMenuServerBrowser = v;
                runner.SaveAndRefresh();
                MainMenuButtons.RefreshForCurrentMenu();
            });
        ToggleRow(contentScrollViewContent, "Cache server browser between opens", cfg.enableServerPreviewCache,
            v => { cfg.enableServerPreviewCache = v; runner.SaveAndRefresh(); });

        // ── Server browser stores (compact rows) ──────────────────────────
        // Each store has its own enable toggle + expandable entry list.
        var passwordsList = new VisualElement(); passwordsList.style.marginTop = 4;
        var favoritesList = new VisualElement(); favoritesList.style.marginTop = 4;
        var blockedList   = new VisualElement(); blockedList.style.marginTop = 4;
        var trustedList   = new VisualElement(); trustedList.style.marginTop = 4;

         Separator(contentScrollViewContent);

        CompactStoreRow(contentScrollViewContent,
            "Saved Passwords",
            () => cfg.enableSavedServerPasswords,
            () => SavedServerPasswords.SnapshotKeys().Count,
            v =>
            {
                cfg.enableSavedServerPasswords = v;
                runner.SaveAndRefresh();
                RebuildSavedPasswordsList(passwordsList);
            },
            passwordsList,
            () => RebuildSavedPasswordsList(passwordsList));

             Separator(contentScrollViewContent);

        CompactStoreRow(contentScrollViewContent,
            "Favorites",
            () => cfg.enableServerFavorites,
            () => ServerBrowserSort.SnapshotFavoriteKeys().Count,
            v =>
            {
                cfg.enableServerFavorites = v;
                runner.SaveAndRefresh();
                ServerBrowserSort.RefreshForCurrentBrowser();
                RebuildFavoritesList(favoritesList);
            },
            favoritesList,
            () => RebuildFavoritesList(favoritesList));

             Separator(contentScrollViewContent);

        CompactStoreRow(contentScrollViewContent,
            "Blocked",
            () => cfg.enableServerBlocks,
            () => ServerBrowserSort.SnapshotBlockedKeys().Count,
            v =>
            {
                cfg.enableServerBlocks = v;
                runner.SaveAndRefresh();
                ServerBrowserSort.RefreshForCurrentBrowser();
                RebuildBlockedList(blockedList);
            },
            blockedList,
            () => RebuildBlockedList(blockedList));

             Separator(contentScrollViewContent);

        CompactStoreRow(contentScrollViewContent,
            "Trusted Mod Lists",
            () => cfg.enableTrustedModLists,
            () => MissingModsPopupSuppression.SnapshotKeys().Count,
            v =>
            {
                cfg.enableTrustedModLists = v;
                runner.SaveAndRefresh();
                RebuildTrustedServersList(trustedList);
            },
            trustedList,
            () => RebuildTrustedServersList(trustedList));

        /*

        // ── Arena visuals (further down — niche / personal preference) ─────
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Arena Visuals");
        Note(contentScrollViewContent,
            "Disable parts of the arena scenery. The full blackout toggle destroys the arena GameObject on the next server join.");

        ToggleRow(contentScrollViewContent, "Disable all arena visuals",
            cfg.disableArenaVisuals,
            v => { cfg.disableArenaVisuals = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisuals.ApplyState(cfg); });

        ToggleRow(contentScrollViewContent, "Disable arena props", cfg.disableArenaProps,
            v => { cfg.disableArenaProps = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisuals.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable arena lights", cfg.disableArenaLights,
            v => { cfg.disableArenaLights = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisuals.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable custom skybox", cfg.disableArenaSkybox,
            v => { cfg.disableArenaSkybox = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisuals.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable arena particles", cfg.disableArenaParticles,
            v => { cfg.disableArenaParticles = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisuals.ApplyPartial(cfg); });

        var audioRow = UITools.CreateConfigurationRow();
        audioRow.Add(UITools.CreateConfigurationLabel("Arena ambient audio volume"));
        var audioSlider = UITools.CreateConfigurationSlider(0f, 1f, cfg.arenaAudioVolume, 300);
        audioSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            cfg.arenaAudioVolume = evt.newValue;
            runner.SaveAndRefresh();
            ToasterReskinLoader.qol.ArenaVisuals.ApplyPartial(cfg);
        });
        audioRow.Add(audioSlider);
        contentScrollViewContent.Add(audioRow);

        */

        // ── Additions (opt-in enhancements layered on top of vanilla) ──
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Additions");
        Note(contentScrollViewContent,
            "Larger optional enhancements that go beyond small base-game patches.");

        ToggleRow(contentScrollViewContent, "Use enhanced friends list", cfg.enableBetterFriendsList,
            v =>
            {
                cfg.enableBetterFriendsList = v;
                runner.SaveAndRefresh();
                if (v) BetterFriendsList.Enable();
                else   BetterFriendsList.Disable();
            });

        ToggleRow(contentScrollViewContent, "Show party members in locker room", cfg.enablePartyLineup,
            v =>
            {
                cfg.enablePartyLineup = v;
                runner.SaveAndRefresh();
                PartyLineup.RefreshFromConfig();
            });

        ToggleRow(contentScrollViewContent, "Enable matchmaking beacon ping panel", cfg.enableBeaconPing,
            v =>
            {
                cfg.enableBeaconPing = v;
                runner.SaveAndRefresh();
                if (v) BeaconPing.Enable();
                else   BeaconPing.Disable();
            });

        ToggleRow(contentScrollViewContent, "Darken vanilla checkbox/input backgrounds", cfg.enableVanillaUIRetheme,
            v =>
            {
                cfg.enableVanillaUIRetheme = v;
                runner.SaveAndRefresh();
                if (v) VanillaUIRetheme.Enable();
                else   VanillaUIRetheme.Disable();
            });

        // ── Developer-oriented toggles (bottom — least relevant to most players) ──
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Developer");
        Note(contentScrollViewContent,
            "Tools intended for development and debugging. Safe to ignore as a regular player.");

        ToggleRow(contentScrollViewContent, "Enable in-game dev console", cfg.enableDevConsole,
            v => { cfg.enableDevConsole = v; runner.SaveAndRefresh(); });

        var devButtonsRow = UITools.CreateConfigurationRow();
        devButtonsRow.style.justifyContent = Justify.FlexStart;
        var openConsoleBtn = new Button(() =>
        {
            // Make sure the feature is enabled before trying to open — Open()
            // would silently no-op via the Update() guard otherwise.
            if (!cfg.enableDevConsole)
            {
                cfg.enableDevConsole = true;
                runner.SaveAndRefresh();
            }
            DevConsole.Instance?.Open();
        }) { text = "Open dev console" };
        UITools.StyleConfigButton(openConsoleBtn);
        openConsoleBtn.style.marginRight = 8;
        devButtonsRow.Add(openConsoleBtn);

        var openLogsBtn = new Button(DevConsole.OpenLogsFolder) { text = "Open logs folder" };
        UITools.StyleConfigButton(openLogsBtn);
        devButtonsRow.Add(openLogsBtn);
        contentScrollViewContent.Add(devButtonsRow);
    }

    // ─────────────────────────────── helpers ────────────────────────────────

    private static void Header(VisualElement parent, string text)
    {
        var h = new Label($"<b>{text}</b>");
        h.style.fontSize = 20;
        h.style.color = Color.white;
        h.style.marginTop = 4;
        h.style.marginBottom = 8;
        parent.Add(h);
    }

    // Single-line entry-store row: title + entry count + enable toggle +
    // expand chevron. Clicking the chevron / title toggles the inline
    // list visibility. The toggle is independent of the expand state.
    private static void CompactStoreRow(
        VisualElement parent,
        string title,
        Func<bool> getEnabled,
        Func<int> getCount,
        Action<bool> onChange,
        VisualElement list,
        Action rebuildList)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 10;
        row.style.marginBottom = 2;

        // Geometric Shapes block (U+25BC/B6) — same family as vanilla's
        // sort-direction arrows in UIServerBrowser, so we know the
        // game's font has them. The "small triangle" variants (▸/▾)
        // are not in NotInter and render as missing-glyph boxes.
        var titleLbl = new Label($"<b>{title}</b>");
        titleLbl.style.fontSize = 15;
        titleLbl.style.color = Color.white;
        row.Add(titleLbl);

        // flexGrow=1 pushes the count + toggle to the far right edge.
        var chevron = new Label("▶");
        chevron.style.fontSize = 12;
        chevron.style.color = new Color(0.7f, 0.7f, 0.7f);
        chevron.style.marginLeft = 8;
        chevron.style.flexGrow = 1;
        chevron.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(chevron);

        int count = getCount();
        var countLbl = new Label($"({count})");
        countLbl.style.fontSize = 12;
        countLbl.style.color = new Color(0.65f, 0.65f, 0.65f);
        countLbl.style.marginRight = 10;
        row.Add(countLbl);

        var t = UITools.CreateConfigurationCheckbox(getEnabled());
        t.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            onChange(evt.newValue);
            countLbl.text = $"({getCount()})";
        });
        row.Add(t);

        parent.Add(row);

        // Initially collapsed.
        list.style.display = DisplayStyle.None;
        list.style.marginLeft = 18;
        list.style.marginBottom = 8;
        parent.Add(list);

        void ToggleExpand()
        {
            bool nowVisible = list.style.display == DisplayStyle.None;
            list.style.display = nowVisible ? DisplayStyle.Flex : DisplayStyle.None;
            chevron.text = nowVisible ? "▼" : "▶"; // ▼ expanded / ▶ collapsed
            if (nowVisible) rebuildList();
            countLbl.text = $"({getCount()})";
        }

        chevron.RegisterCallback<ClickEvent>(_ => ToggleExpand());
        titleLbl.RegisterCallback<ClickEvent>(_ => ToggleExpand());
    }

    private static void Note(VisualElement parent, string text)
    {
        var n = UITools.CreateConfigurationLabel(text);
        n.style.marginBottom = 8;
        n.style.fontSize = 13;
        n.style.color = new Color(0.7f, 0.7f, 0.7f);
        n.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(n);
    }

    private static void Separator(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        sep.style.marginTop = 16;
        sep.style.marginBottom = 16;
        parent.Add(sep);
    }

    private static void RebuildSavedPasswordsList(VisualElement container)
    {
        if (container == null) return;
        container.Clear();

        var runner = QoLRunner.Instance;
        var cfg = runner?.Config;
        if (cfg == null) return;
        if (!cfg.enableSavedServerPasswords) return;

        var keys = SavedServerPasswords.SnapshotKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel("No saved passwords yet.");
            empty.style.color = new Color(0.65f, 0.65f, 0.65f);
            empty.style.marginTop = 4;
            empty.style.marginBottom = 4;
            container.Add(empty);
            return;
        }

        foreach (var key in keys)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            // Friendly name when the server browser has pinged this
            // ip:port at least once this session — otherwise fall back
            // to bare ip:port.
            string serverName = SavedServerPasswords.GetCachedServerName(key);

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var primary = UITools.CreateConfigurationLabel(
                string.IsNullOrEmpty(serverName) ? key : serverName);
            labelStack.Add(primary);

            if (!string.IsNullOrEmpty(serverName))
            {
                var subtitle = UITools.CreateConfigurationLabel(key);
                subtitle.style.fontSize = 11;
                subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
                subtitle.style.marginTop = 0;
                labelStack.Add(subtitle);
            }

            row.Add(labelStack);

            var forgetBtn = new Button(() =>
            {
                SavedServerPasswords.Remove(key);
                RebuildSavedPasswordsList(container);
            })
            { text = "Forget" };
            UITools.StyleConfigButton(forgetBtn);
            forgetBtn.style.marginLeft = 8;
            row.Add(forgetBtn);

            container.Add(row);
        }

        var clearAllRow = UITools.CreateConfigurationRow();
        clearAllRow.style.justifyContent = Justify.FlexEnd;
        clearAllRow.style.marginTop = 8;
        var clearAllBtn = new Button(() =>
        {
            SavedServerPasswords.RemoveAll();
            RebuildSavedPasswordsList(container);
        })
        { text = "Forget all saved passwords" };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    // Mirrors RebuildSavedPasswordsList for the favorites store. The
    // friendly name comes from the cached value the star button wrote
    // when the user clicked it; we don't ping live for it here.
    private static void RebuildFavoritesList(VisualElement container)
    {
        if (container == null) return;
        container.Clear();

        var keys = ServerBrowserSort.SnapshotFavoriteKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel("No favorites yet — click the ★ on a server row to favorite it.");
            empty.style.color = new Color(0.65f, 0.65f, 0.65f);
            empty.style.marginTop = 4;
            empty.style.marginBottom = 4;
            container.Add(empty);
            return;
        }

        foreach (var key in keys)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            string cached = ServerBrowserSort.GetFavoriteCachedName(key);

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var primary = UITools.CreateConfigurationLabel(
                string.IsNullOrEmpty(cached) ? key : cached);
            labelStack.Add(primary);

            if (!string.IsNullOrEmpty(cached))
            {
                var subtitle = UITools.CreateConfigurationLabel(key);
                subtitle.style.fontSize = 11;
                subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
                subtitle.style.marginTop = 0;
                labelStack.Add(subtitle);
            }

            row.Add(labelStack);

            var removeBtn = new Button(() =>
            {
                ServerBrowserSort.RemoveFavorite(key);
                RebuildFavoritesList(container);
                ServerBrowserSort.RefreshForCurrentBrowser();
            })
            { text = "Remove" };
            UITools.StyleConfigButton(removeBtn);
            removeBtn.style.marginLeft = 8;
            row.Add(removeBtn);

            container.Add(row);
        }

        var clearAllRow = UITools.CreateConfigurationRow();
        clearAllRow.style.justifyContent = Justify.FlexEnd;
        clearAllRow.style.marginTop = 8;
        var clearAllBtn = new Button(() =>
        {
            ServerBrowserSort.RemoveAllFavorites();
            RebuildFavoritesList(container);
            ServerBrowserSort.RefreshForCurrentBrowser();
        })
        { text = "Remove all favorites" };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    // Mirrors RebuildFavoritesList for the blocked-servers store.
    private static void RebuildBlockedList(VisualElement container)
    {
        if (container == null) return;
        container.Clear();

        var keys = ServerBrowserSort.SnapshotBlockedKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel("No blocked servers — right-click a row in the server browser to block.");
            empty.style.color = new Color(0.65f, 0.65f, 0.65f);
            empty.style.marginTop = 4;
            empty.style.marginBottom = 4;
            container.Add(empty);
            return;
        }

        foreach (var key in keys)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            string cached = ServerBrowserSort.GetBlockedCachedName(key);

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var primary = UITools.CreateConfigurationLabel(
                string.IsNullOrEmpty(cached) ? key : cached);
            labelStack.Add(primary);

            if (!string.IsNullOrEmpty(cached))
            {
                var subtitle = UITools.CreateConfigurationLabel(key);
                subtitle.style.fontSize = 11;
                subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
                subtitle.style.marginTop = 0;
                labelStack.Add(subtitle);
            }

            row.Add(labelStack);

            var unblockBtn = new Button(() =>
            {
                ServerBrowserSort.RemoveBlock(key);
                RebuildBlockedList(container);
                ServerBrowserSort.RefreshForCurrentBrowser();
            })
            { text = "Unblock" };
            UITools.StyleConfigButton(unblockBtn);
            unblockBtn.style.marginLeft = 8;
            row.Add(unblockBtn);

            container.Add(row);
        }

        var clearAllRow = UITools.CreateConfigurationRow();
        clearAllRow.style.justifyContent = Justify.FlexEnd;
        clearAllRow.style.marginTop = 8;
        var clearAllBtn = new Button(() =>
        {
            ServerBrowserSort.RemoveAllBlocks();
            RebuildBlockedList(container);
            ServerBrowserSort.RefreshForCurrentBrowser();
        })
        { text = "Unblock all servers" };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    // Mirrors RebuildSavedPasswordsList for the trusted-mods store —
    // populated by MissingModsPopupSuppression when the user ticks the
    // popup's "Don't show this popup again" toggle.
    private static void RebuildTrustedServersList(VisualElement container)
    {
        if (container == null) return;
        container.Clear();

        var keys = MissingModsPopupSuppression.SnapshotKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel("No trusted servers yet.");
            empty.style.color = new Color(0.65f, 0.65f, 0.65f);
            empty.style.marginTop = 4;
            empty.style.marginBottom = 4;
            container.Add(empty);
            return;
        }

        foreach (var key in keys)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            string serverName = SavedServerPasswords.GetCachedServerName(key);
            int modCount = MissingModsPopupSuppression.CountModsFor(key);

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var primary = UITools.CreateConfigurationLabel(
                string.IsNullOrEmpty(serverName) ? key : serverName);
            labelStack.Add(primary);

            string subtitleText = string.IsNullOrEmpty(serverName)
                ? $"{modCount} mod{(modCount == 1 ? "" : "s")} trusted"
                : $"{key} — {modCount} mod{(modCount == 1 ? "" : "s")} trusted";
            var subtitle = UITools.CreateConfigurationLabel(subtitleText);
            subtitle.style.fontSize = 11;
            subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
            subtitle.style.marginTop = 0;
            labelStack.Add(subtitle);

            row.Add(labelStack);

            var forgetBtn = new Button(() =>
            {
                MissingModsPopupSuppression.Remove(key);
                RebuildTrustedServersList(container);
            })
            { text = "Untrust" };
            UITools.StyleConfigButton(forgetBtn);
            forgetBtn.style.marginLeft = 8;
            row.Add(forgetBtn);

            container.Add(row);
        }

        var clearAllRow = UITools.CreateConfigurationRow();
        clearAllRow.style.justifyContent = Justify.FlexEnd;
        clearAllRow.style.marginTop = 8;
        var clearAllBtn = new Button(() =>
        {
            MissingModsPopupSuppression.RemoveAll();
            RebuildTrustedServersList(container);
        })
        { text = "Untrust all servers" };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    private static VisualElement ToggleRow(VisualElement parent, string label, bool initial, Action<bool> onChange)
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var t = UITools.CreateConfigurationCheckbox(initial);
        t.RegisterCallback<ChangeEvent<bool>>(evt => onChange(evt.newValue));
        row.Add(t);
        parent.Add(row);
        return row;
    }

}
