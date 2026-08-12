// SkyboxSection.cs

using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class SkyboxSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        CreateSliderRow(
            contentScrollViewContent,
            "Atmosphere Thickness",
            0,
            10,
            () => ReskinProfileManager.currentProfile.skyboxAtmosphereThickness,
            val =>
                ReskinProfileManager.currentProfile.skyboxAtmosphereThickness =
                    val
        );
        CreateSliderRow(
            contentScrollViewContent,
            "Exposure",
            0,
            10,
            () => ReskinProfileManager.currentProfile.skyboxExposure,
            val => ReskinProfileManager.currentProfile.skyboxExposure = val
        );
        
        CreateSliderRow(
            contentScrollViewContent,
            "Sun Size",
            0,
            2,
            () => ReskinProfileManager.currentProfile.skyboxSunSize,
            val => ReskinProfileManager.currentProfile.skyboxSunSize = val
        );
        
        
        
        var groundColorSection = UITools.CreateColorConfigurationRow(
            "<b>Ground Color</b>",
            ReskinProfileManager.currentProfile.skyboxGroundColor,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.skyboxGroundColor =
                    newColor;
                SkyboxSwapper.UpdateSkybox();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(groundColorSection);

        var skyTintSection = UITools.CreateColorConfigurationRow(
            "<b>Sky Tint</b>",
            ReskinProfileManager.currentProfile.skyboxSkyTint,
            false,
            newColor =>
            {
                ReskinProfileManager.currentProfile.skyboxSkyTint = newColor;
                SkyboxSwapper.UpdateSkybox();
            },
            () => { ReskinProfileManager.SaveProfile(); }
        );
        contentScrollViewContent.Add(skyTintSection);
        
        Label notice = UITools.CreateConfigurationLabel("<b>These values don't seem to do much:</b>");
        contentScrollViewContent.Add(notice);
        
        CreateSliderRow(
            contentScrollViewContent,
            "Sun Disk",
            0,
            10,
            () => ReskinProfileManager.currentProfile.skyboxSunDisk,
            val => ReskinProfileManager.currentProfile.skyboxSunDisk = val
        );
        
        CreateSliderRow(
            contentScrollViewContent,
            "Sun Size Convergence",
            0,
            10,
            () => ReskinProfileManager.currentProfile.skyboxSunSizeConvergence,
            val => ReskinProfileManager.currentProfile.skyboxSunSizeConvergence = val
        );
        
        Button resetButton = new Button
        {
            text = "Reset to default",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                // width = new StyleLength(new Length(200, LengthUnit.Pixel)),
                // minWidth = new StyleLength(new Length(200, LengthUnit.Pixel)),
                // maxWidth = new StyleLength(new Length(200, LengthUnit.Pixel)),
                // width = referenceButton.style.width,
                // minWidth = referenceButton.style.minWidth,
                // maxWidth = referenceButton.style.maxWidth,
                // height = 60,
                // minHeight = 60,
                // maxHeight = 60,
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
            // 1. Call the NEW method to reset ONLY the skybox data.
            ReskinProfileManager.ResetSkyboxToDefault();

            // 2. Clear the existing UI for this section.
            Label title = (Label) contentScrollViewContent.Children().ToArray()[0];
            contentScrollViewContent.Clear();
            contentScrollViewContent.Add(title);

            // 3. Re-create the section. It will now be built using the
            //    freshly reset default values from the profile.
            CreateSection(contentScrollViewContent);
        });
        contentScrollViewContent.Add(resetButton);
    }
    
    // Helper function to reduce repetition for the simple sliders
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
            SkyboxSwapper.UpdateSkybox();
        });
        UITools.RegisterSliderSave(slider, () => ReskinProfileManager.SaveProfile());

        row.Add(slider);
        container.Add(row);
    }
}