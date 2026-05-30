// Arena visual disable — extracted from PoncePlayerInput Core.cs into a
// standalone helper so the Toaster UI can call it directly.
//
// Two modes:
//   ApplyState(cfg)   — full blackout: destroys arena GameObjects and
//                       updates the Scenery Loader config file. Effective
//                       on next server join when objects spawn.
//   ApplyPartial(cfg) — per-component toggles (props / lights / skybox /
//                       particles) + ambient-audio volume.

using System;
using System.IO;
using ToasterReskinLoader.qol;
using UnityEngine;

namespace ToasterReskinLoader.qol;

public static class ArenaVisuals
{
    private static Material _originalSkybox;
    private static Material _baseGameSkybox;

    // Cached arena root, invalidated by InvalidateCache() (called from
    // SwapperManager.OnSceneLoaded). Avoids scene-wide FindObjectsByType
    // walks every time a user toggles a setting.
    private static GameObject _cachedArenaRoot;

    public static void InvalidateCache()
    {
        _cachedArenaRoot = null;
    }

    private static bool NameMatchesArenaRoot(string n) =>
        n == "HockeyArenaRoot"
        || n.IndexOf("OutdoorHockey", StringComparison.OrdinalIgnoreCase) >= 0
        || n.IndexOf("HockeyArena", StringComparison.OrdinalIgnoreCase) >= 0;

    private static GameObject FindArenaRoot()
    {
        if (_cachedArenaRoot != null) return _cachedArenaRoot;
        var all = UnityEngine.Object.FindObjectsByType<GameObject>(UnityEngine.FindObjectsSortMode.None);
        foreach (var go in all)
        {
            if (NameMatchesArenaRoot(go.name))
            {
                _cachedArenaRoot = go;
                break;
            }
        }
        return _cachedArenaRoot;
    }

    private class AudioVolumeTracker : MonoBehaviour
    {
        public float originalVolume = 1.0f;
    }

    public static void ApplyState(QoLConfig cfg)
    {
        if (cfg == null) return;
        bool disable = cfg.disableArenaVisuals;

        // dem's SceneryLoader integration — temporarily disabled. Restore
        // by uncommenting; the block toggled `useSceneLocally` in the
        // SceneryLoader config so the arena blackout would persist across
        // joins.
        // try
        // {
        //     string gameDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        //     string configDir = Path.Combine(gameDir, "config");
        //     string sceneryConfigPath = Path.Combine(configDir, "SceneryLoader", "SceneryLoaderConfig.local.json");
        //     if (File.Exists(sceneryConfigPath))
        //     {
        //         string jsonContent = File.ReadAllText(sceneryConfigPath);
        //         if (jsonContent.Contains("\"useSceneLocally\""))
        //         {
        //             string pattern = "\"useSceneLocally\"\\s*:\\s*(true|false)";
        //             string replacement = $"\"useSceneLocally\": {(!disable).ToString().ToLower()}";
        //             jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, pattern, replacement);
        //             File.WriteAllText(sceneryConfigPath, jsonContent);
        //         }
        //     }
        // }
        // catch (Exception e) { Debug.LogWarning("[PPKB/Arena] SceneryLoader config update failed: " + e.Message); }

        if (!disable)
        {
            // Re-enable: user needs to rejoin server for full restoration.
            return;
        }

        try
        {
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(UnityEngine.FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                string name = go.name;
                bool nameMatch =
                    name.IndexOf("HockeyArena",   StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("OutdoorHockey", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Scenery",       StringComparison.OrdinalIgnoreCase) >= 0
                    || (name.IndexOf("arena", StringComparison.OrdinalIgnoreCase) >= 0
                        && (name.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("(clone)", StringComparison.OrdinalIgnoreCase) >= 0));
                if (!nameMatch) continue;
                if (go.GetComponentsInChildren<Renderer>(true).Length == 0 &&
                    go.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    continue;
                UnityEngine.Object.Destroy(go);
            }
            InvalidateCache();

            if (_originalSkybox == null && RenderSettings.skybox != null)
                _originalSkybox = RenderSettings.skybox;

            if (_baseGameSkybox == null)
            {
                var allMaterials = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var mat in allMaterials)
                {
                    if (mat.name.Contains("Skybox") || mat.shader?.name == "Skybox/Procedural" || mat.shader?.name == "Skybox/6 Sided")
                    {
                        if (mat != _originalSkybox) { _baseGameSkybox = mat; break; }
                    }
                }
            }

            RenderSettings.skybox = _baseGameSkybox;
            DynamicGI.UpdateEnvironment();
        }
        catch (Exception e) { Debug.LogError("[PPKB/Arena] ApplyState full-disable failed: " + e); }
    }

    // GetComponentsInChildren scrapes anything parented under the arena
    // root, which on a live server includes Player GameObjects and the
    // base-game UIUsernames labels. Disabling those breaks the
    // nameplates ("Show Player Usernames" stops working). This filter
    // skips any component whose ancestor chain mentions player / puck /
    // username / UI / canvas / name so the prop/light/particle toggles
    // only affect actual scenery.
    private static bool IsArenaSceneryTransform(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (!string.IsNullOrEmpty(n))
            {
                if (n.IndexOf("Player",   System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("Puck",     System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("Username", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("Name",     System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("Label",    System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("Canvas",   System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (n.IndexOf("UI",       System.StringComparison.Ordinal) >= 0) return false;
            }
            t = t.parent;
        }
        return true;
    }

    public static void ApplyPartial(QoLConfig cfg)
    {
        if (cfg == null) return;
        try
        {
            var arenaRoot = FindArenaRoot();
            if (arenaRoot == null) return;

            // When DISABLING, only touch scenery (skip players/UI/etc).
            // When ENABLING (toggle off), re-enable everything so anyone
            // whose nameplates got hidden by older builds is restored.
            var renderers = arenaRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (cfg.disableArenaProps && !IsArenaSceneryTransform(r.transform)) continue;
                r.enabled = !cfg.disableArenaProps;
            }

            var lights = arenaRoot.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                if (cfg.disableArenaLights && !IsArenaSceneryTransform(l.transform)) continue;
                l.enabled = !cfg.disableArenaLights;
            }

            if (cfg.disableArenaSkybox)
            {
                if (_originalSkybox == null && RenderSettings.skybox != null)
                    _originalSkybox = RenderSettings.skybox;
                if (_baseGameSkybox == null)
                {
                    var allMaterials = Resources.FindObjectsOfTypeAll<Material>();
                    foreach (var mat in allMaterials)
                    {
                        if (mat.name.Contains("Skybox") || mat.shader?.name == "Skybox/Procedural" || mat.shader?.name == "Skybox/6 Sided")
                        {
                            if (mat != _originalSkybox) { _baseGameSkybox = mat; break; }
                        }
                    }
                }
                RenderSettings.skybox = _baseGameSkybox;
                DynamicGI.UpdateEnvironment();
            }
            else if (_originalSkybox != null)
            {
                RenderSettings.skybox = _originalSkybox;
                DynamicGI.UpdateEnvironment();
            }

            var particles = arenaRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (cfg.disableArenaParticles && !IsArenaSceneryTransform(ps.transform)) continue;
                if (cfg.disableArenaParticles)
                {
                    if (ps.isPlaying) { ps.Stop(); ps.Clear(); }
                    var em = ps.emission; em.enabled = false;
                }
                else
                {
                    var em = ps.emission; em.enabled = true;
                    if (!ps.isPlaying) ps.Play();
                }
            }

            var allAudio = UnityEngine.Object.FindObjectsByType<AudioSource>(UnityEngine.FindObjectsSortMode.None);
            foreach (var audio in allAudio)
            {
                var name = audio.gameObject.name;
                if (name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (name.IndexOf("voice",  StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (name.IndexOf("puck",   StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var parent = audio.transform.parent;
                if (parent != null)
                {
                    var parentName = parent.name;
                    if (parentName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (parentName.IndexOf("voice",  StringComparison.OrdinalIgnoreCase) >= 0) continue;
                }

                var tracker = audio.gameObject.GetComponent<AudioVolumeTracker>();
                if (tracker == null)
                {
                    tracker = audio.gameObject.AddComponent<AudioVolumeTracker>();
                    tracker.originalVolume = audio.volume;
                }
                audio.volume = tracker.originalVolume * cfg.arenaAudioVolume;
            }
        }
        catch (Exception e) { Debug.LogError("[PPKB/Arena] ApplyPartial failed: " + e); }
    }
}