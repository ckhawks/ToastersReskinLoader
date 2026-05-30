using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.ui.sections;

/// <summary>
/// "Puck Indicator" section — NHL EA-style on-screen arrows that point toward the puck
/// when it is off-screen to the sides / top / bottom of the camera view.
/// </summary>
public static class PuckIndicatorSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        var profile = ReskinProfileManager.currentProfile;

        // --- Section header ---
        Label header = UITools.CreateConfigurationLabel("<b>Puck Indicator</b>");
        header.style.marginTop = 10;
        header.style.marginBottom = 4;
        contentScrollViewContent.Add(header);

        // --- Enabled toggle ---
        var enabledRow = UITools.CreateConfigurationRow();
        enabledRow.Add(UITools.CreateConfigurationLabel("Enabled"));
        var enabledToggle = UITools.CreateConfigurationCheckbox(profile.puckIndicatorEnabled);
        enabledToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            profile.puckIndicatorEnabled = evt.newValue;
            ReskinProfileManager.SaveProfile();
            PuckIndicatorSwapper.ApplyAll();
        });
        enabledRow.Add(enabledToggle);
        contentScrollViewContent.Add(enabledRow);

        // --- Arrow Color ---
        var colorRow = UITools.CreateColorConfigurationRow(
            "Arrow Color",
            (Color)profile.puckIndicatorArrowColor,
            false,
            newColor =>
            {
                profile.puckIndicatorArrowColor = newColor;
                PuckIndicatorSwapper.ApplyAll();
            },
            () => ReskinProfileManager.SaveProfile()
        );
        contentScrollViewContent.Add(colorRow);

        // --- Arrow Size ---
        var sizeRow = UITools.CreateConfigurationRow();
        sizeRow.Add(UITools.CreateConfigurationLabel("Arrow Size"));
        var sizeSlider = UITools.CreateConfigurationSlider(10f, 60f, (float)profile.puckIndicatorArrowSize, 300f);
        sizeSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            profile.puckIndicatorArrowSize = evt.newValue;
            ReskinProfileManager.SaveProfile();
            PuckIndicatorSwapper.ApplyAll();
        });
        sizeRow.Add(sizeSlider);
        contentScrollViewContent.Add(sizeRow);

        // --- Edge Margin ---
        var marginRow = UITools.CreateConfigurationRow();
        marginRow.Add(UITools.CreateConfigurationLabel("Edge Margin"));
        var marginSlider = UITools.CreateConfigurationSlider(5f, 100f, (float)profile.puckIndicatorEdgeMargin, 300f);
        marginSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            profile.puckIndicatorEdgeMargin = evt.newValue;
            ReskinProfileManager.SaveProfile();
            PuckIndicatorSwapper.ApplyAll();
        });
        marginRow.Add(marginSlider);
        contentScrollViewContent.Add(marginRow);

        // --- Opacity ---
        var opacityRow = UITools.CreateConfigurationRow();
        opacityRow.Add(UITools.CreateConfigurationLabel("Opacity"));
        var opacitySlider = UITools.CreateConfigurationSlider(0.1f, 1f, (float)profile.puckIndicatorOpacity, 300f);
        opacitySlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            profile.puckIndicatorOpacity = evt.newValue;
            ReskinProfileManager.SaveProfile();
            PuckIndicatorSwapper.ApplyAll();
        });
        opacityRow.Add(opacitySlider);
        contentScrollViewContent.Add(opacityRow);

        // --- Show Elevation ---
        var elevationRow = UITools.CreateConfigurationRow();
        elevationRow.Add(UITools.CreateConfigurationLabel("Show Elevation"));
        var elevationToggle = UITools.CreateConfigurationCheckbox(profile.puckIndicatorShowElevation);
        elevationToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            profile.puckIndicatorShowElevation = evt.newValue;
            ReskinProfileManager.SaveProfile();
            PuckIndicatorSwapper.ApplyAll();
        });
        elevationRow.Add(elevationToggle);
        contentScrollViewContent.Add(elevationRow);

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
            ReskinProfileManager.ResetPuckIndicatorToDefault();
            Label title = (Label)contentScrollViewContent.Children().ToArray()[0];
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);

        // Bottom spacer
        Label spacer = new Label(" ");
        spacer.style.height = 30;
        contentScrollViewContent.Add(spacer);
    }
}
