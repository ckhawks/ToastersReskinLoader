// UITools.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui;

public static class UITools
{
    public static VisualElement CreateRow()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        return row;
    }
    
    public static VisualElement CreateConfigurationRow()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginTop = 4;
        row.style.marginBottom = 4;
        return row;
    }

    public static PopupField<ReskinRegistry.ReskinEntry> CreateConfigurationDropdownField()
    {
        PopupField<ReskinRegistry.ReskinEntry> popupField = new PopupField<ReskinRegistry.ReskinEntry>();

        popupField.index = 0; // If you don't do this, there is no selected value, and the formatSelectedValueCallback DIES
        popupField.formatSelectedValueCallback = e => (e == null) ? "None" : e.Name;
        popupField.formatListItemCallback = e => e.Name;

        popupField.style.minWidth = 400;
        popupField.style.maxWidth = 400;
        popupField.style.width = 400;
        popupField.style.height = 60;
        popupField.style.minHeight = 30;
        popupField.style.maxHeight = 30;
        popupField.style.fontSize = 16;
        popupField.style.overflow = Overflow.Hidden;
        popupField.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        popupField.style.color = Color.white;
        popupField.style.paddingLeft = 10;
        StyleDropdownArrow(popupField);
        
        StylePopoverOnClick(popupField);
        
        return popupField;
    }

    public static PopupField<string> CreateStringDropdownField(List<string> choices, string defaultValue)
    {
        PopupField<string> popupField = new PopupField<string>(choices, defaultValue ?? choices[0]);

        popupField.style.minWidth = 400;
        popupField.style.maxWidth = 400;
        popupField.style.width = 400;
        popupField.style.height = 60;
        popupField.style.minHeight = 30;
        popupField.style.maxHeight = 30;
        popupField.style.fontSize = 16;
        popupField.style.overflow = Overflow.Hidden;
        popupField.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        popupField.style.color = Color.white;
        popupField.style.paddingLeft = 10;
        StyleDropdownArrowGeneric(popupField);
        StylePopoverOnClick(popupField);

        return popupField;
    }

    /// <summary>
    /// Hooks into a popup field's click to style the popover dropdown that appears.
    /// Works for any VisualElement-based popup field.
    /// </summary>
    public static void StylePopoverOnClick(VisualElement popupField)
    {
        popupField.RegisterCallback<MouseDownEvent>(evt =>
        {
            popupField.schedule.Execute(() => StylePopover(popupField)).ExecuteLater(2);
        });
    }

    private static void StylePopover(VisualElement popupField)
    {
        var root = popupField.panel?.visualTree;
        if (root == null) return;

        var dropdown = root.Q(className: "unity-base-dropdown");
        if (dropdown == null) return;

        var containerInner = dropdown.Q(className: "unity-base-dropdown__container-inner");
        if (containerInner != null)
        {
            containerInner.style.backgroundColor = new Color(47/255f, 47/255f, 47/255f, 0.9f);
            containerInner.style.borderTopWidth = 2;
            containerInner.style.borderBottomWidth = 2;
            containerInner.style.borderLeftWidth = 2;
            containerInner.style.borderRightWidth = 2;
            containerInner.style.borderTopColor = Color.white;
            containerInner.style.borderBottomColor = Color.white;
            containerInner.style.borderLeftColor = Color.white;
            containerInner.style.borderRightColor = Color.white;
        }

        var items = dropdown.Query(className: "unity-base-dropdown__item").ToList();
        foreach (var item in items)
        {
            item.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.justifyContent = Justify.Center;
            item.style.minHeight = 24;

            item.RegisterCallback<MouseEnterEvent>(evt2 =>
            {
                item.style.backgroundColor = Color.white;
                var label2 = item.Q<Label>(className: "unity-base-dropdown__label");
                if (label2 != null) label2.style.color = Color.black;
            });
            item.RegisterCallback<MouseLeaveEvent>(evt2 =>
            {
                item.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var label2 = item.Q<Label>(className: "unity-base-dropdown__label");
                if (label2 != null) label2.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            });

            var label = item.Q<Label>(className: "unity-base-dropdown__label");
            if (label != null)
            {
                label.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                label.style.fontSize = 14;
            }
        }
    }

    public static Label CreateConfigurationLabel(string text)
    {
        Label label = new Label();
        label.text = text;
        label.style.fontSize = 16;
        label.style.color = Color.white;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    public static Toggle CreateConfigurationCheckbox(bool defaultValue)
    {
        Toggle toggle = new Toggle();
        toggle.value = defaultValue;

        toggle.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            // Style the outer frame/border of the toggle, not the checkmark itself
            var input = toggle.Q(className: "unity-toggle__input");
            if (input != null)
            {
                input.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                input.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                input.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                input.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                input.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            }
        });

        return toggle;
    }

    public static Slider CreateConfigurationSlider(float lowValue, float highValue, float value, float width)
    {
        Slider slider = new Slider();
        slider.lowValue = lowValue;
        slider.highValue = highValue;
        slider.value = value;
        slider.direction = SliderDirection.Horizontal;
        slider.showInputField = true;
        slider.style.width = width;
        slider.style.minWidth = width;
        slider.style.maxWidth = width;
        slider.style.fontSize = 14;
        slider.style.flexShrink = 1;
        slider.style.overflow = Overflow.Hidden;

        // Style the slider track with a background
        slider.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var dragger = slider.Q(className: "unity-base-slider__dragger-border");
            if (dragger != null)
            {
                dragger.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            }
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            }
            var inputField = slider.Q(className: "unity-base-text-field__input");
            if (inputField != null)
            {
                inputField.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                inputField.style.color = Color.white;
            }
        });

        return slider;
    }

    public static void StyleDropdownField(DropdownField dropdown)
    {
        dropdown.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        dropdown.style.color = Color.white;
        dropdown.style.paddingLeft = 10;
        StyleDropdownArrowGeneric(dropdown);
    }

    public static void StyleDropdownArrow(BasePopupField<ReskinRegistry.ReskinEntry, ReskinRegistry.ReskinEntry> dropdown)
    {
        StyleDropdownArrowGeneric(dropdown);
    }

    public static void StyleDropdownArrowGeneric(VisualElement dropdown)
    {
        dropdown.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var input = dropdown.Q(className: "unity-base-popup-field__input");
            if (input == null) return;

            input.style.flexDirection = FlexDirection.Row;
            input.style.justifyContent = Justify.SpaceBetween;
            input.style.alignItems = Align.Center;

            Label arrow = new Label("▼");
            arrow.style.color = Color.white;
            arrow.style.fontSize = 18;
            arrow.style.marginLeft = 4;
            arrow.style.marginRight = 8;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.style.paddingBottom = 0;
            arrow.style.paddingTop = 0;
            arrow.pickingMode = PickingMode.Ignore;
            input.Add(arrow);
        });
    }

    public static void AddHoverEffectsForButton(Button button)
    {
        button.RegisterCallback<MouseEnterEvent>(new EventCallback<MouseEnterEvent>((evt) =>
        {
            button.style.backgroundColor = Color.white;
            button.style.color = Color.black;
        }));
        button.RegisterCallback<MouseLeaveEvent>(new EventCallback<MouseLeaveEvent>((evt) =>
        {
            button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            button.style.color = Color.white;
        }));
    }
    
    /// <summary>
    /// Creates a full configuration section for editing a Color value.
    /// Includes a label, a color preview box, and sliders for R, G, B, and optionally A.
    /// </summary>
    /// <param name="label">The text for the main label of the section.</param>
    /// <param name="initialColor">The starting color to display.</param>
    /// <param name="includeAlpha">If true, an 'A' (alpha) slider will be included.</param>
    /// <param name="onValueChanged">Callback that fires continuously as the color changes. Good for live previews.</param>
    /// <param name="onSave">Callback that fires when the user releases the mouse on any slider. Good for saving the final value.</param>
    /// <returns>A VisualElement containing the entire color configuration UI.</returns>
    public static VisualElement CreateColorConfigurationRow(
        string label,
        Color initialColor,
        bool includeAlpha,
        Action<Color> onValueChanged,
        Action onSave
    )
    {
        // This will hold the current color state as the user interacts with the sliders
        var currentColor = initialColor;

        // 1. Main container for the whole component
        var mainContainer = new VisualElement();
        mainContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.style.marginBottom = 10;

        // 2. Top row for the main label and the color preview
        var topRow = CreateConfigurationRow();
        topRow.Add(CreateConfigurationLabel(label));

        var colorPreview = new VisualElement
        {
            style =
            {
                width = 300,
                height = 30,
                backgroundColor = initialColor,
            },
        };
        topRow.Add(colorPreview);
        mainContainer.Add(topRow);

        // 3. Container for the individual R, G, B, A sliders
        var slidersContainer = new VisualElement();
        slidersContainer.style.marginLeft = 20; // Indent sliders slightly
        mainContainer.Add(slidersContainer);

        // 4. Local helper function to avoid repeating slider creation code
        void CreateAndRegisterSliderRow(
            string componentLabel,
            float initialComponentValue,
            Func<float, float> updateComponentAction
        )
        {
            var row = CreateConfigurationRow();
            row.Add(CreateConfigurationLabel(componentLabel));
            var slider = CreateConfigurationSlider(
                0,
                1,
                initialComponentValue,
                300
            );
            

            // Register callback for continuous changes (live preview)
            slider.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                // Update the specific color component (r, g, b, or a)
                updateComponentAction(evt.newValue);
                // Update the preview box color
                colorPreview.style.backgroundColor = currentColor;
                // Fire the external callback for live updates
                onValueChanged?.Invoke(currentColor);
                onSave?.Invoke();
            });

            // Register callback for when the user is done dragging (save)
            slider.RegisterCallback<PointerUpEvent>(evt =>
            {
                onSave?.Invoke();
            });

            row.Add(slider);
            slidersContainer.Add(row);
        }

        // 5. Create the sliders using the local helper
        CreateAndRegisterSliderRow(
            "Red",
            initialColor.r,
            (val) => currentColor.r = val
        );
        CreateAndRegisterSliderRow(
            "Green",
            initialColor.g,
            (val) => currentColor.g = val
        );
        CreateAndRegisterSliderRow(
            "Blue",
            initialColor.b,
            (val) => currentColor.b = val
        );

        if (includeAlpha)
        {
            CreateAndRegisterSliderRow(
                "Alpha",
                initialColor.a,
                (val) => currentColor.a = val
            );
        }

        return mainContainer;
    }
}