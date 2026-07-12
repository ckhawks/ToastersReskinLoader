// Developer tools — dev console and frame profiler. Part of the "Tweaks"
// sidebar group. Safe to ignore as a regular player.

using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.diagnostics;
using ToasterReskinLoader.diagnostics.profiler;

namespace ToasterReskinLoader.ui.sections;

public static class DeveloperSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Tools intended for development and debugging. Safe to ignore as a regular player.");
        if (cfg == null) return;

        SettingsUI.ToggleRow(root, "Enable in-game dev console", cfg.enableDevConsole,
            v => { cfg.enableDevConsole = v; Settings.Save(); });

        SettingsUI.ToggleRow(root,
            "Enable frame profiler overlay (F4 cycles mode, F5 toggles CSV log)",
            cfg.enableFrameProfiler,
            v =>
            {
                cfg.enableFrameProfiler = v;
                Settings.Save();
                if (v) FrameProfiler.Enable(); else FrameProfiler.Disable();
            });

        SettingsUI.ToggleRow(root,
            "  └ Also instrument other mods (per-mod cost rows; adds many Harmony patches)",
            cfg.enableFrameProfilerModInstrumentation,
            v =>
            {
                cfg.enableFrameProfilerModInstrumentation = v;
                Settings.Save();
                // If the profiler is currently running, cycle it so the
                // per-mod patches get applied (or removed) immediately
                // instead of waiting for next startup.
                if (cfg.enableFrameProfiler)
                {
                    FrameProfiler.Disable();
                    FrameProfiler.Enable();
                }
            });

        SettingsUI.ToggleRow(root,
            "Log per-mod enable timing at startup ([EnableTiming] lines; mods after TRL only)",
            cfg.enablePluginEnableTiming,
            v =>
            {
                cfg.enablePluginEnableTiming = v;
                Settings.Save();
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
                Settings.Save();
            }
            DevConsole.Instance?.Open();
        }) { text = "Open dev console" };
        UITools.StyleConfigButton(openConsoleBtn);
        openConsoleBtn.style.marginRight = 8;
        devButtonsRow.Add(openConsoleBtn);

        var openLogsBtn = new Button(DevConsole.OpenLogsFolder) { text = "Open logs folder" };
        UITools.StyleConfigButton(openLogsBtn);
        devButtonsRow.Add(openLogsBtn);
        root.Add(devButtonsRow);
    }
}
