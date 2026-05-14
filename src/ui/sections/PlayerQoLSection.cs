// Quality of Life tab — small base-game UX patches plus a few opt-in
// extras (arena visual disable, in-game dev console). Single scrollable
// page; ordered so the common everyday-player UX fixes are at the top
// and the developer/arena-tweak controls are further down.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToasterReskinLoader.qol;
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

        ToggleRow(contentScrollViewContent, "ESC closes secondary menus", cfg.enableEscCloseMenus,
            v => { cfg.enableEscCloseMenus = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Open chat in any in-game phase", cfg.enableChatAnyInGamePhase,
            v => { cfg.enableChatAnyInGamePhase = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "View scoreboard in any in-game phase", cfg.enableScoreboardAnyInGamePhase,
            v => { cfg.enableScoreboardAnyInGamePhase = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Drag-select and right-click-copy chat lines", cfg.enableChatDragSelect,
            v => { cfg.enableChatDragSelect = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Hide chat when inactive", cfg.enableHideInactiveChat,
            v => { cfg.enableHideInactiveChat = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show minimap for spectators", cfg.enableSpectatorMinimap,
            v => { cfg.enableSpectatorMinimap = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Show jersey number in player name", cfg.enableNumberedNames,
            v => { cfg.enableNumberedNames = v; runner.SaveAndRefresh(); });

        ToggleRow(contentScrollViewContent, "Inline server browser filters", cfg.enableInlineServerBrowserFilters,
            v =>
            {
                cfg.enableInlineServerBrowserFilters = v;
                runner.SaveAndRefresh();
                if (v) InlineServerBrowserFilters.ReapplyInlineFiltersForCurrent();
                else   InlineServerBrowserFilters.UndoInlineFiltersForCurrent();
            });

        ToggleRow(contentScrollViewContent, "Remember server browser filters", cfg.enableBrowserFilterPersistence,
            v => { cfg.enableBrowserFilterPersistence = v; runner.SaveAndRefresh(); });

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

        // ── Developer-oriented toggles (bottom — least relevant to most players) ──
        Separator(contentScrollViewContent);
        Header(contentScrollViewContent, "Developer");
        Note(contentScrollViewContent,
            "Tools intended for development and debugging. Safe to ignore as a regular player.");

        ToggleRow(contentScrollViewContent, "Enable dev console", cfg.enableDevConsole,
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
        openConsoleBtn.style.marginRight = 8;
        devButtonsRow.Add(openConsoleBtn);

        var openLogsBtn = new Button(DevConsole.OpenLogsFolder) { text = "Open logs folder" };
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
