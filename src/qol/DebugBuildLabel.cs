using HarmonyLib;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol;

[HarmonyPatch(typeof(UIDebug), nameof(UIDebug.SetBuild))]
internal static class DebugBuildLabelPatch
{
    static void Prefix(ref string text)
    {
        int idx = text.IndexOf(' ', text.IndexOf(' ') + 1);
        if (idx < 0) return;
        text = $"{text.Substring(0, idx)} - TRL {Plugin.MOD_VERSION}{text.Substring(idx)}";
    }
}
