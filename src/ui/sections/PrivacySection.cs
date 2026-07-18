using UnityEngine;
using UnityEngine.UIElements;

using ToasterReskinLoader.api;

namespace ToasterReskinLoader.ui.sections;

// Opt-out toggles for the anonymous usage telemetry TRL sends to puckstats once
// per game launch. Kept in its own bottom-of-sidebar page (not Appearance/Extras/
// Developer) so the data-sharing choices are easy to find and self-contained.
// Both default on; turning one off stops that send and purges the stored data.
public static class PrivacySection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        SettingsUI.Note(contentScrollViewContent,
            "Toaster's Reskin Loader can share anonymous usage stats with puckstats.io " +
            "to power the reskin gallery (which skins people actually use) and to help " +
            "prioritize which mod features to improve. Data is sent once per game launch. " +
            "You can opt out of either below — turning one off also tells the server to " +
            "forget what it has already stored for you. Server passwords and other private " +
            "data are never sent.");

        SettingsUI.Header(contentScrollViewContent, "Reskin usage");
        SettingsUI.Note(contentScrollViewContent,
            "Which reskins you have equipped (per slot/team) and your puck randomizer list. " +
            "Used to rank reskins by real usage on the public gallery.");
        SettingsUI.ToggleRow(contentScrollViewContent, "Share Reskin Usage Analytics",
            Plugin.modSettings.ShareReskinAnalytics, on =>
            {
                Plugin.modSettings.ShareReskinAnalytics = on;
                Plugin.modSettings.Save();
                if (on)
                    UsageAnalyticsAPI.QueuePost();          // resume sharing
                else
                    UsageAnalyticsAPI.PurgeReskinEquips();  // stop + forget stored equips
            });

        SettingsUI.Header(contentScrollViewContent, "Mod settings");
        SettingsUI.Note(contentScrollViewContent,
            "Which quality-of-life toggles and options you have enabled. Internal only — " +
            "used to see which features get used. Never surfaced publicly.");
        SettingsUI.ToggleRow(contentScrollViewContent, "Share Settings Analytics",
            Plugin.modSettings.ShareSettingsAnalytics, on =>
            {
                Plugin.modSettings.ShareSettingsAnalytics = on;
                Plugin.modSettings.Save();
                if (on)
                    UsageAnalyticsAPI.QueuePost();       // resume sharing
                else
                    UsageAnalyticsAPI.PurgeSettings();   // stop + forget stored settings
            });
    }
}
