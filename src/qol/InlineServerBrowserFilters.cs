// Server browser: inline filters at the bottom of the panel.
//
// Vanilla shows a separate full-screen Filters popup behind a [FILTERS]
// button. Reparenting that popup as-is keeps the screen-overlay USS so it
// ends up positioned off-screen. Instead, we harvest its child controls
// (search field, max-ping field, toggles) into a fresh compact strip
// appended at the bottom of the server browser. The original popup is
// hidden entirely. Controls retain their wired-up callbacks because we
// only reparent the elements themselves.
//
// Toggle-off behavior: UndoInlineFiltersForCurrent walks back every
// modification — re-parents controls, restores hidden labels and
// `label` text, clears inline overrides, drops the strip, and re-shows
// the vanilla [FILTERS] button.

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class InlineServerBrowserFilters
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableInlineServerBrowserFilters ?? true;

    private static UIServerBrowser _inlinedFor;
    // Tracks (child, originalParent) for every control we yanked out of the
    // base-game filters popup so we can put them back on toggle-off.
    private static readonly List<(VisualElement child, VisualElement originalParent)> _movedControls = new();
    // BuildCell hides each control's internal label. Track them so undo
    // can un-hide.
    private static readonly List<VisualElement> _hiddenInnerLabels = new();
    // StyleInputField / StyleToggleBox apply inline style overrides that
    // would persist after re-parenting back to the popup. Track every
    // styled element so undo can clear the inline overrides and let the
    // base-game USS reassert.
    private static readonly List<VisualElement> _styledOverlays = new();
    // Hover callbacks attached to the moved REFRESH / NEW SERVER buttons.
    // Without tracking, every InlineFilters cycle would add a new pair of
    // handlers — N toggles in a session = N ticks per hover.
    private static readonly Dictionary<VisualElement,
        (EventCallback<MouseEnterEvent> enter, EventCallback<MouseLeaveEvent> leave)> _buttonHoverCallbacks = new();
    // BuildCell clears each control's `label` text property to suppress the
    // inner label that would otherwise read "Search Text Field" / "Show
    // Full Toggle" / etc. Capture the original text so undo can restore it.
    private static readonly List<(VisualElement ctrl, string originalLabel)> _originalLabels = new();
    private static VisualElement _hiddenFiltersButton;
    private static VisualElement _hiddenFiltersPopup;

    // Match the server-list row style: dark uniform row, bold uppercase
    // label on the left, control right-aligned, comfortable padding.
    private static readonly Color BrowserRowBg    = new Color(61f / 255f, 61f / 255f, 61f / 255f, 1f);
    private static readonly Color BrowserRowHover = new Color(80f / 255f, 80f / 255f, 80f / 255f, 1f);
    // Shared dark fill for both the textfield input and the toggle's outer
    // box — the user wanted the two controls to read as one family. Sits
    // inside the row a notch darker than BrowserRowBg.
    internal static readonly Color BrowserInputBg = new Color(0.20f, 0.20f, 0.20f, 1f);
    // Border color for toggles, matching UITools.CreateConfigurationCheckbox.
    private static readonly Color ToasterToggleBorder = new Color(0.40f, 0.40f, 0.40f, 1f);

    [HarmonyPatch(typeof(UIServerBrowser), "Show")]
    private static class ServerBrowser_Show_InlineFilters
    {
        private static void Postfix(UIServerBrowser __instance)
        {
            if (!Enabled) return;
            InlineFilters(__instance);
        }
    }

    // Keep ShowFilters/HideFilters from re-displaying the now-hidden popup
    // while the inline-filters feature is on; otherwise let the vanilla
    // popup work as it always did.
    [HarmonyPatch(typeof(UIServerBrowser), "ShowFilters")]
    private static class ServerBrowser_ShowFilters_NoOp
    {
        private static bool Prefix() => !Enabled;
    }
    [HarmonyPatch(typeof(UIServerBrowser), "HideFilters")]
    private static class ServerBrowser_HideFilters_NoOp
    {
        private static bool Prefix() => !Enabled;
    }

    private static void TrackMove(VisualElement child)
    {
        if (child?.parent != null) _movedControls.Add((child, child.parent));
    }

    // Reset every inline style InlineFilters / StyleInputField /
    // StyleToggleBox / BuildCell touched, so the control falls back to its
    // base-game USS look once it's re-parented to the original popup.
    private static void ClearInlineOverrides(VisualElement ve)
    {
        if (ve == null) return;
        try
        {
            ve.style.width = StyleKeyword.Null;
            ve.style.height = StyleKeyword.Null;
            ve.style.minWidth = StyleKeyword.Null;
            ve.style.minHeight = StyleKeyword.Null;
            ve.style.flexGrow = StyleKeyword.Null;
            ve.style.flexShrink = StyleKeyword.Null;
            ve.style.flexBasis = StyleKeyword.Null;
            ve.style.backgroundColor = StyleKeyword.Null;
            ve.style.color = StyleKeyword.Null;
            ve.style.unityTextAlign = StyleKeyword.Null;
            ve.style.fontSize = StyleKeyword.Null;
            ve.style.unityFontStyleAndWeight = StyleKeyword.Null;
            ve.style.marginLeft = StyleKeyword.Null;
            ve.style.marginRight = StyleKeyword.Null;
            ve.style.marginTop = StyleKeyword.Null;
            ve.style.marginBottom = StyleKeyword.Null;
            ve.style.paddingLeft = StyleKeyword.Null;
            ve.style.paddingRight = StyleKeyword.Null;
            ve.style.paddingTop = StyleKeyword.Null;
            ve.style.paddingBottom = StyleKeyword.Null;
            ve.style.borderTopWidth = StyleKeyword.Null;
            ve.style.borderBottomWidth = StyleKeyword.Null;
            ve.style.borderLeftWidth = StyleKeyword.Null;
            ve.style.borderRightWidth = StyleKeyword.Null;
            ve.style.borderTopColor = StyleKeyword.Null;
            ve.style.borderBottomColor = StyleKeyword.Null;
            ve.style.borderLeftColor = StyleKeyword.Null;
            ve.style.borderRightColor = StyleKeyword.Null;
            ve.style.unityBackgroundImageTintColor = StyleKeyword.Null;
        }
        catch { }
    }

    // Reverses the changes InlineFilters made: parent each moved control
    // back to its original wrapper, un-hide its built-in label, drop our
    // inline overrides, drop the strip, and re-show the vanilla filters
    // button. Called from the QoL section when the toggle is turned off.
    public static void UndoInlineFiltersForCurrent()
    {
        var browser = _inlinedFor;
        _inlinedFor = null;
        if (browser == null) return;
        try
        {
            var serverBrowser = AccessTools.Field(typeof(UIServerBrowser), "serverBrowser")?.GetValue(browser) as VisualElement;

            // 0) Unregister the button hover callbacks so the vanilla
            //    [REFRESH] / [NEW SERVER] buttons don't keep our handlers
            //    after they're put back in the original popup.
            foreach (var kv in _buttonHoverCallbacks)
            {
                if (kv.Key == null) continue;
                try { kv.Key.UnregisterCallback(kv.Value.enter); } catch { }
                try { kv.Key.UnregisterCallback(kv.Value.leave); } catch { }
            }
            _buttonHoverCallbacks.Clear();

            // 1) Restore label text BuildCell blanked + un-hide the label
            //    child element BuildCell collapsed.
            foreach (var (ctrl, originalLabel) in _originalLabels)
            {
                if (ctrl == null) continue;
                try
                {
                    var labelProp = ctrl.GetType().GetProperty("label",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    labelProp?.SetValue(ctrl, originalLabel ?? "");
                }
                catch { }
            }
            _originalLabels.Clear();

            foreach (var lbl in _hiddenInnerLabels)
            {
                if (lbl == null) continue;
                try { lbl.style.display = DisplayStyle.Flex; } catch { }
            }
            _hiddenInnerLabels.Clear();

            // 2) Clear inline style overrides we applied + on each moved
            //    control's descendants so USS can fully take over again.
            foreach (var v in _styledOverlays)
            {
                ClearInlineOverrides(v);
                if (v != null)
                {
                    try
                    {
                        var input = v.Q(className: "unity-base-text-field__input")
                                 ?? v.Q(className: "unity-text-field__input")
                                 ?? v.Q(className: "unity-base-field__input");
                        ClearInlineOverrides(input);
                        var checkmark = v.Q(className: "unity-toggle__checkmark");
                        ClearInlineOverrides(checkmark);
                        var toggleInput = v.Q(className: "unity-toggle__input");
                        ClearInlineOverrides(toggleInput);
                    }
                    catch { }
                }
            }
            _styledOverlays.Clear();

            // 3) Reparent every yanked control back to where we found it.
            foreach (var (child, parent) in _movedControls)
            {
                if (child == null || parent == null) continue;
                try
                {
                    child.RemoveFromHierarchy();
                    parent.Add(child);
                }
                catch { }
            }
            _movedControls.Clear();

            // 4) Drop our inline strip and re-show the vanilla [FILTERS]
            //    button so the user can re-open the popup.
            if (serverBrowser != null)
            {
                var strip = serverBrowser.Q<VisualElement>("PPKB_InlineFilters");
                strip?.RemoveFromHierarchy();
            }

            if (_hiddenFiltersButton != null) _hiddenFiltersButton.style.display = StyleKeyword.Null;
            _hiddenFiltersButton = null;

            // 5) Reset the popup's display override so clicking [FILTERS]
            //    works again. The base-game ShowFilters/HideFilters patches
            //    handle the actual visibility toggling.
            if (_hiddenFiltersPopup != null) _hiddenFiltersPopup.style.display = StyleKeyword.Null;
            _hiddenFiltersPopup = null;
        }
        catch (Exception e) { Debug.LogWarning("[QoL] UndoInlineFiltersForCurrent failed: " + e.Message); }
    }

    // Triggers the inline-filter logic against whichever UIServerBrowser is
    // currently open. Used when the user toggles the feature back on
    // mid-session.
    public static void ReapplyInlineFiltersForCurrent()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui?.ServerBrowser == null) return;
            if (!ui.ServerBrowser.IsVisible) return;
            _inlinedFor = null; // force re-run on the same instance
            InlineFilters(ui.ServerBrowser);
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ReapplyInlineFiltersForCurrent failed: " + e.Message); }
    }

    // Plays the base-game UI tick sound when the user hovers the row and
    // brightens the row background. The leave handler restores it. The
    // _lastTickedRow throttle keeps the tick from re-firing when the
    // mouse jiggles inside the same row's children.
    private static VisualElement _lastTickedRow;
    private static void AddRowHoverEffects(VisualElement row)
    {
        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            row.style.backgroundColor = new StyleColor(BrowserRowHover);
            if (_lastTickedRow != row)
            {
                _lastTickedRow = row;
                try { MonoBehaviourSingleton<UIManager>.Instance?.PlaySelectSound(); } catch { }
            }
        });
        row.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            row.style.backgroundColor = new StyleColor(BrowserRowBg);
            if (_lastTickedRow == row) _lastTickedRow = null;
        });
    }

    // The base-game popup applies a dark input background via a USS
    // selector scoped to the popup's ancestor; reparenting the field out of
    // that popup drops the match and the input renders transparent. We
    // re-apply the background, a sensible height, right-alignment +
    // overflow clipping so long values don't bleed out of the field.
    private static void StyleInputField(VisualElement field, float width)
    {
        _styledOverlays.Add(field);
        field.style.width = width;
        field.style.height = 32;
        field.style.overflow = Overflow.Hidden;
        var input = field.Q(className: "unity-base-text-field__input")
                 ?? field.Q(className: "unity-text-field__input")
                 ?? field.Q(className: "unity-base-field__input");
        if (input != null)
        {
            input.style.backgroundColor = new StyleColor(BrowserInputBg);
            input.style.unityTextAlign = TextAnchor.MiddleRight;
            input.style.overflow = Overflow.Hidden;
            input.style.textOverflow = TextOverflow.Clip;
        }
    }

    // Toggle box shares the textfield's BrowserInputBg so the two controls
    // look like one family. Border color matches the Toaster toggle palette
    // (UITools.CreateConfigurationCheckbox).
    internal static void StyleToggleBox(Toggle toggle)
    {
        if (toggle == null) return;
        _styledOverlays.Add(toggle);
        var input = toggle.Q(className: "unity-toggle__input");
        if (input != null)
        {
            input.style.backgroundColor = new StyleColor(BrowserInputBg);
            input.style.borderTopColor = new StyleColor(ToasterToggleBorder);
            input.style.borderBottomColor = new StyleColor(ToasterToggleBorder);
            input.style.borderLeftColor = new StyleColor(ToasterToggleBorder);
            input.style.borderRightColor = new StyleColor(ToasterToggleBorder);
        }
    }

    private static VisualElement BuildCell(string label, VisualElement control)
    {
        var labelProp = control.GetType().GetProperty("label",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        try
        {
            // Capture the existing label text before we blank it so undo
            // can put it back ("SEARCH", "MAX PING", "SHOW FULL", ...).
            if (labelProp != null)
            {
                var original = labelProp.GetValue(control, null) as string;
                _originalLabels.Add((control, original));
                labelProp.SetValue(control, "");
            }
        }
        catch { }

        var labelChild = control.Q<Label>(className: "unity-base-field__label")
                      ?? control.Q<Label>(className: "unity-toggle__label")
                      ?? control.Q<Label>(className: "unity-text-field__label");
        if (labelChild != null)
        {
            labelChild.style.display = DisplayStyle.None;
            _hiddenInnerLabels.Add(labelChild);
        }

        control.style.marginLeft = 0;
        control.style.marginRight = 0;
        control.style.marginTop = 0;
        control.style.marginBottom = 0;
        control.style.flexGrow = 0;
        control.style.flexShrink = 0;

        var cell = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                height = 50,
                minHeight = 50,
                flexShrink = 0,
                marginBottom = 8,
                paddingLeft = 24,
                paddingRight = 24,
                backgroundColor = new StyleColor(BrowserRowBg),
            }
        };
        var lab = new Label(label)
        {
            style =
            {
                color = Color.white,
                fontSize = 24,
                unityFontStyleAndWeight = FontStyle.Normal,
                unityTextAlign = TextAnchor.MiddleLeft,
            }
        };
        cell.Add(lab);
        cell.Add(control);

        // Hover (and the click-anywhere-toggles behavior below) only make
        // sense for toggle rows. Text inputs aren't whole-row clickable —
        // the user has to click the field to type — so a row hover state
        // there falsely advertises clickability.
        if (control is Toggle toggle)
        {
            AddRowHoverEffects(cell);
            cell.RegisterCallback<ClickEvent>(evt =>
            {
                // If the click landed on the toggle (or anything inside
                // it), let the toggle handle it natively — flipping again
                // here would cancel that out.
                var t = evt.target as VisualElement;
                while (t != null && t != cell)
                {
                    if (t == toggle) return;
                    t = t.parent;
                }
                toggle.value = !toggle.value;
            });
        }

        return cell;
    }

    private static VisualElement BuildColumn()
    {
        return new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                flexGrow = 1,
                flexShrink = 1,
                flexBasis = 0,
            }
        };
    }

    private static void InlineFilters(UIServerBrowser browser)
    {
        if (browser == null || browser == _inlinedFor) return;
        try
        {
            var serverBrowser = AccessTools.Field(typeof(UIServerBrowser), "serverBrowser")?.GetValue(browser) as VisualElement;
            var filters = AccessTools.Field(typeof(UIServerBrowser), "filters")?.GetValue(browser) as VisualElement;
            var filtersButton = AccessTools.Field(typeof(UIServerBrowser), "filtersButton")?.GetValue(browser) as VisualElement;
            var refreshBtn = AccessTools.Field(typeof(UIServerBrowser), "refreshButton")?.GetValue(browser) as VisualElement;
            var newServerBtn = AccessTools.Field(typeof(UIServerBrowser), "newServerButton")?.GetValue(browser) as VisualElement;
            if (serverBrowser == null || filters == null) return;

            _hiddenFiltersButton = filtersButton;
            _hiddenFiltersPopup = filters;
            if (filtersButton != null) filtersButton.style.display = DisplayStyle.None;
            filters.style.display = DisplayStyle.None;

            var search = filters.Q<VisualElement>("SearchTextField")?.Q<TextField>();
            var maxPing = filters.Q<VisualElement>("MaxPingIntegerField")?.Q<IntegerField>();
            var showFull = filters.Q<VisualElement>("ShowFullToggle")?.Q<Toggle>();
            var showEmpty = filters.Q<VisualElement>("ShowEmptyToggle")?.Q<Toggle>();
            var showPwd = filters.Q<VisualElement>("ShowPasswordProtectedToggle")?.Q<Toggle>();
            var showModded = filters.Q<VisualElement>("ShowModdedToggle")?.Q<Toggle>();
            var showUnreach = filters.Q<VisualElement>("ShowUnreachableToggle")?.Q<Toggle>();
            _movedControls.Clear();
            _hiddenInnerLabels.Clear();
            _styledOverlays.Clear();
            _originalLabels.Clear();
            // Unregister any hover callbacks left over from a previous
            // inline cycle so we don't stack multiple PlaySelectSound
            // calls per hover.
            foreach (var kv in _buttonHoverCallbacks)
            {
                if (kv.Key == null) continue;
                try { kv.Key.UnregisterCallback(kv.Value.enter); } catch { }
                try { kv.Key.UnregisterCallback(kv.Value.leave); } catch { }
            }
            _buttonHoverCallbacks.Clear();

            // ---- Filter strip: two columns side-by-side ----
            var strip = new VisualElement
            {
                name = "PPKB_InlineFilters",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    flexShrink = 0,
                    // Sit flush against the server-list end. The
                    // server-list ScrollView above already has its own
                    // trailing space, so any extra margin here doubles
                    // up and looks like a big empty band.
                    marginTop = 0,
                    marginBottom = 0,
                }
            };

            var col1 = BuildColumn();
            var col2 = BuildColumn();

            if (search != null)
            {
                TrackMove(search);
                search.RemoveFromHierarchy();
                StyleInputField(search, 200);
                col1.Add(BuildCell("SEARCH", search));
            }
            if (maxPing != null)
            {
                TrackMove(maxPing);
                maxPing.RemoveFromHierarchy();
                StyleInputField(maxPing, 80);
                col1.Add(BuildCell("MAX PING", maxPing));
            }
            if (showUnreach != null)
            {
                TrackMove(showUnreach);
                showUnreach.RemoveFromHierarchy();
                StyleToggleBox(showUnreach);
                col1.Add(BuildCell("UNREACHABLE", showUnreach));
            }
            if (showModded != null) { TrackMove(showModded); showModded.RemoveFromHierarchy(); StyleToggleBox(showModded); col1.Add(BuildCell("MODDED", showModded)); }

            if (showFull != null)   { TrackMove(showFull);   showFull.RemoveFromHierarchy();   StyleToggleBox(showFull);   col2.Add(BuildCell("FULL", showFull));     }
            if (showEmpty != null)  { TrackMove(showEmpty);  showEmpty.RemoveFromHierarchy();  StyleToggleBox(showEmpty);  col2.Add(BuildCell("EMPTY", showEmpty));   }
            if (showPwd != null)    { TrackMove(showPwd);    showPwd.RemoveFromHierarchy();    StyleToggleBox(showPwd);    col2.Add(BuildCell("LOCKED", showPwd));    }

            // Fill the empty 4th slot in col2 with the REFRESH + NEW SERVER buttons.
            if (refreshBtn != null && newServerBtn != null)
            {
                TrackMove(refreshBtn);
                TrackMove(newServerBtn);
                refreshBtn.RemoveFromHierarchy();
                newServerBtn.RemoveFromHierarchy();
                foreach (var b in new[] { refreshBtn, newServerBtn })
                {
                    _styledOverlays.Add(b);
                    b.style.height = 50;
                    b.style.flexGrow = 1;
                    b.style.flexBasis = 0;
                    b.style.flexShrink = 1;
                    b.style.minWidth = 0;
                    b.style.marginLeft = 0;
                    b.style.marginRight = 0;
                    b.style.marginTop = 0;
                    b.style.marginBottom = 0;
                    b.style.fontSize = 24;
                    b.style.unityFontStyleAndWeight = FontStyle.Normal;
                    b.style.color = Color.white;
                    b.style.backgroundColor = new StyleColor(BrowserRowBg);
                    b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0;
                    b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0;
                    b.style.paddingLeft = 24; b.style.paddingRight = 24;
                    b.style.unityTextAlign = TextAnchor.MiddleLeft;

                    // Hover: brighten on enter, restore on leave. We do
                    // NOT play a tick sound here — the base-game button
                    // plays its own hover sound natively. Adding ours
                    // produced two ticks per hover. Callback refs go
                    // into _buttonHoverCallbacks so we can deregister
                    // them cleanly on undo / re-apply.
                    var btnRef = b;
                    EventCallback<MouseEnterEvent> onEnter = _ =>
                    {
                        if (!btnRef.enabledSelf) return;
                        btnRef.style.backgroundColor = new StyleColor(BrowserRowHover);
                    };
                    EventCallback<MouseLeaveEvent> onLeave = _ =>
                    {
                        btnRef.style.backgroundColor = new StyleColor(BrowserRowBg);
                    };
                    btnRef.RegisterCallback(onEnter);
                    btnRef.RegisterCallback(onLeave);
                    _buttonHoverCallbacks[btnRef] = (onEnter, onLeave);
                }
                refreshBtn.style.marginRight = 4;
                newServerBtn.style.marginLeft = 4;

                var buttonsRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        justifyContent = Justify.SpaceBetween,
                        height = 50,
                        minHeight = 50,
                        flexShrink = 0,
                        marginBottom = 8,
                    }
                };
                buttonsRow.Add(refreshBtn);
                buttonsRow.Add(newServerBtn);
                col2.Add(buttonsRow);
            }

            strip.Add(col1);
            strip.Add(col2);
            // Two equal-width columns. Outer edges align with the panel
            // edges; inner gap is 8px total (4px on each column's facing
            // side) to match the row vertical gap.
            col1.style.marginLeft = 0;
            col1.style.marginRight = 4;
            col2.style.marginLeft = 4;
            col2.style.marginRight = 0;

            serverBrowser.Add(strip);

            _inlinedFor = browser;
        }
        catch (Exception e) { Debug.LogWarning("[QoL] ServerBrowser inline-filters failed: " + e.Message); }
    }
}
