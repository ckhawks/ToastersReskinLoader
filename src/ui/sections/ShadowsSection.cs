using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class ShadowsSection
{
    private static readonly Dictionary<string, int> ResolutionOptions = new Dictionary<string, int>
    {
        { "256 (Very Low)", 256 },
        { "512 (Low)", 512 },
        { "1024 (Medium)", 1024 },
        { "2048 (High)", 2048 },
        { "4096 (Very High)", 4096 },
        { "8192 (Ultra)", 8192 },
    };

    private static readonly Dictionary<string, int> CascadeOptions = new Dictionary<string, int>
    {
        { "1 Cascade", 1 },
        { "2 Cascades", 2 },
        { "3 Cascades", 3 },
        { "4 Cascades", 4 },
    };

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        // Description
        Label description = UITools.CreateConfigurationLabel(
            "Crispy Shadows overrides the game's shadow map settings for sharper, higher-quality shadows.");
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(description);

        // Enable toggle
        VisualElement enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Crispy Shadows"));

        Toggle enableToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.crispyShadowsEnabled);
        enableToggle.value = ReskinProfileManager.currentProfile.crispyShadowsEnabled;

        // We'll collect all the controls that should be grayed out when disabled
        var dependentControls = new List<VisualElement>();

        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.crispyShadowsEnabled = evt.newValue;
            ReskinProfileManager.SaveProfile();
            CrispyShadowsSwapper.Apply();
            UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        contentScrollViewContent.Add(enableRow);

        // Shadow Resolution dropdown
        VisualElement resolutionRow = UITools.CreateConfigurationRow();
        resolutionRow.Add(UITools.CreateConfigurationLabel("Shadow Map Resolution"));

        var resolutionChoices = ResolutionOptions.Keys.ToList();
        string currentResolution = ResolutionOptions
            .FirstOrDefault(kv => kv.Value == ReskinProfileManager.currentProfile.shadowResolution).Key
            ?? "4096 (Very High)";

        PopupField<string> resolutionDropdown = UITools.CreateStringDropdownField(resolutionChoices, currentResolution);

        // VRAM label
        Label vramLabel = UITools.CreateConfigurationLabel(
            $"Estimated VRAM: {CrispyShadowsSwapper.EstimateVRAM(ReskinProfileManager.currentProfile.shadowResolution)}");
        vramLabel.style.marginTop = 2;
        vramLabel.style.marginBottom = 8;
        vramLabel.style.fontSize = 13;
        vramLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

        resolutionDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (ResolutionOptions.TryGetValue(evt.newValue, out int resolution))
            {
                ReskinProfileManager.currentProfile.shadowResolution = resolution;
                ReskinProfileManager.SaveProfile();
                CrispyShadowsSwapper.Apply();
                vramLabel.text = $"Estimated VRAM: {CrispyShadowsSwapper.EstimateVRAM(resolution)}";
            }
        });

        resolutionRow.Add(resolutionDropdown);
        contentScrollViewContent.Add(resolutionRow);
        dependentControls.Add(resolutionRow);

        // VRAM estimate
        contentScrollViewContent.Add(vramLabel);
        dependentControls.Add(vramLabel);

        // Shadow Distance slider
        VisualElement distanceRow = UITools.CreateConfigurationRow();
        distanceRow.Add(UITools.CreateConfigurationLabel("Shadow Distance"));
        Slider distanceSlider = UITools.CreateConfigurationSlider(10, 300,
            ReskinProfileManager.currentProfile.shadowDistance, 300);

        distanceSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ReskinProfileManager.currentProfile.shadowDistance = evt.newValue;
            CrispyShadowsSwapper.Apply();
        });
        distanceSlider.RegisterCallback<PointerUpEvent>(evt =>
        {
            ReskinProfileManager.SaveProfile();
        });

        distanceRow.Add(distanceSlider);
        contentScrollViewContent.Add(distanceRow);
        dependentControls.Add(distanceRow);

        // Shadow Cascades dropdown
        VisualElement cascadeRow = UITools.CreateConfigurationRow();
        cascadeRow.Add(UITools.CreateConfigurationLabel("Shadow Cascades"));

        var cascadeChoices = CascadeOptions.Keys.ToList();
        string currentCascade = CascadeOptions
            .FirstOrDefault(kv => kv.Value == ReskinProfileManager.currentProfile.shadowCascadeCount).Key
            ?? "4 Cascades";

        PopupField<string> cascadeDropdown = UITools.CreateStringDropdownField(cascadeChoices, currentCascade);
        cascadeDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (CascadeOptions.TryGetValue(evt.newValue, out int cascades))
            {
                ReskinProfileManager.currentProfile.shadowCascadeCount = cascades;
                ReskinProfileManager.SaveProfile();
                CrispyShadowsSwapper.Apply();
            }
        });

        cascadeRow.Add(cascadeDropdown);
        contentScrollViewContent.Add(cascadeRow);
        dependentControls.Add(cascadeRow);

        // Soft Shadows toggle
        VisualElement softRow = UITools.CreateConfigurationRow();
        softRow.Add(UITools.CreateConfigurationLabel("Soft Shadows"));

        Toggle softToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.shadowSoftShadows);
        softToggle.value = ReskinProfileManager.currentProfile.shadowSoftShadows;
        softToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.shadowSoftShadows = evt.newValue;
            ReskinProfileManager.SaveProfile();
            CrispyShadowsSwapper.Apply();
        });

        softRow.Add(softToggle);
        contentScrollViewContent.Add(softRow);
        dependentControls.Add(softRow);

        // Reset button
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
            ReskinProfileManager.ResetShadowsToDefault();

            Label title = (Label)contentScrollViewContent.Children().ToArray()[0];
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);

        // Set initial state of dependent controls
        UpdateDependentControlsState(dependentControls, ReskinProfileManager.currentProfile.crispyShadowsEnabled);
    }

    private static void UpdateDependentControlsState(List<VisualElement> controls, bool enabled)
    {
        foreach (var control in controls)
        {
            control.SetEnabled(enabled);
            control.style.opacity = enabled ? 1f : 0.5f;
        }
    }
}
