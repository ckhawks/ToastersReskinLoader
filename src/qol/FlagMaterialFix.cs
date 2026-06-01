// Fixes the vanilla "everyone shows the same country flag" bug.
//
// Player flag meshes (PlayerHead -> Helmet*/Flag) render their country via
// MeshRendererTexturer.SetTexture, which writes `this.Material.mainTexture`.
// On the flag prefab the texturer's serialized `material` field is baked to a
// SHARED material asset (one for skater helmets, one for goalie helmets), and
// the Material getter only instantiates a per-renderer copy when that field is
// null. So every player's flag renderer writes its texture onto the SAME
// material -- last writer wins, and every player ends up showing one identical
// flag. (Jerseys/groin already work because their texturer's field is null and
// instantiates correctly; confirmed live by comparing material instance IDs:
// flags shared positive asset IDs #10300/#10298, jerseys had unique negative
// instanced IDs.)
//
// Fix: before each SetTexture, if the texturer hasn't instantiated its own
// material yet, null the cached field so the getter re-fetches via
// MeshRenderer.material -- which makes a unique per-renderer copy AND sets
// isMaterialInstantiated so OnDestroy cleans it up (no leak). Idempotent: a
// no-op once the texturer owns its instance, and harmless for texturers that
// already instantiate. This is the same de-sharing technique PartyLineup uses
// for player clones (see BreakMaterialSharing).
//
// Controlled by enableFlagMaterialFix (default on).

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToasterReskinLoader.qol;

internal static class FlagMaterialFix
{
    private static readonly FieldInfo _materialField = typeof(MeshRendererTexturer)
        .GetField("material", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo _isInstantiatedField = typeof(MeshRendererTexturer)
        .GetField("isMaterialInstantiated", BindingFlags.Instance | BindingFlags.NonPublic);

    [HarmonyPatch(typeof(MeshRendererTexturer), nameof(MeshRendererTexturer.SetTexture))]
    private static class SetTexture_BreakSharedMaterial
    {
        private static void Prefix(MeshRendererTexturer __instance)
        {
            // Default on; only skip when explicitly disabled. (Config may be null
            // for the brief window before QoLRunner bootstraps -- default to
            // applying the fix in that case, which is the correct behavior.)
            if (!(QoLRunner.Instance?.Config?.enableFlagMaterialFix ?? true)) return;
            if (_materialField == null || _isInstantiatedField == null) return;

            try
            {
                // If this texturer hasn't made its own material instance yet, its
                // cached `material` may be a shared asset baked into the prefab.
                // Null it so the Material getter instantiates a per-renderer copy
                // (and marks it for OnDestroy cleanup) instead of mutating the
                // shared asset that every other player's flag also points at.
                bool instantiated = (bool)_isInstantiatedField.GetValue(__instance);
                if (!instantiated)
                    _materialField.SetValue(__instance, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[QoL] FlagMaterialFix prefix failed: " + e.Message);
            }
        }
    }
}
