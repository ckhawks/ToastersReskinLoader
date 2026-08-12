using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using ToasterReskinLoader.core;

namespace ToasterReskinLoader.swappers
{
    // Recolors the equipped jersey (torso) and pants (groin) toward a picked color:
    // colored pixels take the picked color's hue, and the jersey's brightness is
    // normalized so its brightest colored areas match the picked color's value —
    // picking the team color makes the jersey look like the team color. Whites and
    // blacks stay, so the style/stripes remain visible. Works on vanilla styles AND
    // custom reskins (the texture actually applied is recolored, never replaced).
    //
    // Performance: the per-pixel HSV math runs on a background thread; only the
    // texture reads/writes touch the main thread. Recolor results are cached per
    // (source texture, settings) so a drag doesn't redo work, and slider changes
    // are debounced by the UI.
    public static class JerseyColorSwapper
    {
        // Original (un-shifted) block texture per part, so a toggle-off restores exactly
        // what the game/JerseySwapper had applied.
        private static readonly Dictionary<(PlayerTeam, ulong, string), Texture> originalBlockTextures =
            new Dictionary<(PlayerTeam, ulong, string), Texture>();

        // The recolor we last wrote per part, so a re-apply with unchanged settings
        // doesn't re-recolor, and a jersey change under us is detected.
        private static readonly Dictionary<(PlayerTeam, ulong, string), Texture> lastAppliedTextures =
            new Dictionary<(PlayerTeam, ulong, string), Texture>();

        // Readable CPU copy of each source jersey texture (GPU readback is expensive —
        // do it once per source, reuse for every recolor).
        private static readonly Dictionary<int, Texture2D> readableBases = new Dictionary<int, Texture2D>();

        // Finished recolor results, keyed by (source id, settings). All players wearing
        // the same jersey share one result.
        private static readonly Dictionary<string, Texture2D> shiftedTextureCache = new Dictionary<string, Texture2D>();

        // Recolor keys currently being computed on a background thread.
        private static readonly HashSet<string> pendingKeys = new HashSet<string>();

        static readonly FieldInfo _meshRendererTexturerTorsoField = typeof(PlayerTorso)
            .GetField("meshRendererTexturer", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _meshRendererTexturerGroinField = typeof(PlayerGroin)
            .GetField("meshRendererTexturer", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _meshRendererField = typeof(MeshRendererTexturer)
            .GetField("meshRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _texturePropertyNameField = typeof(MeshRendererTexturer)
            .GetField("texturePropertyName", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo _materialIndexField = typeof(MeshRendererTexturer)
            .GetField("materialIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MaterialPropertyBlock _readBlock = new MaterialPropertyBlock();

        public static void ClearCache()
        {
            originalBlockTextures.Clear();
            lastAppliedTextures.Clear();
            readableBases.Clear();
            shiftedTextureCache.Clear();
            pendingKeys.Clear();
        }

        private static string GetTextureProperty(MeshRendererTexturer texturer)
            => _texturePropertyNameField?.GetValue(texturer) as string ?? "_BaseMap";

        private static int GetMaterialIndex(MeshRendererTexturer texturer)
            => _materialIndexField != null ? (int)_materialIndexField.GetValue(texturer) : 0;

        // Reads the texture the game currently has in the texturer's property block.
        private static Texture ReadBlockTexture(MeshRendererTexturer texturer)
        {
            if (texturer == null) return null;
            var renderer = (MeshRenderer)_meshRendererField.GetValue(texturer);
            if (renderer == null) return null;
            int idx = GetMaterialIndex(texturer);
            string prop = GetTextureProperty(texturer);
            renderer.GetPropertyBlock(_readBlock, idx);
            return _readBlock.GetTexture(prop);
        }

        private static MeshRendererTexturer GetPartTexturer(PlayerMesh mesh, string part)
        {
            if (mesh?.PlayerTorso == null || mesh.PlayerGroin == null) return null;
            return part == "torso"
                ? (MeshRendererTexturer)_meshRendererTexturerTorsoField.GetValue(mesh.PlayerTorso)
                : (MeshRendererTexturer)_meshRendererTexturerGroinField.GetValue(mesh.PlayerGroin);
        }

        // Copy any texture (readable or not) into a new readable Texture2D via a
        // RenderTexture round-trip — the game's jerseys aren't guaranteed readable.
        private static Texture2D MakeReadableCopy(Texture source)
        {
            try
            {
                var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                return copy;
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"JerseyColorSwapper: readable copy failed: {e.Message}");
                return null;
            }
        }

        // Main thread only: the readable base copy of a source texture (cached per source).
        private static Texture2D GetReadableBase(Texture source)
        {
            int id = source.GetInstanceID();
            if (readableBases.TryGetValue(id, out var cached) && cached != null)
                return cached;
            var copy = MakeReadableCopy(source);
            if (copy != null) readableBases[id] = copy;
            return copy;
        }

        // Background thread: pure CPU math, no Unity API that touches the GPU.
        // Colored pixels take the target color's HUE and SATURATION exactly, keeping
        // their own brightness so the texture's shading stays visible. Achromatic
        // targets (black/white/gray) have no meaningful hue, so they desaturate
        // fully and take the target's value — black becomes black, white white.
        private static Color[] RecolorLoop(Color[] src, float targetHue01, float targetSat01, float targetVal01)
        {
            bool achromatic = targetSat01 < 0.05f;
            var dst = new Color[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color c = src[i];
                Color.RGBToHSV(c, out _, out float s, out float v);
                if (s > 0.08f)
                {
                    float sat = achromatic ? 0f : targetSat01;
                    float val = achromatic ? targetVal01 : v;
                    var rgb = Color.HSVToRGB(targetHue01, sat, val);
                    rgb.a = c.a;
                    dst[i] = rgb;
                }
                else
                {
                    dst[i] = c;
                }
            }
            return dst;
        }

        private static string BuildKey(Texture source, float targetHue01, float targetSat01, float targetVal01)
        {
            return source.GetInstanceID() + "|recolor|"
                + Mathf.RoundToInt(targetHue01 * 360f) + "|"
                + Mathf.RoundToInt(targetSat01 * 100f) + "|"
                + Mathf.RoundToInt(targetVal01 * 100f);
        }

        private static void ApplyPart(
            PlayerMesh mesh, ulong playerId, PlayerTeam team, string part,
            bool enabled, float targetHue01, float targetSat01, float targetVal01)
        {
            if (mesh == null) return;
            var texturer = GetPartTexturer(mesh, part);
            if (texturer == null) return;
            var renderer = (MeshRenderer)_meshRendererField.GetValue(texturer);
            if (renderer == null) return;

            var partKey = (team, playerId, part);
            Texture current = ReadBlockTexture(texturer);

            // Toggle off — restore whatever the game/JerseySwapper had applied.
            if (!enabled)
            {
                if (originalBlockTextures.TryGetValue(partKey, out var orig) && orig != null)
                    texturer.SetTexture(orig);
                return;
            }

            // Pick the base texture to recolor: the first real texture we see, refreshed
            // whenever the block changes under us (custom jersey applied/removed, style
            // changed) and the new texture isn't one of our own outputs.
            Texture baseTex;
            if (!originalBlockTextures.TryGetValue(partKey, out baseTex) || baseTex == null)
            {
                if (current == null) return; // game hasn't applied a jersey yet — retry next pass
                baseTex = current;
                originalBlockTextures[partKey] = baseTex;
            }
            else if (current != null && current != baseTex &&
                     (!lastAppliedTextures.TryGetValue(partKey, out var last) || current != last))
            {
                baseTex = current;
                originalBlockTextures[partKey] = baseTex;
            }

            if (baseTex == null) return;

            string key = BuildKey(baseTex, targetHue01, targetSat01, targetVal01);

            // Already recolored — apply immediately.
            if (shiftedTextureCache.TryGetValue(key, out var ready))
            {
                texturer.SetTexture(ready);
                lastAppliedTextures[partKey] = ready;
                return;
            }

            // A recolor for this exact key is already in flight — skip this pass;
            // the completion applies to everyone.
            if (!pendingKeys.Add(key)) return;

            var baseReadable = GetReadableBase(baseTex);
            if (baseReadable == null)
            {
                pendingKeys.Remove(key);
                return;
            }

            Color[] src = baseReadable.GetPixels(); // main-thread memcpy (safe)
            float hue = targetHue01, sat = targetSat01, val = targetVal01;
            try
            {
                Task.Run(() =>
                {
                    Color[] dst = RecolorLoop(src, hue, sat, val);
                    MainThreadDispatcher.Run(() =>
                    {
                        try
                        {
                            var result = new Texture2D(baseReadable.width, baseReadable.height, TextureFormat.RGBA32, false);
                            result.SetPixels(dst);
                            result.Apply();
                            result.name = baseTex.name + "_recolor";
                            shiftedTextureCache[key] = result;
                            pendingKeys.Remove(key);
                            texturer.SetTexture(result);
                            lastAppliedTextures[partKey] = result;
                            // Everyone else wearing the same jersey picks up the cached
                            // result in one pass.
                            try { ApplyAll(); } catch { }
                        }
                        catch (Exception e)
                        {
                            pendingKeys.Remove(key);
                            Plugin.LogWarning($"JerseyColorSwapper: apply failed: {e.Message}");
                        }
                    });
                });
            }
            catch (Exception e)
            {
                pendingKeys.Remove(key);
                Plugin.LogWarning($"JerseyColorSwapper: recolor task failed: {e.Message}");
            }
        }

        /// <summary>Re-colors every spawned player's jersey/pants from the current profile.</summary>
        public static void ApplyAll()
        {
            try
            {
                // Spawned players — PlayerManager may not exist in the locker room,
                // so this branch must not gate the preview branch below.
                if (PlayerManager.Instance != null)
                {
                    foreach (var team in new[] { PlayerTeam.Blue, PlayerTeam.Red })
                    {
                        foreach (Player player in PlayerManager.Instance.GetPlayersByTeam(team))
                            ApplyForPlayer(player);
                    }
                }

                // Locker-room preview has no Player object — apply from the menu context
                if (ChangingRoomHelper.IsInMainMenu())
                {
                    ChangingRoomHelper.Scan();
                    var mesh = ChangingRoomHelper.GetPlayerMesh();
                    if (mesh != null)
                        ApplyForMesh(mesh, SettingsManager.Team, SettingsManager.Role);
                }
            }
            catch (Exception e) { Plugin.LogWarning($"JerseyColorSwapper.ApplyAll failed: {e.Message}"); }
        }

        public static void ApplyForPlayer(Player player)
        {
            try
            {
                if (player == null || player.PlayerBody?.PlayerMesh == null) return;
                if (player.PlayerBody.PlayerMesh.PlayerTorso == null || player.PlayerBody.PlayerMesh.PlayerGroin == null) return;

                var team = player.Team;
                if (team is not (PlayerTeam.Blue or PlayerTeam.Red)) return;

                ApplyForMesh(player.PlayerBody.PlayerMesh, team, player.Role, player.OwnerClientId);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"JerseyColorSwapper.ApplyForPlayer failed: {e.Message}");
            }
        }

        /// <summary>
        /// Applies the recolor to a player mesh directly — used for the locker-room
        /// preview, which has no Player object.
        /// </summary>
        public static void ApplyForMesh(PlayerMesh mesh, PlayerTeam team, PlayerRole role, ulong playerId = 0)
        {
            try
            {
                if (mesh == null) return;
                if (team is not (PlayerTeam.Blue or PlayerTeam.Red)) return;
                if (mesh.PlayerTorso == null || mesh.PlayerGroin == null) return;

                var profile = ReskinProfileManager.currentProfile;
                bool blue = team == PlayerTeam.Blue;
                bool enabled = blue ? profile.blueJerseyColorEnabled : profile.redJerseyColorEnabled;

                // Target: the custom team color, or the picked jersey color. Its HUE
                // and SATURATION are applied exactly — the texture keeps its own
                // brightness/shading, so a matching color looks identical to the
                // normal jersey. Black/white picks also pull brightness toward the
                // target (see RecolorLoop).
                Color target = (blue ? profile.blueJerseyUseTeamColor : profile.redJerseyUseTeamColor)
                    ? (blue ? profile.blueTeamColor : profile.redTeamColor)
                    : (blue ? profile.blueJerseyColor : profile.redJerseyColor);
                Color.RGBToHSV(target, out float targetHue, out float targetSat, out float targetVal);

                ApplyPart(mesh, playerId, team, "torso", enabled, targetHue, targetSat, targetVal);
                ApplyPart(mesh, playerId, team, "groin", enabled, targetHue, targetSat, targetVal);
            }
            catch (Exception e)
            {
                Plugin.LogDebug($"JerseyColorSwapper.ApplyForMesh failed: {e.Message}");
            }
        }
    }
}
