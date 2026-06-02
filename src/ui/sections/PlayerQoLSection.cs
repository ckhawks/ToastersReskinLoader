// Quality of Life tab — small base-game UX patches plus a few opt-in
// extras (arena visual disable, in-game dev console). Single scrollable
// page; ordered so the common everyday-player UX fixes are at the top
// and the developer/arena-tweak controls are further down.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToasterReskinLoader.qol;
using ToasterReskinLoader.core;
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

        // Minimap rotation mode — mutually exclusive dropdown.
        {
            var row = UITools.CreateConfigurationRow();
            row.Add(UITools.CreateConfigurationLabel("Minimap rotation"));

            var labels = new List<string> { "Off (vanilla)", "Rotated 90°", "Follow player orientation" };
            var values = new List<string> { "off", "rotate90", "followPlayer" };
            var currentIdx = Math.Max(0, values.IndexOf(cfg.minimapRotationMode ?? "off"));

            var dd = UITools.CreateStringDropdownField(labels, labels[currentIdx]);
            dd.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = labels.IndexOf(evt.newValue);
                if (idx < 0) idx = 0;
                cfg.minimapRotationMode = values[idx];
                runner.SaveAndRefresh();
            });
            row.Add(dd);
            contentScrollViewContent.Add(row);
        }

        ToggleRow(contentScrollViewContent, "Color floating player names by team", cfg.enablePlayerUsernameTeamColors,
            v => { cfg.enablePlayerUsernameTeamColors = v; runner.SaveAndRefresh(); });
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

        ToggleRow(contentScrollViewContent, "Fix shared player country flags", cfg.enableFlagMaterialFix,
            v => { cfg.enableFlagMaterialFix = v; runner.SaveAndRefresh(); });
        Note(contentScrollViewContent,
            "Vanilla bug: every player's helmet flag shares one material, so everyone ends up showing the same flag. "
            + "Takes effect as players (re)spawn.");

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
        ToggleRow(contentScrollViewContent, "Fast server browser scanning (parallel pings)", cfg.enableFastServerBrowserScanning,
            v => { cfg.enableFastServerBrowserScanning = v; runner.SaveAndRefresh(); });

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
                // Re-style open browser rows so the 🔓 auto-fill badge
                // appears/disappears live (the badge rides this toggle,
                // independent of favorites/blocks).
                ServerBrowserSort.RefreshForCurrentBrowser();
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
            v => { cfg.disableArenaVisuals = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyState(cfg); });

        ToggleRow(contentScrollViewContent, "Disable arena props", cfg.disableArenaProps,
            v => { cfg.disableArenaProps = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable arena lights", cfg.disableArenaLights,
            v => { cfg.disableArenaLights = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable custom skybox", cfg.disableArenaSkybox,
            v => { cfg.disableArenaSkybox = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyPartial(cfg); });
        ToggleRow(contentScrollViewContent, "Disable arena particles", cfg.disableArenaParticles,
            v => { cfg.disableArenaParticles = v; runner.SaveAndRefresh(); ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyPartial(cfg); });

        var audioRow = UITools.CreateConfigurationRow();
        audioRow.Add(UITools.CreateConfigurationLabel("Arena ambient audio volume"));
        var audioSlider = UITools.CreateConfigurationSlider(0f, 1f, cfg.arenaAudioVolume, 300);
        audioSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            cfg.arenaAudioVolume = evt.newValue;
            runner.SaveAndRefresh();
            ToasterReskinLoader.qol.ArenaVisualsToggle.ApplyPartial(cfg);
        });
        audioRow.Add(audioSlider);
        contentScrollViewContent.Add(audioRow);

        */

        // ── Input ──────────────────────────────────────────────────────────
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Input");
        Note(contentScrollViewContent,
            "Disable all connected game controllers (gamepads/joysticks). Use this if a plugged-in "
            + "controller steals UI focus in menus so your first click on a button gets eaten. Applies "
            + "immediately and to controllers connected later; turn it off to restore controller support. "
            + "Note: if your controller's stick also drifts the mouse cursor, that's a Steam Input / "
            + "DS4Windows stick-to-mouse mapping and must be turned off there — it arrives as real "
            + "mouse movement this can't block.");

        ToggleRow(contentScrollViewContent, "Disable controller / gamepad input", cfg.disableControllerInput,
            v =>
            {
                cfg.disableControllerInput = v;
                runner.SaveAndRefresh();
                DisableControllerInput.Apply(v);
            });

        // ── Additions (opt-in enhancements layered on top of vanilla) ──
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Additions");
        Note(contentScrollViewContent,
            "Larger optional enhancements that go beyond small base-game patches.");

        ToggleRow(contentScrollViewContent, "Free-look camera in position select", cfg.enablePositionSelectFreeLook,
            v => { cfg.enablePositionSelectFreeLook = v; runner.SaveAndRefresh(); });
        Note(contentScrollViewContent,
            "Right-click to fly around like a spectator while picking a position; right-click or Esc to return.");

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

        ToggleRow(contentScrollViewContent, "Auto-connect to matchmaking matches", cfg.enableAutoConnectMatchmaking,
            v =>
            {
                cfg.enableAutoConnectMatchmaking = v;
                runner.SaveAndRefresh();
                if (v) AutoConnectMatchmaking.Enable();
                else   AutoConnectMatchmaking.Disable();
            });

        ToggleRow(contentScrollViewContent, "Use enhanced mod menu (search, sort, badges, update checker)", cfg.enableEnhancedModMenu,
            v => { cfg.enableEnhancedModMenu = v; runner.SaveAndRefresh(); });
        Note(contentScrollViewContent,
            "Restart the game for an off→on toggle to take full effect; changes apply to the next mod menu open.");

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

        ToggleRow(contentScrollViewContent,
            "Enable frame profiler overlay (F4 cycles mode, F5 toggles CSV log)",
            cfg.enableFrameProfiler,
            v =>
            {
                cfg.enableFrameProfiler = v;
                runner.SaveAndRefresh();
                if (v) FrameProfiler.Enable(); else FrameProfiler.Disable();
            });

        ToggleRow(contentScrollViewContent,
            "  └ Also instrument other mods (per-mod cost rows; adds many Harmony patches)",
            cfg.enableFrameProfilerModInstrumentation,
            v =>
            {
                cfg.enableFrameProfilerModInstrumentation = v;
                runner.SaveAndRefresh();
                // If the profiler is currently running, cycle it so the
                // per-mod patches get applied (or removed) immediately
                // instead of waiting for next startup.
                if (cfg.enableFrameProfiler)
                {
                    FrameProfiler.Disable();
                    FrameProfiler.Enable();
                }
            });

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

        // Expand chevron on the LEFT, before the title — consistent with
        // the accordion arrows used elsewhere in the menu. Geometric Shapes
        // block (U+25B6/BC) — same family as vanilla's sort-direction arrows
        // in UIServerBrowser, so we know the game's font has them. The "small
        // triangle" variants (▸/▾) are not in NotInter and render as
        // missing-glyph boxes.
        var chevron = new Label("▶");
        chevron.style.fontSize = 12;
        chevron.style.color = new Color(0.7f, 0.7f, 0.7f);
        chevron.style.width = 14;
        chevron.style.marginRight = 6;
        chevron.style.unityTextAlign = TextAnchor.MiddleCenter;
        chevron.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(chevron);

        // flexGrow=1 on the title pushes the count + toggle to the far right edge.
        var titleLbl = new Label($"<b>{title}</b>");
        titleLbl.style.fontSize = 15;
        titleLbl.style.color = Color.white;
        titleLbl.style.flexGrow = 1;
        row.Add(titleLbl);

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

    // Shared shape for the four server-browser-side "stores" rendered as
    // an inline managed list (Saved Passwords / Favorites / Blocked /
    // Trusted Mod Lists). They differ only in key source, label text,
    // button labels, and the remove/clear actions — captured here so a
    // single RebuildStoreList renders all four.
    private sealed class StoreListSpec
    {
        public Func<bool> Gate;                                   // null = always shown
        public Func<List<string>> GetKeys;
        public string EmptyMessage;
        public Func<string, (string primary, string subtitle)> Labels;
        public string RemoveText;
        public Action<string> Remove;
        public string ClearAllText;
        public Action ClearAll;
        public Action AfterMutate;                                // null = nothing extra
    }

    private static void RebuildStoreList(VisualElement container, StoreListSpec spec)
    {
        if (container == null) return;
        container.Clear();
        if (spec.Gate != null && !spec.Gate()) return;

        void Rebuild() => RebuildStoreList(container, spec);

        var keys = spec.GetKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel(spec.EmptyMessage);
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

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var (primaryText, subtitleText) = spec.Labels(key);
            labelStack.Add(UITools.CreateConfigurationLabel(primaryText));

            if (!string.IsNullOrEmpty(subtitleText))
            {
                var subtitle = UITools.CreateConfigurationLabel(subtitleText);
                subtitle.style.fontSize = 11;
                subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
                subtitle.style.marginTop = 0;
                labelStack.Add(subtitle);
            }

            row.Add(labelStack);

            var removeBtn = new Button(() =>
            {
                spec.Remove(key);
                Rebuild();
                spec.AfterMutate?.Invoke();
            })
            { text = spec.RemoveText };
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
            spec.ClearAll();
            Rebuild();
            spec.AfterMutate?.Invoke();
        })
        { text = spec.ClearAllText };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    // Friendly server name (cached from a browser ping this session) on
    // top, bare ip:port as the subtitle when we have a name.
    private static void RebuildSavedPasswordsList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            Gate         = () => QoLRunner.Instance?.Config?.enableSavedServerPasswords ?? false,
            GetKeys      = SavedServerPasswords.SnapshotKeys,
            EmptyMessage = "No saved passwords yet.",
            Labels       = key =>
            {
                string name = SavedServerPasswords.GetCachedServerName(key);
                return (string.IsNullOrEmpty(name) ? key : name,
                        string.IsNullOrEmpty(name) ? null : key);
            },
            RemoveText   = "Forget",
            Remove       = SavedServerPasswords.Remove,
            ClearAllText = "Forget all saved passwords",
            ClearAll     = SavedServerPasswords.RemoveAll,
        });

    // Favorites store. Cached name comes from the value the star button
    // wrote on click; we don't ping live for it here. Re-sorts the open
    // browser after a removal so the row order updates immediately.
    private static void RebuildFavoritesList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = ServerBrowserSort.SnapshotFavoriteKeys,
            EmptyMessage = "No favorites yet — click the ★ on a server row to favorite it.",
            Labels       = key =>
            {
                string cached = ServerBrowserSort.GetFavoriteCachedName(key);
                return (string.IsNullOrEmpty(cached) ? key : cached,
                        string.IsNullOrEmpty(cached) ? null : key);
            },
            RemoveText   = "Remove",
            Remove       = ServerBrowserSort.RemoveFavorite,
            ClearAllText = "Remove all favorites",
            ClearAll     = ServerBrowserSort.RemoveAllFavorites,
            AfterMutate  = ServerBrowserSort.RefreshForCurrentBrowser,
        });

    // Blocked-servers store.
    private static void RebuildBlockedList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = ServerBrowserSort.SnapshotBlockedKeys,
            EmptyMessage = "No blocked servers — right-click a row in the server browser to block.",
            Labels       = key =>
            {
                string cached = ServerBrowserSort.GetBlockedCachedName(key);
                return (string.IsNullOrEmpty(cached) ? key : cached,
                        string.IsNullOrEmpty(cached) ? null : key);
            },
            RemoveText   = "Unblock",
            Remove       = ServerBrowserSort.RemoveBlock,
            ClearAllText = "Unblock all servers",
            ClearAll     = ServerBrowserSort.RemoveAllBlocks,
            AfterMutate  = ServerBrowserSort.RefreshForCurrentBrowser,
        });

    // Trusted-mods store — populated by MissingModsPopupSuppression when
    // the user ticks the popup's "Don't show this popup again" toggle. The
    // subtitle always shows the trusted mod count (and the ip:port when we
    // also have a friendly name).
    private static void RebuildTrustedServersList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = MissingModsPopupSuppression.SnapshotKeys,
            EmptyMessage = "No trusted servers yet.",
            Labels       = key =>
            {
                string name = SavedServerPasswords.GetCachedServerName(key);
                int modCount = MissingModsPopupSuppression.CountModsFor(key);
                string plural = modCount == 1 ? "" : "s";
                string subtitle = string.IsNullOrEmpty(name)
                    ? $"{modCount} mod{plural} trusted"
                    : $"{key} — {modCount} mod{plural} trusted";
                return (string.IsNullOrEmpty(name) ? key : name, subtitle);
            },
            RemoveText   = "Untrust",
            Remove       = MissingModsPopupSuppression.Remove,
            ClearAllText = "Untrust all servers",
            ClearAll     = MissingModsPopupSuppression.RemoveAll,
        });

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
