using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Puck indicator
/// </summary>
public static class PuckIndicatorSwapper
{
    // ── State ──────────────────────────────────────────────────────────
    private static bool               _applied;
    private static VisualElement      _overlay;
    private static VisualElement      _arrow;       // the little triangle
    private static PuckIndicatorTicker _ticker;

    // Smoothing history (previous-frame values for lerp)
    private static Vector2  _smoothPos;
    private static float    _smoothRot;
    private static float    // current displayed opacity (lerps toward target)
                            _smoothAlpha;
    private static float    _targetAlpha;

    // Cached style values to avoid redundant per-frame writes
    private static float    _cachedSize = -1f;
    private static Color    _cachedColor = new Color(-1, -1, -1, -1);

    // Ticker error suppression (don't spam)
    private static bool     _tickErrorLogged;


    // ── Public API ─────────────────────────────────────────────────────

    public static void ApplyAll()
    {
        if (ReskinProfileManager.currentProfile.puckIndicatorEnabled)
            Apply();
        else
            Remove();
    }

    public static void Apply()
    {
        if (_applied) return;
        Plugin.Log("PuckIndicatorSwapper.Apply()");

        try
        {
            BuildOverlay();
            EnsureTicker();
            _smoothAlpha   = 0f;
            _targetAlpha   = 0f;
            _smoothPos     = new Vector2(Screen.width * 0.5f, 0f);
            _smoothRot     = 0f;
            _applied = true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.Apply: {ex}");
        }
    }

    public static void Remove()
    {
        if (!_applied) return;
        Plugin.Log("PuckIndicatorSwapper.Remove()");

        try
        {
            _arrow?.RemoveFromHierarchy();
            _overlay?.RemoveFromHierarchy();
            _arrow = null;
            _overlay = null;

            if (_ticker != null)
            {
                UnityEngine.Object.Destroy(_ticker);
                _ticker = null;
            }

            _applied = false;
            _cachedSize = -1f;
            _cachedColor = new Color(-1, -1, -1, -1);
            _tickErrorLogged = false;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.Remove: {ex}");
        }
    }

    // ── Overlay build ──────────────────────────────────────────────────

    private static void BuildOverlay()
    {
        var ui = UIManager.Instance;
        if (ui == null || ui.RootVisualElement == null) return;

        _overlay = new VisualElement { name = "PuckIndicatorOverlay" };
        _overlay.style.position  = Position.Absolute;
        _overlay.style.left      = 0;
        _overlay.style.top       = 0;
        _overlay.style.right     = 0;
        _overlay.style.bottom    = 0;
        _overlay.style.overflow  = Overflow.Visible;
        _overlay.pickingMode    = PickingMode.Ignore;

        // ── Arrow ──────────────────────────────────────────────────────
        // We build a proper right-pointing triangle using the
        // transparent-border trick, then rotate the whole element.
        //
        //      ▲
        //     ◀▶   ← the "point" of the arrow faces right by default
        //      ▼
        //
        // A tall thin rectangle with only the right border coloured
        // produces a clean CSS triangle suitable for rotation.
        _arrow = new VisualElement { name = "PuckArrow" };
        _arrow.style.position       = Position.Absolute;
        _arrow.pickingMode         = PickingMode.Ignore;
        _arrow.style.overflow      = Overflow.Hidden;
        _arrow.style.width          = 20;      // overridden each frame
        _arrow.style.height         = 20;      // overridden each frame
        // Transparent on three sides → coloured triangle on the 4th
        _arrow.style.borderTopColor     = new StyleColor(new Color(0, 0, 0, 0));
        _arrow.style.borderBottomColor  = new StyleColor(new Color(0, 0, 0, 0));
        _arrow.style.borderLeftColor    = new StyleColor(new Color(0, 0, 0, 0));
        _arrow.style.borderRightColor   = new StyleColor(Color.white); // overridden
        _arrow.style.borderTopWidth     = 10;
        _arrow.style.borderBottomWidth  = 10;
        _arrow.style.borderLeftWidth    = 0;
        _arrow.style.borderRightWidth   = 14;

        // Seed caches to match initial style so first Tick doesn't
        // redundantly write static border widths.
        _cachedSize  = 20f;
        _cachedColor = Color.white;

        _overlay.Add(_arrow);
        ui.RootVisualElement.Add(_overlay);
    }

    private static void EnsureTicker()
    {
        if (_ticker != null) return;
        var go = UIManager.Instance?.gameObject;
        if (go == null) return;

        _ticker = go.GetComponent<PuckIndicatorTicker>();
        if (_ticker == null)
            _ticker = go.AddComponent<PuckIndicatorTicker>();
    }

    // ── Per-frame tick ─────────────────────────────────────────────────

    internal static void Tick()
    {
        // Reset one-shot error flag so a new exception logs
        _tickErrorLogged = false;

        if (!_applied || _overlay == null || _arrow == null) return;

        var profile = ReskinProfileManager.currentProfile;
        if (profile == null || !profile.puckIndicatorEnabled)
        {
            _overlay.style.display = DisplayStyle.None;
            return;
        }

        // Re-validate overlay is still in the UI hierarchy. If the UI
        // root was rebuilt (scene reload) the element will be detached.
        if (_overlay.panel == null)
        {
            _applied = false;
            BuildOverlay();
            if (_overlay == null) return;
        }

        _overlay.style.display = DisplayStyle.Flex;

        // ── Camera (re-resolve every frame — handles respawn / spectate) ──
        Camera cam = FindCamera();
        if (cam == null) { FadeOut(); return; }

        // ── Puck ───────────────────────────────────────────────────────
        Puck puck = FindActivePuck();
        if (puck == null || puck.gameObject == null) { FadeOut(); return; }
        Vector3 puckPos = puck.transform.position;

        // ── Direction from camera to puck ───────────────────────────────
        Vector3 toPuck = (puckPos - cam.transform.position);

        // Project onto camera axes
        float dotFwd   = Vector3.Dot(toPuck, cam.transform.forward);
        float dotRight = Vector3.Dot(toPuck, cam.transform.right);
        float dotUp    = Vector3.Dot(toPuck, cam.transform.up);

        bool behindCam = dotFwd < 0f;

        // Perspective division using |dotFwd| as the denominator.
        // Using abs ensures behind-camera pucks map to the correct
        // screen edge (e.g. puck behind+right → arrow on right edge).
        float fwdAbs = Mathf.Abs(dotFwd);
        if (fwdAbs < 0.0001f) fwdAbs = 0.0001f;

        float perspX = dotRight / fwdAbs;
        float perspY = dotUp   / fwdAbs;

        // Correct horizontal FOV: Unity's fieldOfView is vertical FOV.
        // halfHFovTan = tan(atan(tan(vfov/2) * aspect))
        // halfVFovTan = tan(vfov/2)
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float halfVFovTan = Mathf.Tan(vfov * 0.5f);
        float halfHFovTan = Mathf.Tan(Mathf.Atan(halfVFovTan * cam.aspect));

        float nx = perspX / halfHFovTan;
        float ny = perspY / halfVFovTan;

        // ── On/off-screen check ──────────────────────────────────────────
        // Puck is on-screen when it's in front of the camera and within
        // the frustum.  nx/ny are normalised so ±1 = screen edge.
        // A small dead-band (0.95) prevents flicker right at the boundary.
        if (!behindCam && Mathf.Abs(nx) < 0.95f && Mathf.Abs(ny) < 0.95f)
        {
            FadeOut();
            return;
        }

        // ── Map to screen-edge position ────────────────────────────────
        // Unity UI: Y=0 is TOP.  +ny = puck above camera frustum →
        // arrow should be at top (small Y), so flip Y.
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float margin = profile.puckIndicatorEdgeMargin;
        Vector2 dir = new Vector2(nx, -ny);

        Vector2 targetPos;
        float targetRot;

        if (Mathf.Abs(dir.x) < 0.001f && Mathf.Abs(dir.y) < 0.001f)
        {
            // Degenerate: puck is on the camera's forward axis.
            // Use world-space height delta to pick top vs bottom edge.
            float heightDelta = puckPos.y - cam.transform.position.y;
            if (heightDelta > 0f)
            {
                targetPos = new Vector2(centre.x, margin);
                targetRot = -90f;
            }
            else
            {
                targetPos = new Vector2(centre.x, Screen.height - margin);
                targetRot = 90f;
            }
        }
        else
        {
            float absX = Mathf.Abs(dir.x);
            float absY = Mathf.Abs(dir.y);
            float scale = Mathf.Max(absX, absY);
            Vector2 ndir = dir / scale;

            float rectHalfW = Screen.width  * 0.5f - margin;
            float rectHalfH = Screen.height * 0.5f - margin;

            float t;
            if (Mathf.Abs(ndir.x) > Mathf.Abs(ndir.y))
                t = rectHalfW / Mathf.Abs(ndir.x);
            else
                t = rectHalfH / Mathf.Abs(ndir.y);

            targetPos = centre + ndir * t;

            targetPos.x = Mathf.Clamp(targetPos.x, margin, Screen.width  - margin);
            targetPos.y = Mathf.Clamp(targetPos.y, margin, Screen.height - margin);

            targetRot = Mathf.Atan2(-ndir.y, -ndir.x) * Mathf.Rad2Deg;
        }

        // ── Smoothing ──────────────────────────────────────────────────
        float lerpT = 1f - Mathf.Exp(-22f * Time.deltaTime);
        _smoothPos.x = Mathf.Lerp(_smoothPos.x, targetPos.x, lerpT);
        _smoothPos.y = Mathf.Lerp(_smoothPos.y, targetPos.y, lerpT);

        float rotDiff = Mathf.DeltaAngle(_smoothRot, targetRot);
        _smoothRot = Mathf.LerpAngle(_smoothRot, _smoothRot + rotDiff, lerpT);

        _targetAlpha = 1f;
        _smoothAlpha = Mathf.Lerp(_smoothAlpha, _targetAlpha, lerpT);

        // ── Apply to the VisualElement ─────────────────────────────────
        float size    = profile.puckIndicatorArrowSize;
        Color baseCol = profile.puckIndicatorArrowColor;
        baseCol.a     = profile.puckIndicatorOpacity * _smoothAlpha;
        float alpha   = baseCol.a;

        float halfW = size * 0.5f;
        float halfH = size * 0.35f;

        // Always-writes: position, rotation, display (change every frame)
        _arrow.style.left   = _smoothPos.x - halfH;
        _arrow.style.top    = _smoothPos.y - halfW;
        _arrow.style.rotate = new Rotate(_smoothRot);
        _arrow.style.display = DisplayStyle.Flex;

        // Conditional writes: only touch style props when the value changed
        if (!Mathf.Approximately(size, _cachedSize))
        {
            _cachedSize = size;
            halfW = size * 0.5f;
            halfH = size * 0.35f;
            _arrow.style.left  = _smoothPos.x - halfH;  // recompute with new halfH
            _arrow.style.width  = halfH;
            _arrow.style.height = size;
            _arrow.style.borderTopWidth    = halfW;
            _arrow.style.borderBottomWidth = halfW;
            _arrow.style.borderLeftWidth   = 0;
            _arrow.style.borderRightWidth  = halfH * 1.5f;
        }

        if (baseCol != _cachedColor)
        {
            _cachedColor = baseCol;
            _arrow.style.borderRightColor = new StyleColor(baseCol);
        }
    }

    private static void FadeOut()
    {
        float lerpT = 1f - Mathf.Exp(-18f * Time.deltaTime);
        _targetAlpha = 0f;
        _smoothAlpha = Mathf.Lerp(_smoothAlpha, _targetAlpha, lerpT);

        if (_smoothAlpha < 0.01f)
            _arrow.style.display = DisplayStyle.None;
    }

    // ── Camera helper ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the camera component that belongs to the local gameplay
    /// player.  Re-resolves every call to handle respawn/spectate
    /// transitions.  Falls back to Camera.main.
    /// </summary>
    private static Camera FindCamera()
    {
        try
        {
            var lp = MonoBehaviourSingleton<PlayerManager>.Instance?.GetLocalPlayer();
            if (lp != null && lp.gameObject != null)
            {
                var c = lp.gameObject.GetComponentInChildren<Camera>(true);
                if (c != null && c.enabled) return c;
            }

            return Camera.main;
        }
        catch { return null; }
    }

    // ── Puck helper ───────────────────────────────────────────────────

    private static Puck FindActivePuck()
    {
        try
        {
            // Use the built-in which already excludes replay pucks
            return MonoBehaviourSingleton<PuckManager>.Instance?.GetPuck();
        }
        catch { return null; }
    }

    // ── Helper MonoBehaviour ───────────────────────────────────────────

    internal class PuckIndicatorTicker : MonoBehaviour
    {
        private void LateUpdate()
        {
            try { Tick(); }
            catch (Exception ex)
            {
                // Transient exception (e.g. scene/phase transition null).
                // Hide the overlay so we don't leave a frozen arrow, but
                // keep _applied true so we recover on the next frame.
                if (!_tickErrorLogged)
                {
                    _tickErrorLogged = true;
                    Plugin.LogError($"PuckIndicatorSwapper tick (suppressed): {ex}");
                }
                // Hide arrow immediately to prevent frozen artifact
                if (_arrow != null)
                    _arrow.style.display = DisplayStyle.None;
            }
        }
    }
}
