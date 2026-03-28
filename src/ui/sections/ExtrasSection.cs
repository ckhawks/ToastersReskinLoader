using System;
using System.Collections.Generic;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class ExtrasSection
{
    // Session-only state — not saved to ModSettings or profile
    private static readonly HashSet<string> RedeemedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool bigHeadsEnabled;

    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        // -- Code entry --
        Label codeLabel = new Label("<b>Enter Code</b>");
        codeLabel.style.fontSize = 16;
        codeLabel.style.color = Color.white;
        codeLabel.style.marginTop = 8;
        codeLabel.style.marginBottom = 4;
        contentScrollViewContent.Add(codeLabel);

        Label codeDescription = new Label("Enter a code to unlock hidden features for this session.");
        codeDescription.style.fontSize = 14;
        codeDescription.style.color = new Color(0.7f, 0.7f, 0.7f);
        codeDescription.style.whiteSpace = WhiteSpace.Normal;
        codeDescription.style.marginBottom = 8;
        contentScrollViewContent.Add(codeDescription);

        // Container for unlockable settings that appear after codes are entered
        VisualElement unlockedContainer = new VisualElement();
        unlockedContainer.style.marginTop = 12;

        VisualElement codeRow = UITools.CreateConfigurationRow();
        TextField codeField = new TextField();
        codeField.style.flexGrow = 1;
        codeField.style.marginRight = 8;
        codeRow.Add(codeField);

        Label feedbackLabel = new Label();
        feedbackLabel.style.fontSize = 14;
        feedbackLabel.style.marginTop = 8;
        feedbackLabel.style.marginBottom = 4;

        Button redeemButton = new Button { text = "Redeem" };
        redeemButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        redeemButton.style.paddingLeft = 12;
        redeemButton.style.paddingRight = 12;
        redeemButton.style.paddingTop = 6;
        redeemButton.style.paddingBottom = 6;
        UITools.AddHoverEffectsForButton(redeemButton);
        redeemButton.RegisterCallback<ClickEvent>(evt =>
        {
            string code = codeField.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(code))
                return;

            if (RedeemedCodes.Contains(code))
            {
                feedbackLabel.text = "Code already redeemed.";
                feedbackLabel.style.color = new Color(0.7f, 0.7f, 0.5f);
            }
            else if (TryRedeemCode(code, unlockedContainer))
            {
                feedbackLabel.text = "Code accepted!";
                feedbackLabel.style.color = new Color(0.4f, 0.9f, 0.4f);
                codeField.value = "";
            }
            else
            {
                feedbackLabel.text = "Invalid code.";
                feedbackLabel.style.color = new Color(0.9f, 0.4f, 0.4f);
            }
        });
        codeRow.Add(redeemButton);
        contentScrollViewContent.Add(codeRow);
        contentScrollViewContent.Add(feedbackLabel);
        contentScrollViewContent.Add(unlockedContainer);

        // Show settings for any codes already redeemed this session
        RebuildUnlockedSettings(unlockedContainer);
    }

    private static bool TryRedeemCode(string code, VisualElement unlockedContainer)
    {
        switch (code.ToUpperInvariant())
        {
            case "BIGHEADS":
                RedeemedCodes.Add(code);
                RebuildUnlockedSettings(unlockedContainer);
                return true;
            default:
                return false;
        }
    }

    private static void RebuildUnlockedSettings(VisualElement container)
    {
        container.Clear();

        if (RedeemedCodes.Contains("BIGHEADS"))
        {
            Label sectionLabel = new Label("<b>Big Heads</b>");
            sectionLabel.style.fontSize = 16;
            sectionLabel.style.color = Color.white;
            sectionLabel.style.marginTop = 8;
            sectionLabel.style.marginBottom = 4;
            container.Add(sectionLabel);

            VisualElement bigHeadRow = UITools.CreateConfigurationRow();
            bigHeadRow.Add(UITools.CreateConfigurationLabel("Enable Big Heads"));
            Toggle bigHeadToggle = UITools.CreateConfigurationCheckbox(bigHeadsEnabled);
            bigHeadToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                bigHeadsEnabled = evt.newValue;
                Plugin.modSettings.BigHeadsEnabled = evt.newValue;
                HatSwapper.ResetHeadScales();
            });
            bigHeadRow.Add(bigHeadToggle);
            container.Add(bigHeadRow);
        }
    }
}
