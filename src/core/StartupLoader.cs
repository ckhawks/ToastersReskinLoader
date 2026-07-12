// StartupLoader.cs
//
// The original startup path decoded and mip-mapped every active reskin texture
// synchronously inside Plugin.OnEnable (LoadTexturesForActiveReskins →
// SwapperManager.SetAll). OnEnable is invoked by the game via reflection on the
// main thread (BasePlugin.Enable), so that whole loop froze the render thread —
// the game sat on a black/stale frame for however long the decode took.
//
// This spreads the texture warm-up across frames with a per-frame time budget so
// the game keeps rendering, shows a small overlay while it works, and only then
// runs the (now-cheap, fully-cached) apply pass. Texture2D.LoadImage/Apply must
// still run on the main thread, so this is frame-chunking, not true threading —
// but chunking is what actually kills the freeze. Background disk reads are a
// possible future optimization on top of this.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.swappers;

namespace ToasterReskinLoader.core;

public sealed class StartupLoader : MonoBehaviour
{
    // How long per frame we're willing to spend decoding textures before yielding.
    // A single large texture can overrun this (LoadImage can't be subdivided), but
    // we always yield afterwards so no frame stacks multiple heavy decodes.
    private const double FrameBudgetMs = 6.0;

    private static StartupLoader _instance;

    private VisualElement _overlay;
    private VisualElement _progressFill;
    private Label _progressLabel;
    private Action _onComplete;

    /// <summary>
    /// Warms the active reskin textures across frames, then runs <paramref name="onComplete"/>
    /// (the apply pass) once everything is cached. Returns immediately; the work runs
    /// over the following frames on a self-hosted GameObject.
    /// </summary>
    public static void Begin(Action onComplete)
    {
        if (_instance != null)
        {
            // A load is already in flight — just chain the new completion after it.
            var prev = _instance._onComplete;
            _instance._onComplete = () => { prev?.Invoke(); onComplete?.Invoke(); };
            return;
        }

        var go = new GameObject("TRL_StartupLoader");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<StartupLoader>();
        _instance._onComplete = onComplete;
        _instance.StartCoroutine(_instance.LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        List<ReskinRegistry.ReskinEntry> entries = null;
        try
        {
            entries = ReskinProfileManager.GetAllActiveReskinEntries();
            // Drop anything no longer referenced before we start pulling from disk.
            TextureManager.UnloadUnusedTextures(entries);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[StartupLoader] Failed to gather active reskins: {e.Message}");
            entries = new List<ReskinRegistry.ReskinEntry>();
        }

        int total = entries.Count;
        if (total > 0)
        {
            TryBuildOverlay(total);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                try { TextureManager.GetTexture(entries[i]); }
                catch (Exception e) { Plugin.LogError($"[StartupLoader] Texture load failed: {e.Message}"); }

                UpdateProgress((i + 1) / (float)total, i + 1, total);

                if (sw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                {
                    yield return null;
                    sw.Restart();
                }
            }

            try { PuckSwapper.GetBumpMapPathAndLoad(); }
            catch (Exception e) { Plugin.LogError($"[StartupLoader] Bump map load failed: {e.Message}"); }

            Plugin.Log($"[StartupLoader] Warmed {total} reskin textures across frames.");
            // One more frame so the full progress bar paints before we apply.
            yield return null;
        }

        // Everything is cached now, so the apply pass is cheap — run it in one shot.
        try { _onComplete?.Invoke(); }
        catch (Exception e) { Plugin.LogError($"[StartupLoader] onComplete failed: {e.Message}"); }

        // Fade the overlay out so reskins don't visibly pop in behind a hard cut.
        yield return FadeOutAndTeardown();
    }

    private void TryBuildOverlay(int total)
    {
        try
        {
            var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
            if (root == null) return;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f, 1f));
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            // Draw above the menu but let clicks fall through — it's purely cosmetic.
            _overlay.pickingMode = PickingMode.Ignore;

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.Center;
            box.style.width = 360;

            var title = new Label("Loading reskins…");
            title.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            title.style.fontSize = 20;
            title.style.marginBottom = 12;
            box.Add(title);

            var track = new VisualElement();
            track.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            track.style.height = 10;
            track.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            track.style.borderTopLeftRadius = 5;
            track.style.borderTopRightRadius = 5;
            track.style.borderBottomLeftRadius = 5;
            track.style.borderBottomRightRadius = 5;
            track.style.overflow = Overflow.Hidden;

            _progressFill = new VisualElement();
            _progressFill.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
            _progressFill.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            _progressFill.style.backgroundColor = new StyleColor(new Color(0.85f, 0.55f, 0.2f));
            track.Add(_progressFill);
            box.Add(track);

            _progressLabel = new Label($"0 / {total}");
            _progressLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            _progressLabel.style.fontSize = 12;
            _progressLabel.style.marginTop = 8;
            box.Add(_progressLabel);

            _overlay.Add(box);
            root.Add(_overlay);
        }
        catch (Exception e)
        {
            Plugin.LogError($"[StartupLoader] Overlay build failed (loading continues without it): {e.Message}");
            _overlay = null;
        }
    }

    private void UpdateProgress(float fraction, int done, int total)
    {
        if (_progressFill != null)
            _progressFill.style.width = new StyleLength(new Length(Mathf.Clamp01(fraction) * 100f, LengthUnit.Percent));
        if (_progressLabel != null)
            _progressLabel.text = $"{done} / {total}";
    }

    private IEnumerator FadeOutAndTeardown()
    {
        if (_overlay != null)
        {
            const int steps = 8;
            for (int i = steps; i >= 0; i--)
            {
                _overlay.style.opacity = i / (float)steps;
                yield return null;
            }
            try { _overlay.RemoveFromHierarchy(); } catch { }
            _overlay = null;
        }

        _instance = null;
        try { UnityEngine.Object.Destroy(gameObject); } catch { }
    }
}
