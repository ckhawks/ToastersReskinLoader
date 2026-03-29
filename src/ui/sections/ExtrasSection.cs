using System;
using System.Collections.Generic;
using ToasterReskinLoader.api;
using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class ExtrasSection
{
    // Session-only state — not saved to ModSettings or profile
    private static readonly HashSet<string> LocalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        Label codeDescription = new Label("Enter a code to unlock items or hidden features.");
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
        codeField.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        codeField.style.paddingLeft = 8;
        codeField.style.paddingRight = 8;
        codeField.style.paddingTop = 4;
        codeField.style.paddingBottom = 4;
        codeField.RegisterValueChangedCallback(evt =>
        {
            string upper = evt.newValue?.ToUpperInvariant() ?? "";
            if (upper != evt.newValue)
                codeField.SetValueWithoutNotify(upper);
        });
        Button redeemButton = new Button { text = "Redeem" };
        codeField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                using var click = ClickEvent.GetPooled();
                redeemButton.SendEvent(click);
            }
        });
        codeRow.Add(codeField);

        Label feedbackLabel = new Label();
        feedbackLabel.style.fontSize = 14;
        feedbackLabel.style.marginTop = 8;
        feedbackLabel.style.marginBottom = 4;
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

            // Check local-only codes first
            if (TryRedeemLocalCode(code, unlockedContainer, feedbackLabel))
            {
                codeField.value = "";
                return;
            }

            // Otherwise try the server API
            redeemButton.SetEnabled(false);
            feedbackLabel.text = "Redeeming...";
            feedbackLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

            AppearanceAPI.RedeemCode(code, (success, message) =>
            {
                redeemButton.SetEnabled(true);
                feedbackLabel.text = message;
                if (success)
                {
                    feedbackLabel.style.color = new Color(0.4f, 0.9f, 0.4f);
                    codeField.value = "";
                }
                else
                {
                    feedbackLabel.style.color = new Color(0.9f, 0.4f, 0.4f);
                }
            });
        });
        codeRow.Add(redeemButton);
        contentScrollViewContent.Add(codeRow);
        contentScrollViewContent.Add(feedbackLabel);
        contentScrollViewContent.Add(unlockedContainer);

        // Show settings for any local codes already redeemed this session
        RebuildUnlockedSettings(unlockedContainer);
    }

    private static bool TryRedeemLocalCode(string code, VisualElement unlockedContainer, Label feedbackLabel)
    {
        string upper = code.ToUpperInvariant();
        switch (upper)
        {
            case "BIGHEADS":
                if (LocalCodes.Contains(upper))
                {
                    feedbackLabel.text = "Code already redeemed.";
                    feedbackLabel.style.color = new Color(0.7f, 0.7f, 0.5f);
                }
                else
                {
                    LocalCodes.Add(upper);
                    RebuildUnlockedSettings(unlockedContainer);
                    feedbackLabel.text = "Code accepted!";
                    feedbackLabel.style.color = new Color(0.4f, 0.9f, 0.4f);
                }
                return true;
            case "UNLOCKALLHATS":
                foreach (var hat in HatSwapper.AllHats)
                    AppearanceAPI.UnlockedHatIds.Add(hat.Id);
                AppearanceAPI.NotifyUnlocksChanged();
                feedbackLabel.text = "All hats unlocked (debug, session only).";
                feedbackLabel.style.color = new Color(0.4f, 0.9f, 0.4f);
                return true;
            default:
                return false;
        }
    }

    private static void RebuildUnlockedSettings(VisualElement container)
    {
        container.Clear();

        if (LocalCodes.Contains("BIGHEADS"))
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
                AppearanceAPI.ReapplyAllAppearances();
            });
            bigHeadRow.Add(bigHeadToggle);
            container.Add(bigHeadRow);
        }
    }
}
