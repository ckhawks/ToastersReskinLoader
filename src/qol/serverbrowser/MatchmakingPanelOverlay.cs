// MatchmakingPanelOverlay — shared reflection shim over the vanilla
// UIMatchmaking "Matching" panel.
//
// Both the server slot-queue (ServerSlotQueue) and the title-screen
// Quick Join overlay (MainMenuButtons) hijack this same vanilla panel so
// they get its styling for free. They used to each cache an identical set
// of UIMatchmaking MethodInfos and reimplement the panel lookup, so a
// change to one was easy to forget in the other. This centralizes the
// MethodInfo cache + panel/container lookup + the low-level setters.
//
// Every setter is self-contained and best-effort: it resolves the live
// panel itself and swallows reflection failures, mirroring the old
// "*Safe" wrappers. Callers layer their own presentation (label
// injection, rich-text phase strings) on top.

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UIElements;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol.serverbrowser;

internal static class MatchmakingPanelOverlay
{
    private static readonly Type Type = AccessTools.TypeByName("UIMatchmaking");
    private static readonly MethodInfo _setVisible    = Type?.GetMethod("SetMatchingVisibility");
    private static readonly MethodInfo _setPhaseText  = Type?.GetMethod("SetMatchingPhaseText");
    private static readonly MethodInfo _setConnectVis = Type?.GetMethod("SetMatchingConnectButtonVisibility");
    private static readonly MethodInfo _setCloseVis   = Type?.GetMethod("SetMatchingCloseButtonVisibility");
    private static readonly MethodInfo _setTimeVis    = Type?.GetMethod("SetMatchingTimeVisibility");
    private static readonly MethodInfo _setTimeText   = Type?.GetMethod("SetMatchingTimeText");
    private static readonly PropertyInfo _isVisible   = Type?.GetProperty("IsVisible");
    private static readonly FieldInfo _matchingField  = Type?.GetField("matching",
        BindingFlags.Instance | BindingFlags.NonPublic);

    // The live UIMatchmaking instance off UIManager.Matchmaking, or null.
    internal static object Panel
    {
        get
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui == null) return null;
            return ui.GetType().GetField("Matchmaking",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ui);
        }
    }

    internal static void SetIsVisible(bool v)     { var p = Panel; if (p == null) return; try { _isVisible?.SetValue(p, v); } catch { } }
    internal static void SetVisible(bool v)       { var p = Panel; if (p == null) return; try { _setVisible?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetPhaseText(string t)   { var p = Panel; if (p == null) return; try { _setPhaseText?.Invoke(p, new object[] { t }); } catch { } }
    internal static void SetConnectButton(bool v) { var p = Panel; if (p == null) return; try { _setConnectVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetCloseButton(bool v)   { var p = Panel; if (p == null) return; try { _setCloseVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetTimeVisible(bool v)   { var p = Panel; if (p == null) return; try { _setTimeVis?.Invoke(p, new object[] { v }); } catch { } }
    internal static void SetTimeText(int seconds) { var p = Panel; if (p == null) return; try { _setTimeText?.Invoke(p, new object[] { seconds }); } catch { } }

    // The inner "matching" VisualElement (the row that holds PhaseLabel),
    // or null. Used by the slot-queue's label-injection.
    internal static VisualElement GetMatchingContainer()
    {
        var p = Panel;
        if (p == null || _matchingField == null) return null;
        return _matchingField.GetValue(p) as VisualElement;
    }
}
