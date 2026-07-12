// Ports the vanilla fix for the chat-open key leaking into the message so players get
// it before the game updates. The open key (e.g. Y) was typed into the freshly focused
// text field and only swallowed on an exact-string match against the physical key name.
//
// Two vanilla bugs, two patches:
//  1. UIManager.GetChatOpenCharacter used control.name (the physical key — always the
//     QWERTY letter) instead of control.displayName (the OS-layout character the field
//     actually receives), so the swallow missed on Dvorak/AZERTY and similar layouts.
//  2. UIChat.OnTextFieldFirstChange compared case-sensitively, so Caps Lock / Shift made
//     an uppercase character slip past the swallow.
//
// Forward-compatible: once the game ships the fix, patch 1 rewrites displayName to the
// same value and patch 2 finds the field already cleared, so both no-op.

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.hud;

internal static class ChatOpenCharacterFix
{
    [HarmonyPatch(typeof(UIManager), "GetChatOpenCharacter")]
    private static class GetChatOpenCharacter_UseDisplayName
    {
        private static void Postfix(InputAction.CallbackContext context, ref string __result)
        {
            __result = context.control != null && context.ReadValueAsButton()
                ? context.control.displayName
                : null;
        }
    }

    [HarmonyPatch(typeof(UIChat), "OnTextFieldFirstChange")]
    private static class OnTextFieldFirstChange_CaseInsensitive
    {
        private static readonly FieldInfo PendingField =
            AccessTools.Field(typeof(UIChat), "pendingOpenCharacter");
        private static readonly FieldInfo TextFieldField = AccessTools.Field(typeof(UIChat), "textField");

        // Capture the pending character before the original nulls it.
        private static void Prefix(UIChat __instance, out string __state)
        {
            __state = PendingField?.GetValue(__instance) as string;
        }

        // Vanilla only clears the leaked character on an exact-case match; clear it here too
        // when it differs only by case (Caps Lock / Shift). If vanilla already cleared it, the
        // field is empty and this no-ops.
        private static void Postfix(UIChat __instance, string __state)
        {
            if (string.IsNullOrEmpty(__state))
                return;

            if (TextFieldField?.GetValue(__instance) is not TextField textField)
                return;

            if (string.Equals(textField.value, __state, StringComparison.OrdinalIgnoreCase))
                textField.SetValueWithoutNotify(string.Empty);
        }
    }
}
