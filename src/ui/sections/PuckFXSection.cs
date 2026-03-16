using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PuckFXSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        // --- Puck Outline ---
        Label outlineHeader = UITools.CreateConfigurationLabel("<b>Puck Outline</b>");
        outlineHeader.style.marginTop = 10;
        outlineHeader.style.marginBottom = 4;
        contentScrollViewContent.Add(outlineHeader);

        var outlineEnabledRow = UITools.CreateConfigurationRow();
        outlineEnabledRow.Add(UITools.CreateConfigurationLabel("Enabled (game setting)"));
        var outlineEnabledToggle = UITools.CreateConfigurationCheckbox(
            SettingsManager.Instance.ShowPuckOutline > 0);
        outlineEnabledToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            SettingsManager.Instance.UpdateShowPuckOutline(evt.newValue);
            SyncGameSettingsUI();
        });
        outlineEnabledRow.Add(outlineEnabledToggle);
        contentScrollViewContent.Add(outlineEnabledRow);

        var outlineColorRow = UITools.CreateColorConfigurationRow(
            "Outline Color",
            ReskinProfileManager.currentProfile.puckFXOutlineColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.puckFXOutlineColor = newColor;
                PuckFXSwapper.ApplyAll();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(outlineColorRow);

        CreateSliderRow(
            contentScrollViewContent,
            "Outline Size",
            0,
            10,
            () => ReskinProfileManager.currentProfile.puckFXOutlineKernelSize,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXOutlineKernelSize = (int)val;
                PuckFXSwapper.ApplyAll();
            }
        );

        // --- Elevation Indicator ---
        Label elevationHeader = UITools.CreateConfigurationLabel("<b>Elevation Indicator</b>");
        elevationHeader.style.marginTop = 16;
        elevationHeader.style.marginBottom = 4;
        contentScrollViewContent.Add(elevationHeader);

        var elevationEnabledRow = UITools.CreateConfigurationRow();
        elevationEnabledRow.Add(UITools.CreateConfigurationLabel("Enabled (game setting)"));
        var elevationEnabledToggle = UITools.CreateConfigurationCheckbox(
            SettingsManager.Instance.ShowPuckElevation > 0);
        elevationEnabledToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            SettingsManager.Instance.UpdateShowPuckElevation(evt.newValue);
            SyncGameSettingsUI();
        });
        elevationEnabledRow.Add(elevationEnabledToggle);
        contentScrollViewContent.Add(elevationEnabledRow);

        var elevationColorRow = UITools.CreateColorConfigurationRow(
            "Indicator Color",
            ReskinProfileManager.currentProfile.puckFXElevationIndicatorColor,
            true,
            newColor =>
            {
                ReskinProfileManager.currentProfile.puckFXElevationIndicatorColor = newColor;
                PuckFXSwapper.ApplyAll();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(elevationColorRow);

        // --- Verticality Line ---
        Label verticalityHeader = UITools.CreateConfigurationLabel("<b>Verticality Line</b>");
        verticalityHeader.style.marginTop = 16;
        verticalityHeader.style.marginBottom = 4;
        contentScrollViewContent.Add(verticalityHeader);

        var verticalityColorRow = UITools.CreateColorConfigurationRow(
            "Line Color",
            ReskinProfileManager.currentProfile.puckFXVerticalityLineColor,
            true,
            newColor =>
            {
                ReskinProfileManager.currentProfile.puckFXVerticalityLineColor = newColor;
                PuckFXSwapper.ApplyAll();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(verticalityColorRow);

        CreateSliderRow(
            contentScrollViewContent,
            "Start Opacity",
            0f,
            1f,
            () => ReskinProfileManager.currentProfile.puckFXVerticalityLineStartAlpha,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXVerticalityLineStartAlpha = val;
                PuckFXSwapper.ApplyAll();
            }
        );

        CreateSliderRow(
            contentScrollViewContent,
            "End Opacity",
            0f,
            1f,
            () => ReskinProfileManager.currentProfile.puckFXVerticalityLineEndAlpha,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXVerticalityLineEndAlpha = val;
                PuckFXSwapper.ApplyAll();
            }
        );

        // --- Puck Silhouette ---
        Label silhouetteHeader = UITools.CreateConfigurationLabel("<b>Puck Silhouette</b>");
        silhouetteHeader.style.marginTop = 16;
        silhouetteHeader.style.marginBottom = 4;
        contentScrollViewContent.Add(silhouetteHeader);

        var silhouetteEnabledRow = UITools.CreateConfigurationRow();
        silhouetteEnabledRow.Add(UITools.CreateConfigurationLabel("Enabled (game setting)"));
        var silhouetteEnabledToggle = UITools.CreateConfigurationCheckbox(
            SettingsManager.Instance.ShowPuckSilhouette > 0);
        silhouetteEnabledToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            SettingsManager.Instance.UpdateShowPuckSilhouette(evt.newValue);
            SyncGameSettingsUI();
        });
        silhouetteEnabledRow.Add(silhouetteEnabledToggle);
        contentScrollViewContent.Add(silhouetteEnabledRow);

        var silhouetteColorRow = UITools.CreateColorConfigurationRow(
            "Silhouette Color",
            ReskinProfileManager.currentProfile.puckFXSilhouetteColor,
            true,
            newColor =>
            {
                ReskinProfileManager.currentProfile.puckFXSilhouetteColor = newColor;
                PuckFXSwapper.ApplyAll();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(silhouetteColorRow);

        // --- Puck Trail ---
        Label trailHeader = UITools.CreateConfigurationLabel("<b>Puck Trail</b>");
        trailHeader.style.marginTop = 16;
        trailHeader.style.marginBottom = 4;
        contentScrollViewContent.Add(trailHeader);

        Label trailNote = UITools.CreateConfigurationLabel(
            "Trail uses the built-in trail that already exists in the game. If a game update removes it, this section will stop working.");
        trailNote.style.fontSize = 12;
        trailNote.style.color = new Color(0.7f, 0.7f, 0.7f);
        trailNote.style.marginBottom = 4;
        contentScrollViewContent.Add(trailNote);

        var trailEnabledRow = UITools.CreateConfigurationRow();
        trailEnabledRow.Add(UITools.CreateConfigurationLabel("Trail Enabled"));
        var trailToggle = UITools.CreateConfigurationCheckbox(
            ReskinProfileManager.currentProfile.puckFXTrailEnabled);
        trailToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.puckFXTrailEnabled = evt.newValue;
            PuckFXSwapper.ApplyAll();
            ReskinProfileManager.SaveProfile();
        });
        trailEnabledRow.Add(trailToggle);
        contentScrollViewContent.Add(trailEnabledRow);

        var trailColorRow = UITools.CreateColorConfigurationRow(
            "Trail Color",
            ReskinProfileManager.currentProfile.puckFXTrailColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.puckFXTrailColor = newColor;
                PuckFXSwapper.ApplyAll();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(trailColorRow);

        CreateSliderRow(
            contentScrollViewContent,
            "Start Width",
            0f,
            1f,
            () => ReskinProfileManager.currentProfile.puckFXTrailStartWidth,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXTrailStartWidth = val;
                PuckFXSwapper.ApplyAll();
            }
        );

        CreateSliderRow(
            contentScrollViewContent,
            "End Width",
            0f,
            1f,
            () => ReskinProfileManager.currentProfile.puckFXTrailEndWidth,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXTrailEndWidth = val;
                PuckFXSwapper.ApplyAll();
            }
        );

        CreateSliderRow(
            contentScrollViewContent,
            "Lifetime (seconds)",
            0f,
            5f,
            () => ReskinProfileManager.currentProfile.puckFXTrailLifetime,
            val =>
            {
                ReskinProfileManager.currentProfile.puckFXTrailLifetime = val;
                PuckFXSwapper.ApplyAll();
            }
        );

        // Trail start/end opacity sliders are intentionally excluded from the UI.
        // The underlying TrailRenderer alpha gradient doesn't appear to have a visible
        // effect — the material color overrides it. The profile fields still exist
        // (puckFXTrailStartAlpha / puckFXTrailEndAlpha) so the plumbing is ready
        // if a workaround is found.

        // --- Reset Button ---
        Button resetButton = new Button
        {
            text = "Reset to default",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                fontSize = 18,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(resetButton);
        resetButton.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinProfileManager.ResetPuckFXToDefault();

            Label title = (Label)contentScrollViewContent.Children().ToArray()[0];
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);

            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);
    }

    /// <summary>
    /// Syncs the game's own settings UI to reflect changes we made via SettingsManager.
    /// The game UI only reads values on init, so we need to force a refresh.
    /// </summary>
    private static void SyncGameSettingsUI()
    {
        try
        {
            var settingsUI = Object.FindObjectOfType<UISettings>();
            settingsUI?.ApplySettingsValues();
        }
        catch (System.Exception e)
        {
            Plugin.LogDebug($"Could not sync game settings UI: {e.Message}");
        }
    }

    private static void CreateSliderRow(
        VisualElement container,
        string label,
        float min,
        float max,
        System.Func<float> getter,
        System.Action<float> setter
    )
    {
        var row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        var slider = UITools.CreateConfigurationSlider(min, max, getter(), 300);

        slider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            setter(evt.newValue);
        });
        slider.RegisterCallback<PointerUpEvent>(evt =>
        {
            ReskinProfileManager.SaveProfile();
        });

        row.Add(slider);
        container.Add(row);
    }

}
