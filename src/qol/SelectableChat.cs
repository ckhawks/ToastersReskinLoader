// Drag-selectable chat text + left/right-click-copy.
//
// UI Toolkit's TextElement (Label's base) supports selection via the
// `selection` interface (Unity 2023+). Enabling `isSelectable` on the chat
// labels lets the user drag-highlight chat text; a PointerDown handler also
// copies the whole line to the system clipboard.
//
// PickingMode.Ignore on an ancestor does NOT block picking on descendants in
// UIToolkit — only the label itself needs pickingMode=Position to receive
// pointer events, so no ancestor walk is required.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class SelectableChat
{
    private static bool Enabled =>
        QoLRunner.Instance?.Config?.enableChatDragSelect ?? true;

    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    private static class Chat_MakeSelectable_Postfix
    {
        private static void Postfix(UIChat __instance, ChatMessage chatMessage)
        {
            if (!Enabled) return;
            try
            {
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

                    // Left- OR right-click copies the whole message line.
                    var copyTarget = lbl;
                    lbl.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        try
                        {
                            if (evt.button != 0 && evt.button != 1) return;
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
