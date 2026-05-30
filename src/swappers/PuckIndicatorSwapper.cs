using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers;

/// <summary>
/// Screen-space puck direction indicator.
/// When the puck is off-screen, a diamond arrow appears on the nearest screen edge
/// pointing toward the puck's world position — just like the side-of-screen indicators in NHL EA.
///
/// Creates a VisualElement overlay parented under the UIManager root panel
/// so the arrow renders on top of the normal game HUD. A tiny helper
/// MonoBehaviour on the UIManager GameObject drives the per-frame update.
/// </summary>
public static class PuckIndicatorSwapper
{
    private static bool _isApplied;
    private static VisualElement _overlayRoot;
    private static VisualElement _leftArrow;
    private static VisualElement _rightArrow;
    private static VisualElement _topArrow;
    private static VisualElement _bottomArrow;

    // Helper MonoBehaviour that drives Tick() each LateUpdate
    private static PuckIndicatorTicker _ticker;
    private static Camera _camera;

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    public static void ApplyAll()
    {
        if (ReskinProfileManager.currentProfile.puckIndicatorEnabled)
            Apply();
        else
            Remove();
    }

    public static void Apply()
    {
        if (_isApplied) return;

        Plugin.Log("Apply() - PuckIndicatorSwapper");

        try
        {
            _camera = Camera.main;

            CreateOverlay();
            EnsureTicker();
            _isApplied = true;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.Apply(): {ex}");
        }
    }

    public static void Remove()
    {
        if (!_isApplied) return;

        Plugin.Log("Remove() - PuckIndicatorSwapper");

        try
        {
            if (_overlayRoot != null)
            {
                _overlayRoot.RemoveFromHierarchy();
                _overlayRoot = null;
            }

            _leftArrow = _rightArrow = _topArrow = _bottomArrow = null;

            if (_ticker != null)
            {
                UnityEngine.Object.Destroy(_ticker);
                _ticker = null;
            }

            _isApplied = false;
            _camera = null;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.Remove(): {ex}");
        }
    }

    // ---------------------------------------------------------------
    //  Overlay creation
    // ---------------------------------------------------------------

    private static void CreateOverlay()
    {
        var uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            Plugin.LogWarning("PuckIndicatorSwapper: UIManager not available.");
            return;
        }

        var root = uiManager.RootVisualElement;
        if (root == null)
        {
            Plugin.LogWarning("PuckIndicatorSwapper: RootVisualElement is null.");
            return;
        }

        _overlayRoot = new VisualElement { name = "PuckIndicatorOverlay" };
        _overlayRoot.style.position = Position.Absolute;
        _overlayRoot.style.left = Length.Percent(0);
        _overlayRoot.style.top = Length.Percent(0);
        _overlayRoot.style.width = Length.Percent(100);
        _overlayRoot.style.height = Length.Percent(100);
        _overlayRoot.style.overflow = Overflow.Visible;
        _overlayRoot.pickingMode = PickingMode.Ignore;

        _leftArrow = BuildArrow("Left");
        _rightArrow = BuildArrow("Right");
        _topArrow = BuildArrow("Top");
        _bottomArrow = BuildArrow("Bottom");

        _overlayRoot.Add(_leftArrow);
        _overlayRoot.Add(_rightArrow);
        _overlayRoot.Add(_topArrow);
        _overlayRoot.Add(_bottomArrow);

        root.Add(_overlayRoot);

        HideAllArrows();
    }

    private static VisualElement BuildArrow(string name)
    {
        var arrow = new VisualElement { name = $"PuckArrow_{name}" };
        arrow.style.position = Position.Absolute;
        arrow.style.display = DisplayStyle.None;
        arrow.pickingMode = PickingMode.Ignore;
        return arrow;
    }

    // ---------------------------------------------------------------
    //  Helper MonoBehaviour (per-frame tick)
    // ---------------------------------------------------------------

    private static void EnsureTicker()
    {
        if (_ticker != null) return;

        var uiManagerGO = UIManager.Instance?.gameObject;
        if (uiManagerGO == null) return;

        _ticker = uiManagerGO.GetComponent<PuckIndicatorTicker>();
        if (_ticker == null)
            _ticker = uiManagerGO.AddComponent<PuckIndicatorTicker>();
    }

    // ---------------------------------------------------------------
    //  Per-frame update (called from the ticker's LateUpdate)
    // ---------------------------------------------------------------

    private static void LateTick()
    {
        if (_overlayRoot == null || !_isApplied) return;

        var profile = ReskinProfileManager.currentProfile;
        if (profile == null || !profile.puckIndicatorEnabled)
        {
            _overlayRoot.style.display = DisplayStyle.None;
            return;
        }

        _overlayRoot.style.display = DisplayStyle.Flex;

        // We need a camera for the viewport check. Try the local player's camera first,
        // then fall back to Camera.main. In Puck the "main camera" can be a spectator
        // cam that follows the puck, so we specifically want the player's own camera.
        Camera cam = GetLocalPlayerCamera();
        if (cam == null)
        {
            HideAllArrows();
            return;
        }

        // Find puck
        Puck puck = FindActivePuck();
        if (puck == null || puck.gameObject == null)
        {
            HideAllArrows();
            return;
        }

        Vector3 puckPos = puck.transform.position;
        Vector3 screenPoint = cam.WorldToScreenPoint(puckPos);

        // Check if puck is in front of camera and within screen bounds —
        // if so, the player can see it and we don't need an indicator.
        bool inFront = screenPoint.z > 0f;
        bool withinX = screenPoint.x >= 0f && screenPoint.x <= Screen.width;
        bool withinY = screenPoint.y >= 0f && screenPoint.y <= Screen.height;
        bool onScreen = inFront && withinX && withinY;

        if (onScreen)
        {
            HideAllArrows();
            return;
        }

        // --- Compute the angle from camera center toward the puck ---
        // This is the NHL EA approach: project the puck direction onto the
        // camera's local space, get the yaw angle, and map it to a screen edge.
        Vector3 camPos = cam.transform.position;
        Vector3 camFwd = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        Vector3 camUp = cam.transform.up;

        Vector3 toPuck = (puckPos - camPos).normalized;

        // If the puck is behind the camera, we still want the indicator to
        // point toward it, so we use the raw (not projected) direction.
        float dotFwd = Vector3.Dot(toPuck, camFwd);
        bool behindCamera = dotFwd < 0f;

        // Project onto camera plane for angle calculation
        Vector3 projected;
        if (behindCamera)
        {
            // For behind-camera: use the direction projected onto camera plane,
            // which naturally points opposite (arrow will appear on near edge)
            projected = toPuck;
        }
        else
        {
            // Remove the forward component so we get a pure screen-plane direction
            projected = toPuck - camFwd * dotFwd;
            if (projected.sqrMagnitude < 0.0001f)
            {
                // Puck is directly ahead but off-screen (e.g. way above/below)
                projected = camUp;
            }
            projected.Normalize();
        }

        // Compute yaw angle in camera space (-180..+180, 0 = center)
        float yaw = Mathf.Atan2(
            Vector3.Dot(projected, camRight),
            Vector3.Dot(projected, behindCamera ? -camFwd : camFwd)
        ) * Mathf.Rad2Deg;

        // Absolute offsets for elevation
        float pitch = Mathf.Asin(Mathf.Clamp(
            behindCamera ? -Vector3.Dot(toPuck, camUp) : Vector3.Dot(toPuck, camUp),
            -1f, 1f)) * Mathf.Rad2Deg;

        // --- Map the yaw angle to a screen edge position ---
        float margin = profile.puckIndicatorEdgeMargin;
        float size = profile.puckIndicatorArrowSize;
        Color color = profile.puckIndicatorArrowColor;
        color.a = profile.puckIndicatorOpacity;
        float sw = Screen.width;
        float sh = Screen.height;

        // Use atan-based proportional mapping: the further off-center the angle,
        // the closer to the corner the indicator appears.
        // For a typical ~90° horizontal FOV, ±45° maps to screen edges.
        float angleRangeH = cam.fieldOfView * cam.aspect; // approx horizontal FOV
        float angleRangeV = cam.fieldOfView;

        // Normalized position along each axis (-1 to +1 based on angle / half-fov)
        float normX = Mathf.Clamp(yaw / (angleRangeH * 0.6f), -1f, 1f);
        float normY = Mathf.Clamp(pitch / (angleRangeV * 0.6f), -1f, 1f);

        // Squash toward edges (makes it behave more like NHL)
        normX = Mathf.Sign(normX) * Mathf.Pow(Mathf.Abs(normX), 0.7f);
        normY = Mathf.Sign(normY) * Mathf.Pow(Mathf.Abs(normY), 0.7f);

        // Determine which edge is closest based on angle
        // Compute the angle's dominance: is it more horizontal or vertical?
        float absYaw = Mathf.Abs(yaw);
        float absPitch = Mathf.Abs(pitch);

        // Elevation tilt for the arrow
        float elevationAngle = 0f;
        if (profile.puckIndicatorShowElevation)
        {
            try
            {
                var localPlayer = PlayerManager.Instance?.GetLocalPlayer();
                if (localPlayer?.PlayerBody?.transform != null)
                {
                    float heightDelta = puckPos.y - localPlayer.PlayerBody.transform.position.y;
                    elevationAngle = Mathf.Clamp(heightDelta * 5f, -25f, 25f);
                }
            }
            catch { /* elevation is best-effort */ }
        }

        // Map to a single edge using the angle.
        // Divide the 360° around into 4 sectors centered on each edge.
        // Threshold: if absYaw > absPitch, use left/right; otherwise top/bottom.
        float edgePos; // proportional position along the chosen edge (0..1)

        if (absYaw >= absPitch)
        {
            // Left or right edge
            edgePos = 0.5f - normY * 0.5f; // 0=top, 1=bottom of edge

            if (yaw < 0f)
            {
                // Left
                float x = margin;
                float y = margin + (sh - 2f * margin) * edgePos - size * 0.5f;
                PositionArrow(_leftArrow, x, y, -45f + elevationAngle, color, size);
                _rightArrow.style.display = DisplayStyle.None;
            }
            else
            {
                // Right
                float x = sw - size - margin;
                float y = margin + (sh - 2f * margin) * edgePos - size * 0.5f;
                PositionArrow(_rightArrow, x, y, 45f + elevationAngle, color, size);
                _leftArrow.style.display = DisplayStyle.None;
            }
            _topArrow.style.display = DisplayStyle.None;
            _bottomArrow.style.display = DisplayStyle.None;
        }
        else
        {
            // Top or bottom edge
            edgePos = 0.5f + normX * 0.5f; // 0=left, 1=right of edge

            if (pitch > 0f)
            {
                // Top
                float x = margin + (sw - 2f * margin) * edgePos - size * 0.5f;
                float y = sh - size - margin;
                PositionArrow(_topArrow, x, y, 135f + elevationAngle, color, size);
                _bottomArrow.style.display = DisplayStyle.None;
            }
            else
            {
                // Bottom
                float x = margin + (sw - 2f * margin) * edgePos - size * 0.5f;
                float y = margin;
                PositionArrow(_bottomArrow, x, y, -135f + elevationAngle, color, size);
                _topArrow.style.display = DisplayStyle.None;
            }
            _leftArrow.style.display = DisplayStyle.None;
            _rightArrow.style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Gets the local player's camera. Falls back to Camera.main if unavailable.
    /// In Puck, Camera.main can be a spectator cam that follows the puck,
    /// so we need to find the camera that belongs to the human player.
    /// </summary>
    private static Camera GetLocalPlayerCamera()
    {
        try
        {
            // First try: find the camera on the local player's GameObject
            var localPlayer = PlayerManager.Instance?.GetLocalPlayer();
            if (localPlayer?.gameObject != null)
            {
                var playerCam = localPlayer.gameObject.GetComponentInChildren<Camera>(true);
                if (playerCam != null && playerCam.enabled)
                    return playerCam;
            }

            // Second try: Camera.main
            if (_camera == null)
                _camera = Camera.main;
            return _camera;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.GetLocalPlayerCamera: {ex.Message}");
            return null;
        }
    }

    private static void PositionArrow(VisualElement arrow, float x, float y, float rotation, Color color, float size)
    {
        arrow.style.left = x;
        arrow.style.top = y;
        arrow.style.width = size;
        arrow.style.height = size;
        arrow.style.backgroundColor = new StyleColor(color);
        // Make a diamond shape
        arrow.style.borderTopLeftRadius = new Length(size * 0.1f, LengthUnit.Pixel);
        arrow.style.borderTopRightRadius = new Length(size * 0.1f, LengthUnit.Pixel);
        arrow.style.borderBottomLeftRadius = new Length(size * 0.1f, LengthUnit.Pixel);
        arrow.style.borderBottomRightRadius = new Length(size * 0.1f, LengthUnit.Pixel);
        arrow.style.rotate = new Rotate(rotation);
        // Shift pivot to center for proper rotation
        arrow.style.position = Position.Absolute;
        arrow.style.display = DisplayStyle.Flex;
    }

    private static void HideAllArrows()
    {
        if (_leftArrow != null) _leftArrow.style.display = DisplayStyle.None;
        if (_rightArrow != null) _rightArrow.style.display = DisplayStyle.None;
        if (_topArrow != null) _topArrow.style.display = DisplayStyle.None;
        if (_bottomArrow != null) _bottomArrow.style.display = DisplayStyle.None;
    }

    // ---------------------------------------------------------------
    //  Puck finding
    // ---------------------------------------------------------------

    private static Puck FindActivePuck()
    {
        try
        {
            if (PuckManager.Instance == null) return null;

            List<Puck> pucks = PuckManager.Instance.GetPucks();
            if (pucks == null || pucks.Count == 0) return null;

            // Return the first active, non-null puck
            foreach (Puck p in pucks)
            {
                if (p != null && p.gameObject != null && p.gameObject.activeInHierarchy)
                    return p;
            }

            return null;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"PuckIndicatorSwapper.FindActivePuck: {ex.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------------
    //  Helper MonoBehaviour
    // ---------------------------------------------------------------

    /// <summary>
    /// Lightweight ticker attached to the UIManager GameObject.
    /// Calls LateTick() each LateUpdate to reposition the puck indicator arrows.
    /// </summary>
    internal class PuckIndicatorTicker : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                LateTick();
            }
            catch (Exception ex)
            {
                Plugin.LogError($"PuckIndicatorSwapper.Tick: {ex.Message}");
            }
        }
    }
}
