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

        // ── Common UX fixes (top of the page; what most players will care about) ──
        Header(contentScrollViewContent, "Base-game UX patches");
        Note(contentScrollViewContent,
            "Small Harmony patches that polish the vanilla menu and chat behavior. Each can be turned off independently.");

        ToggleRow(contentScrollViewContent, "Close secondary menus with ESC", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Open chat in any in-game phase", cfg.enableChatAnyInGamePhase,
            v => { cfg.enableChatAnyInGamePhase = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Enable scoreboard in any in-game phase", cfg.enableScoreboardAnyInGamePhase,
            v => { cfg.enableScoreboardAnyInGamePhase = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Drag-highlight and copy chat lines", cfg.enableChatDragSelect,
            v => { cfg.enableChatDragSelect = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Hide chat when inactive", cfg.enableHideInactiveChat,
            v => { cfg.enableHideInactiveChat = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show minimap for spectators", cfg.enableSpectatorMinimap,
            v => { cfg.enableSpectatorMinimap = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show jersey number in player name", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show player count on team select buttons", cfg.enableTeamButtonPlayerCount,
            v => { cfg.enableTeamButtonPlayerCount = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show server browser filters inline", cfg.enableInlineServerBrowserFilters,
            v =>
            {
                cfg.enableInlineServerBrowserFilters = v;
                runner.SaveAndRefresh();
                if (v) InlineServerBrowserFilters.ReapplyInlineFiltersForCurrent();
                else   InlineServerBrowserFilters.UndoInlineFiltersForCurrent();
            });

        ToggleRow(contentScrollViewContent, "Remember server browser filters", cfg.enableBrowserFilterPersistence,
            v => { cfg.enableBrowserFilterPersistence = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent,
            "Improve server browser sort order",
            cfg.enableServerBrowserSortTweaks,
            v =>
            {
                cfg.enableServerBrowserSortTweaks = v;
                runner.SaveAndRefresh();
                ServerBrowserSort.RefreshForCurrentBrowser();
            });

        ToggleRow(contentScrollViewContent,
            "Restore Unicode glyphs (sort arrows, accents, etc.)",
            cfg.enableUnicodeFontFallback,
            v =>
            {
                cfg.enableUnicodeFontFallback = v;
                runner.SaveAndRefresh();
                if (v) UnicodeFontFallback.Apply();
                else   UnicodeFontFallback.Disable();
            });

        // ── Trusted server mod lists (DISABLED) ───────────────────────────
        // The MODS REQUIRED popup suppression is shelved for now — see
        // MissingModsPopupSuppression.cs. When re-enabling, uncomment
        // both this section and the Harmony patch classes over there.
        /*
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Trusted Server Mod Lists");
        Note(contentScrollViewContent,
            "Servers you ticked \"Don't show this popup again for this server\" on the MODS REQUIRED prompt. "
            + "If a listed server changes its required mods, the entry is invalidated automatically and the popup will return.");

        var trustedServersList = new VisualElement();
        trustedServersList.style.marginTop = 4;
        contentScrollViewContent.Add(trustedServersList);
        RebuildTrustedServersList(trustedServersList);
        */

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

        ToggleRow(contentScrollViewContent, "Cache server browser (instant rows on open)", cfg.enableServerPreviewCache,
            v => { cfg.enableServerPreviewCache = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Darken vanilla checkbox/input backgrounds", cfg.enableVanillaUIRetheme,
            v =>
            {
                cfg.enableVanillaUIRetheme = v;
                runner.SaveAndRefresh();
                if (v) VanillaUIRetheme.Enable();
                else   VanillaUIRetheme.Disable();
            });

        // ── Saved server passwords ─────────────────────────────────────────
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Saved Server Passwords");
        Note(contentScrollViewContent,
            "When you join a passworded server, a \"Remember password\" checkbox appears on the prompt. "
            + "If the server changes its password, the password will be forgotten.");

        var savedPasswordsList = new VisualElement();
        savedPasswordsList.style.marginTop = 4;

        ToggleRow(contentScrollViewContent, "Enable saved server passwords", cfg.enableSavedServerPasswords,
            v =>
            {
                cfg.enableSavedServerPasswords = v;
                runner.SaveAndRefresh();
                RebuildSavedPasswordsList(savedPasswordsList);
            });

        contentScrollViewContent.Add(savedPasswordsList);
        RebuildSavedPasswordsList(savedPasswordsList);

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

    // Mirrors RebuildSavedPasswordsList for the trusted-mods store. Kept
    // around but commented out alongside the MODS REQUIRED popup
    // suppression feature; uncomment together when re-enabling.
    /*
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
    */

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
