using HarmonyLib;

namespace ToasterReskinLoader.emotes;

// Re-attaches autocomplete and the picker if the game rebuilds its chat UI.
[HarmonyPatch(typeof(UIChat), nameof(UIChat.Initialize))]
public static class ChatInitializePatch
{
    [HarmonyPostfix]
    public static void Postfix(UIChat __instance)
    {
        EmoteInput.OnChatInitialized(__instance);
    }
}

// Makes sure closing chat input really takes keyboard focus away from the hidden text field.
[HarmonyPatch(typeof(UIChat), nameof(UIChat.StopInput))]
public static class ChatStopInputPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            EmoteInput.OnChatInputStopped();
        }
        catch (System.Exception e)
        {
            Plugin.LogError($"[Emotes] Chat close hook failed: {e.Message}");
        }
    }
}

// Escape is an Input System action that closes chat. When it is closing our popup instead, skip it.
[HarmonyPatch(typeof(UIManager), "OnPauseActionPerformed")]
public static class PauseActionPatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        return !EmoteInput.ConsumeEscape();
    }
}
