// Chat & Scoreboard tweaks. Part of the "Tweaks" sidebar group.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.hud;

namespace ToasterReskinLoader.ui.sections;

public static class ChatScoreboardSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Chat readability and in-game clock polish.");
        if (cfg == null) return;
        var runner = QoLRunner.Instance;

        SettingsUI.ToggleRow(root, "Drag-highlight and copy lines", cfg.enableChatDragSelect,
            v => { cfg.enableChatDragSelect = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Hide when inactive", cfg.enableHideInactiveChat,
            v =>
            {
                cfg.enableHideInactiveChat = v;
                runner.SaveAndRefresh();
                HideInactiveChat.RefreshVisualState();
            });
        SettingsUI.ToggleRow(root, "Keep expired messages at full opacity", cfg.enableChatNoFade,
            v =>
            {
                cfg.enableChatNoFade = v;
                runner.SaveAndRefresh();
                HideInactiveChat.RefreshVisualState();
            });

        SettingsUI.ToggleRow(root, "Clock shows milliseconds", cfg.enableScoreboardMilliseconds,
            v => { cfg.enableScoreboardMilliseconds = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Clock turns red in final 30s", cfg.enableScoreboardClockColor,
            v => { cfg.enableScoreboardClockColor = v; runner.SaveAndRefresh(); });
    }
}
