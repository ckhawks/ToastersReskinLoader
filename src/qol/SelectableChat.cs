// Drag-selectable chat text + left-click-copy.
//
// UI Toolkit's TextElement (Label's base) supports selection via the
// `selection` interface (Unity 2023+). Enabling `isSelectable` lets the user
// drag-highlight chat text. We also keep a left-click handler that copies the
// line's plain text to the system clipboard. Right-click is intentionally
// NOT handled so the game's built-in right-click behavior is preserved.
//
// The chat panel sits as a non-interactive HUD overlay: its parent chain has
// pickingMode=Ignore so pointer events fall through to the game. To make
// text interactive we walk the WHOLE ancestor chain from the chat view up
// to its panel root and flip every Ignore to Position.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class SelectableChat
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableChatDragSelect ?? true;

    private static bool _chatPickingOpened;
    private static void OpenChatPickingPath(UIChat chat)
    {
        if (_chatPickingOpened || chat == null) return;
        try
        {
            var view = AccessTools.Field(typeof(UIView), "view")?.GetValue(chat) as VisualElement;
            if (view == null) return;
            // Walk all the way up to the panel root and unblock pointer events.
            var cur = view;
            while (cur != null)
            {
                if (cur.pickingMode == PickingMode.Ignore) cur.pickingMode = PickingMode.Position;
                cur = cur.parent;
            }
            // Also flip any known internal containers explicitly.
            foreach (var name in new[] { "chat", "scrollView", "messages", "padding" })
            {
                var ve = AccessTools.Field(typeof(UIChat), name)?.GetValue(chat) as VisualElement;
                if (ve != null && ve.pickingMode == PickingMode.Ignore)
                    ve.pickingMode = PickingMode.Position;
            }
            _chatPickingOpened = true;
        }
        catch (Exception e) { Debug.LogWarning("[QoL] OpenChatPickingPath failed: " + e.Message); }
    }

    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    private static class Chat_MakeSelectable_Postfix
    {
        private static void Postfix(UIChat __instance, ChatMessage chatMessage)
        {
            if (!Enabled) return;
            try
            {
                OpenChatPickingPath(__instance);

                var messages = AccessTools.Field(typeof(UIChat), "messages")?.GetValue(__instance) as VisualElement;
                if (messages == null || messages.childCount == 0) return;
                var child = messages[messages.childCount - 1];
                if (child == null) return;

                child.pickingMode = PickingMode.Position;

                var labels = child.Query<Label>().ToList();
                foreach (var lbl in labels)
                {
                    lbl.focusable = true;
                    lbl.pickingMode = PickingMode.Position;
                    try
                    {
                        lbl.selection.isSelectable = true;
                        lbl.selection.doubleClickSelectsWord = true;
                        lbl.selection.tripleClickSelectsLine = true;
                    }
                    catch { }

                    // Left-click copies the whole message line. Right-click
                    // is left alone so the game's own context behavior runs.
                    var copyTarget = lbl;
                    lbl.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        try
                        {
                            if (evt.button != 0) return;
                            GUIUtility.systemCopyBuffer = StripRichText(copyTarget.text ?? "");
                            evt.StopPropagation();
                        }
                        catch { }
                    });
                }
            }
            catch (Exception e) { Debug.LogWarning("[QoL] selection-enable failed: " + e.Message); }
        }
    }

    private static string StripRichText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        // Quick and good-enough: drop everything inside <...> tags.
        var sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}
