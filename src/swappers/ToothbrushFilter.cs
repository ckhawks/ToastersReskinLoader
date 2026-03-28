using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Removes the toothbrush mustache (ID 1028) from the game.
/// If the local player has it equipped, resets to none on startup.
/// Also hides it from the vanilla appearance UI.
/// </summary>
public static class ToothbrushFilter
{
    public const int TOOTHBRUSH_ID = 1028;

    /// <summary>
    /// If the local player currently has the toothbrush mustache selected, reset to none.
    /// </summary>
    public static void ResetIfActive()
    {
        if (SettingsManager.MustacheID == TOOTHBRUSH_ID)
        {
            Plugin.Log("[ToothbrushFilter] Player had toothbrush mustache equipped — resetting to none.");
            SettingsManager.UpdateMustacheID(-1);

            if (ChangingRoomHelper.IsInMainMenu())
                ChangingRoomHelper.SetMustacheID(-1);
        }
    }

    // ── Hide toothbrush from vanilla appearance UI ──
    //
    // UIAppearance.Initialize is called once before our mod loads, so we can't
    // prefix it. Instead we patch StyleRadioButtonGroups — a public method called
    // every time the appearance panel refreshes (team/role change, show, etc.).
    // After it styles all radio buttons, we find any with the toothbrush ID and
    // force-hide them.

    private static readonly FieldInfo MustachesRBGField = typeof(UIAppearance)
        .GetField("mustachesRadioButtonGroup", BindingFlags.Instance | BindingFlags.NonPublic);

    [HarmonyPatch(typeof(UIAppearance), nameof(UIAppearance.StyleRadioButtonGroups))]
    public static class HideToothbrushPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIAppearance __instance)
        {
            if (MustachesRBGField == null) return;
            var rbg = MustachesRBGField.GetValue(__instance) as RadioButtonGroup;
            if (rbg == null) return;

            var list = rbg.Query<VisualElement>("AppearanceItemList").First();
            if (list == null) return;

            foreach (var child in list.Children())
            {
                if (child.userData is Dictionary<string, object> data &&
                    data.TryGetValue("item", out var itemObj) &&
                    itemObj is Item item &&
                    item.id == TOOTHBRUSH_ID)
                {
                    child.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
