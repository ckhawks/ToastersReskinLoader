using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class GlossSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = UITools.CreateConfigurationLabel(
            "Adjusts the glossy shine on sticks, players, and pucks. " +
            "Move the slider to 0 to make surfaces fully matte (no reflections), or to 1 to keep the original gloss.");
        description.style.marginBottom = 12;
        description.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(description);

        var dependentControls = new List<VisualElement>();

        // Enable toggle
        VisualElement enableRow = UITools.CreateConfigurationRow();
        enableRow.Add(UITools.CreateConfigurationLabel("Enable Glossiness Control"));

        Toggle enableToggle = UITools.CreateConfigurationCheckbox(ReskinProfileManager.currentProfile.glossRemoverEnabled);
        enableToggle.value = ReskinProfileManager.currentProfile.glossRemoverEnabled;
        enableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            ReskinProfileManager.currentProfile.glossRemoverEnabled = evt.newValue;
            ReskinProfileManager.SaveProfile();
            if (evt.newValue) GlossSwapper.Scan();
            else GlossSwapper.RestoreAll();
            UITools.UpdateDependentControlsState(dependentControls, evt.newValue);
        });
        enableRow.Add(enableToggle);
        contentScrollViewContent.Add(enableRow);

        // Smoothness slider (0 = matte, 1 = original)
        VisualElement smoothRow = UITools.CreateConfigurationRow();
        smoothRow.Add(UITools.CreateConfigurationLabel("Gloss"));
        Slider smoothSlider = UITools.CreateConfigurationSlider(0f, 1f,
            ReskinProfileManager.currentProfile.glossSmoothness, 300);
        smoothSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ReskinProfileManager.currentProfile.glossSmoothness = evt.newValue;
            GlossSwapper.ReapplyAll();
        });
        smoothSlider.RegisterCallback<PointerUpEvent>(evt => ReskinProfileManager.SaveProfile());
        smoothRow.Add(smoothSlider);
        contentScrollViewContent.Add(smoothRow);
        dependentControls.Add(smoothRow);

        // Preset row
        VisualElement presetRow = new VisualElement();
        presetRow.style.flexDirection = FlexDirection.Row;
        presetRow.style.marginTop = 8;
        presetRow.style.marginBottom = 4;
        contentScrollViewContent.Add(presetRow);
        AddPreset(presetRow, contentScrollViewContent, "Matte", 0f);
        AddPreset(presetRow, contentScrollViewContent, "Low", 0.15f);
        AddPreset(presetRow, contentScrollViewContent, "Medium", 0.4f);
        AddPreset(presetRow, contentScrollViewContent, "Original", 0.5f);
        dependentControls.Add(presetRow);

        // ── Apply To ────────────────────────────────────────────────
        VisualElement separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
        separator.style.marginTop = 16;
        separator.style.marginBottom = 16;
        contentScrollViewContent.Add(separator);
        dependentControls.Add(separator);

        Label applyHeader = new Label("<b>Apply To</b>");
        applyHeader.style.fontSize = 20;
        applyHeader.style.color = Color.white;
        applyHeader.style.marginBottom = 8;
        contentScrollViewContent.Add(applyHeader);
        dependentControls.Add(applyHeader);

        AddCategoryToggle(contentScrollViewContent, dependentControls,
            "Sticks",
            () => ReskinProfileManager.currentProfile.glossAffectSticks,
            v => ReskinProfileManager.currentProfile.glossAffectSticks = v);
        AddCategoryToggle(contentScrollViewContent, dependentControls,
            "Players (body, helmet, jersey, etc.)",
            () => ReskinProfileManager.currentProfile.glossAffectPlayers,
            v => ReskinProfileManager.currentProfile.glossAffectPlayers = v);
        AddCategoryToggle(contentScrollViewContent, dependentControls,
            "Pucks",
            () => ReskinProfileManager.currentProfile.glossAffectPucks,
            v => ReskinProfileManager.currentProfile.glossAffectPucks = v);

        // Reset button
        Button resetButton = new Button
        {
            text = "Reset to default",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                fontSize = 18,
                marginTop = 16,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(resetButton);
        resetButton.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinProfileManager.ResetGlossRemoverToDefault();
            GlossSwapper.ReapplyAll();

            Label title = (Label)contentScrollViewContent.Children().First();
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);

        UITools.UpdateDependentControlsState(dependentControls, ReskinProfileManager.currentProfile.glossRemoverEnabled);
    }

    private static void AddCategoryToggle(VisualElement container, List<VisualElement> dependents,
        string label, System.Func<bool> getter, System.Action<bool> setter)
    {
        VisualElement row = UITools.CreateConfigurationRow();
        row.Add(UITools.CreateConfigurationLabel(label));
        Toggle toggle = UITools.CreateConfigurationCheckbox(getter());
        toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            setter(evt.newValue);
            ReskinProfileManager.SaveProfile();
            GlossSwapper.ReapplyAll();
        });
        row.Add(toggle);
        container.Add(row);
        dependents.Add(row);
    }

    private static void AddPreset(VisualElement parent, VisualElement contentRoot, string name, float value)
    {
        Button btn = new Button { text = name };
        btn.style.flexGrow = 1;
        btn.style.height = 28;
        btn.style.marginRight = 4;
        btn.style.paddingLeft = 0;
        btn.style.paddingRight = 0;
        btn.style.paddingTop = 0;
        btn.style.paddingBottom = 0;
        btn.style.fontSize = 13;
        btn.style.unityTextAlign = TextAnchor.MiddleCenter;
        btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        btn.style.color = Color.white;
        UITools.AddHoverEffectsForButton(btn);
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            ReskinProfileManager.currentProfile.glossSmoothness = value;
            ReskinProfileManager.SaveProfile();
            GlossSwapper.ReapplyAll();

            Label title = (Label)contentRoot.Children().First();
            contentRoot.Clear();
            contentRoot.Add(title);
            CreateSection(contentRoot);
        });
        parent.Add(btn);
    }
}
