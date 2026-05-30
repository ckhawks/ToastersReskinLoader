// VanillaUIRetheme — applies a darker background color (#262626 vs vanilla
// #3D3D3D) to UI Toolkit Toggle checkboxes, TextField inputs, DropdownField
// popups, and Slider tracks across every UIDocument the game opens.
//
// Also restyles the dropdown popover (the menu that appears on click) with a
// 2px white border and a faint divider between items — same treatment TRL
// uses for its own dropdowns in UITools.StylePopover.
//
// True runtime USS injection isn't possible from a plugin (StyleSheet is an
// Editor-built asset), so this hooks UIDocument.OnEnable, walks the root
// VisualElement for the relevant inner elements, and sets inline backgrounds.
// A GeometryChangedEvent listener on each root re-applies when new controls
// are added later (panels often populate lazily).
//
// Disable() explicitly writes the vanilla #3D3D3D color back rather than
// clearing the inline style — UI Toolkit sometimes keeps an inline-overridden
// state after StyleKeyword.Null, so an explicit revert is more reliable.
// The popover click handlers remain registered after Disable() but no-op
// because they check the `active` flag before running.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

public static class VanillaUIRetheme
{
    // 0x262626 → 38/255 ≈ 0.1490196
    private static readonly Color DarkBg = new Color(38f / 255f, 38f / 255f, 38f / 255f);
    // 0x3D3D3D → 61/255 ≈ 0.2392157 (vanilla default — explicit revert target)
    private static readonly Color VanillaBg = new Color(61f / 255f, 61f / 255f, 61f / 255f);

    // Inner-element selectors whose background we override. The "fill" lives
    // on these inner elements (not the outer wrapper) in UI Toolkit's theme.
    private static readonly string[] TargetClasses =
    {
        "unity-base-text-field__input",      // TextField, IntegerField, FloatField inputs
        "unity-toggle__input",               // Toggle (checkbox) box
        "unity-base-popup-field__input",     // DropdownField / EnumField / PopupField input
        "unity-base-slider__tracker",        // Slider bar background
    };

    private const string PopoverHookedClass = "trl-vanilla-popover-hooked";

    private static bool active;
    private static readonly HashSet<VisualElement> hookedRoots = new();

    public static void Enable()
    {
        if (active) return;
        active = true;

        foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            HookDocument(doc);

        Plugin.Log("[VanillaUIRetheme] Enabled");
    }

    public static void Disable()
    {
        if (!active) return;
        active = false;

        foreach (var root in hookedRoots)
        {
            if (root == null) continue;
            try
            {
                root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                Recolor(root, VanillaBg);
            }
            catch (Exception e) { Plugin.LogError($"[VanillaUIRetheme] Revert failed: {e}"); }
        }
        hookedRoots.Clear();

        Plugin.Log("[VanillaUIRetheme] Disabled");
    }

    [HarmonyPatch(typeof(UIDocument), "OnEnable")]
    public static class UIDocumentOnEnablePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIDocument __instance)
        {
            if (!active) return;
            HookDocument(__instance);
        }
    }

    private static void HookDocument(UIDocument doc)
    {
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null || !hookedRoots.Add(root)) return;

        root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        Recolor(root, DarkBg);
        HookPopoverFields(root);
    }

    private static void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (!active) return;
        if (evt.target is VisualElement ve)
        {
            Recolor(ve, DarkBg);
            HookPopoverFields(ve);
        }
    }

    // Public so other callers (e.g. controls we inject after the panel's geometry
    // change already fired) can re-apply the dark background on demand.
    public static void RecolorTree(VisualElement root)
    {
        if (root != null) Recolor(root, DarkBg);
    }

    private static void Recolor(VisualElement root, Color color)
    {
        var sc = new StyleColor(color);
        foreach (var cls in TargetClasses)
        {
            foreach (var ve in root.Query(className: cls).Build())
            {
                if (IsInsideChat(ve)) continue;
                ve.style.backgroundColor = sc;
            }
        }
    }

    // The in-game chat input is intentionally a light-gray transparent box —
    // skip any element whose ancestor is UIChat's "ChatView" root.
    private static bool IsInsideChat(VisualElement ve)
    {
        for (var cur = ve; cur != null; cur = cur.parent)
        {
            if (cur.name == "ChatView") return true;
        }
        return false;
    }

    // ── Popover (dropdown menu) styling ──────────────────────────────────────
    //
    // The popover is added to the panel's visual tree only after the user
    // clicks the popup field, so we can't style it up-front. Instead, hook a
    // MouseDownEvent on each popup field; on click, schedule a one-frame-later
    // pass that finds .unity-base-dropdown in the panel and restyles it.

    private static void HookPopoverFields(VisualElement root)
    {
        foreach (var field in root.Query(className: "unity-base-popup-field").Build())
        {
            if (field.ClassListContains(PopoverHookedClass)) continue;
            field.AddToClassList(PopoverHookedClass);

            var popupField = field; // capture for closure
            popupField.RegisterCallback<MouseDownEvent>(_ =>
            {
                if (!active) return;
                popupField.schedule.Execute(() =>
                {
                    if (!active) return;
                    StylePopover(popupField);
                }).ExecuteLater(2);
            });
        }
    }

    private static void StylePopover(VisualElement popupField)
    {
        var panelRoot = popupField.panel?.visualTree;
        if (panelRoot == null) return;

        var dropdown = panelRoot.Q(className: "unity-base-dropdown");
        if (dropdown == null) return;

        var containerInner = dropdown.Q(className: "unity-base-dropdown__container-inner");
        if (containerInner != null)
        {
            containerInner.style.backgroundColor = new Color(47f / 255f, 47f / 255f, 47f / 255f, 0.95f);
            containerInner.style.borderTopWidth = 2;
            containerInner.style.borderBottomWidth = 2;
            containerInner.style.borderLeftWidth = 2;
            containerInner.style.borderRightWidth = 2;
            containerInner.style.borderTopColor = Color.white;
            containerInner.style.borderBottomColor = Color.white;
            containerInner.style.borderLeftColor = Color.white;
            containerInner.style.borderRightColor = Color.white;
        }

        foreach (var item in dropdown.Query(className: "unity-base-dropdown__item").Build())
        {
            item.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.minHeight = 24;

            var capturedItem = item;
            capturedItem.RegisterCallback<MouseEnterEvent>(_ =>
            {
                capturedItem.style.backgroundColor = Color.white;
                var lbl = capturedItem.Q<Label>(className: "unity-base-dropdown__label");
                if (lbl != null) lbl.style.color = Color.black;
            });
            capturedItem.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                capturedItem.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var lbl = capturedItem.Q<Label>(className: "unity-base-dropdown__label");
                if (lbl != null) lbl.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            });

            var label = item.Q<Label>(className: "unity-base-dropdown__label");
            if (label != null)
            {
                label.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                label.style.fontSize = 14;
            }
        }
    }
}
