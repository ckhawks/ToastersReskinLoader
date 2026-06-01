// Free-fly "spectator" camera while in position select.
//
// Vanilla shows position-select players a fixed bench camera
// (CameraType.BluePositionSelection / RedPositionSelection) — you've joined a
// team but can only stare at the rink from one spot until you claim a slot.
// This lets you fly that camera around like a spectator while you decide.
//
// It is purely client-local: the position-select cameras are static scene
// objects whose transform the server neither drives nor validates (the
// spectator free-fly the game itself ships, SpectatorCamera.OnTick, also only
// mutates a local transform and never networks position). So moving the bench
// camera only changes what *you* see — no desync, nothing server-visible.
//
// Interaction (toggle, not hold): right-click toggles free-look on/off. While
// on, the cursor is locked/hidden and you fly with the same controls as the
// spectator camera (move/strafe + Jump/Slide for up/down, Sprint to go faster,
// mouse to look). Right-click again — or Esc, or claiming/leaving the phase —
// returns the cursor so the clickable position markers work again. Because the
// markers are projected from world space every frame (UIPositionSelect), they
// stay glued to their rink slots no matter where you fly.
//
// We drive the bench scene camera directly rather than relying on
// SpectatorCamera.OnTick (which never runs for it — wrong camera type, and it
// bails unless the cursor is freed). Reading the Input System devices straight
// also sidesteps the IsMouseRequired movement gate the vanilla paths use.
//
// ── FUTURE TODO: ToasterCameras compatibility ──────────────────────────────
// The sibling ToasterCameras mod adds spectator-cam modes (watch puck, third
// person, become puck, static positions, cinematic smoothing, grid tracking).
// None of them work in position select today, and two things block it:
//
//   1. ToasterCameras only ever drives a *SpectatorCamera*, via its Harmony
//      patch on SpectatorCamera.OnTick, keyed off `Plugin.spectatorCamera`.
//      In position select the active camera is the static bench camera
//      (CameraType.BluePositionSelection / RedPositionSelection), a different
//      type its patch never touches — and the game only spawns a real
//      SpectatorCamera on PlayerPhase.Spectate (StandardGameMode.cs).
//   2. Even with a SpectatorCamera active, ToasterCameras deliberately bails
//      whenever the local player is on Blue/Red (PatchPlayerCamera.cs, the
//      "if localPlayer.Team == Blue/Red -> unparent + return true" guard).
//      A position-select player is on Blue/Red by definition, so it disables
//      itself in exactly this scenario.
//
// To make them compose (a coordinated change in BOTH mods — TRL can't do it
// alone):
//   * Camera: drive a real SpectatorCamera here instead of the bench cam when
//     one is available. A player who joined into spectator may still own a live
//     SpectatorCamera during position select — nothing despawns it between
//     Spectate and Play (only Server_SpawnCharacter does, on entering Play). If
//     present, switch to it with CameraManager.SetActiveCamera(Spectator,
//     localId) on enter and restore the bench cam on exit; fall back to the
//     bench-cam path below when there isn't one. (Client-instantiating one from
//     the prefab is possible but fragile — unspawned NetworkBehaviour,
//     IsOwner == false.)
//   * Handshake: expose a public flag (e.g. ToasterReskinLoaderAPI
//     .PositionSelectFreeLookActive) and have ToasterCameras relax its team
//     guard to "bail if Blue/Red AND not(TRL free-look active)". Do it soft /
//     reflection-based, mirroring the existing TRLBridge pattern, so neither
//     mod hard-depends on the other.
// ───────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal sealed class PositionSelectFreeLook : MonoBehaviour
{
    // Mirror SpectatorCamera's defaults so the fly feel matches the real
    // spectator camera (movementSpeed 5, positionSmoothTime 0.25, lookSmoothing
    // 10, pitch clamp ±89).
    private const float MovementSpeed = 5f;
    private const float PositionSmoothTime = 0.25f;
    private const float LookSmoothing = 10f;
    private const float PitchMin = -89f;
    private const float PitchMax = 89f;

    private bool _active;
    // Whether to show the "right-click to enter" hint this frame: we're in
    // position select with the feature on, but not currently flying. Computed
    // in Tick() so OnGUI (which can fire several times per frame) stays cheap.
    private bool _showEnterPrompt;
    private BaseCamera _cam;

    // Cached original transform of the bench camera so we can put it back
    // exactly — these are shared scene singletons reused for every future
    // position select, so leaving one displaced would be a visible bug.
    private Vector3 _origLocalPos;
    private Quaternion _origLocalRot;

    // Free-fly state (same shape as SpectatorCamera's private fields).
    private Vector3 _position;
    private Vector3 _positionVelocity;
    private float _pitch, _yaw, _targetPitch, _targetYaw;

    public static void AttachTo(GameObject go)
    {
        if (go.GetComponent<PositionSelectFreeLook>() == null)
            go.AddComponent<PositionSelectFreeLook>();
    }

    private void Update()
    {
        try { Tick(); }
        catch (Exception e)
        {
            Plugin.LogWarning($"[QoL] position-select free-look tick failed: {e.Message}");
            if (_active) Exit();
        }
    }

    private void Tick()
    {
        _showEnterPrompt = false;

        bool enabled = QoLRunner.Instance?.Config?.enablePositionSelectFreeLook ?? false;

        // Any of these means we should not be in (or stay in) free-look.
        if (!enabled || !InPositionSelect() || IsBlockingUIOpen())
        {
            if (_active) Exit();
            return;
        }

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // Esc is an always-available escape hatch back to the cursor.
        if (_active && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Exit();
            return;
        }

        // Right-click toggles the mode.
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            if (_active) Exit();
            else Enter();
            return;
        }

        if (_active)
        {
            DriveCamera(Time.deltaTime);
            return;
        }

        // In position select, feature on, not flying — advertise the toggle.
        _showEnterPrompt = true;
    }

    private void Enter()
    {
        var cam = CameraManager.GetActiveCamera();
        if (cam == null) return; // not registered yet; try again next frame

        _cam = cam;
        var t = cam.transform;
        _origLocalPos = t.localPosition;
        _origLocalRot = t.localRotation;

        _position = t.position;
        _positionVelocity = Vector3.zero;
        var euler = t.rotation.eulerAngles;
        _pitch = _targetPitch = NormalizePitch(euler.x);
        _yaw = _targetYaw = euler.y;

        _active = true;
        ApplyCursorLock(true);
        SetMarkersHidden(true);
    }

    private void Exit()
    {
        _active = false;
        ApplyCursorLock(false);
        SetMarkersHidden(false);

        if (_cam != null)
        {
            var t = _cam.transform;
            t.localPosition = _origLocalPos;
            t.localRotation = _origLocalRot;
            _cam = null;
        }
    }

    private void DriveCamera(float dt)
    {
        // If the active camera changed under us (e.g. swapped teams Blue<->Red,
        // which switches bench cameras), rebind to the new one cleanly.
        var current = CameraManager.GetActiveCamera();
        if (current == null) { Exit(); return; }
        if (current != _cam)
        {
            Exit();
            Enter();
            if (!_active) return;
        }

        // Reassert the cursor lock every frame so a stray game UI-state event
        // can't quietly hand the cursor back mid-flight.
        ApplyCursorLock(true);

        var t = _cam.transform;

        float strafe = (InputManager.TurnRightAction.IsPressed() ? 1f : 0f)
                     + (InputManager.TurnLeftAction.IsPressed() ? -1f : 0f);
        float upDown = InputManager.JumpAction.IsPressed() ? 1f
                     : (InputManager.SlideAction.IsPressed() ? -1f : 0f);
        float forward = (InputManager.MoveForwardAction.IsPressed() ? 1f : 0f)
                      + (InputManager.MoveBackwardAction.IsPressed() ? -1f : 0f);
        bool sprint = InputManager.SprintAction.IsPressed();
        Vector2 look = InputManager.StickAction.ReadValue<Vector2>();

        float speed = sprint ? MovementSpeed * 2f : MovementSpeed;
        _position += t.right * strafe * speed * dt;
        _position += t.up * upDown * speed * dt;
        _position += t.forward * forward * speed * dt;
        t.position = Vector3.SmoothDamp(t.position, _position, ref _positionVelocity,
            PositionSmoothTime, float.PositiveInfinity, dt);

        _targetPitch -= look.y * SettingsManager.LookSensitivity;
        _targetYaw += look.x * SettingsManager.LookSensitivity;
        _targetPitch = Mathf.Clamp(_targetPitch, PitchMin, PitchMax);
        _pitch = Mathf.Lerp(_pitch, _targetPitch, LookSmoothing * dt);
        _yaw = Mathf.Lerp(_yaw, _targetYaw, LookSmoothing * dt);
        t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private static void ApplyCursorLock(bool locked)
    {
        if (locked)
        {
            UnityEngine.Cursor.visible = false;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }
    }

    // Hide/show the clickable position circles. We poke the PositionsView
    // container's display directly rather than calling UIView.Hide(), so we
    // don't flip the view's logical IsVisible (which would ripple into the
    // game's mouse-required / interacting-views bookkeeping). On restore we set
    // it to whatever vanilla currently intends (Flex while the view is visible,
    // None otherwise) so we never resurrect the circles after the phase ends.
    private static void SetMarkersHidden(bool hidden)
    {
        try
        {
            var ps = MonoBehaviourSingleton<UIManager>.Instance?.PositionSelect;
            if (ps == null || ps.View == null) return;
            ps.View.style.display = hidden
                ? DisplayStyle.None
                : (ps.IsVisible ? DisplayStyle.Flex : DisplayStyle.None);
        }
        catch { }
    }

    private static bool InPositionSelect()
    {
        var pm = PlayerManager.Instance;
        var local = pm != null ? pm.GetLocalPlayer() : null;
        return local != null && local.Phase == PlayerPhase.PositionSelect;
    }

    // Free-look must yield to anything that needs the cursor or text input:
    // the pause menu (Esc), chat, the reskin menu, and the dev console.
    private static bool IsBlockingUIOpen()
    {
        try
        {
            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui != null)
            {
                if (ui.PauseMenu != null && ui.PauseMenu.IsVisible) return true;
                if (ui.Chat != null && ui.Chat.IsFocused) return true;
            }
        }
        catch { }

        var root = ToasterReskinLoader.ui.ReskinMenu.rootContainer;
        if (root != null && root.style.display == UnityEngine.UIElements.DisplayStyle.Flex) return true;

        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return true;

        return false;
    }

    // Unity euler pitch comes back in [0,360); fold the top half to negative so
    // the clamp around level (0) behaves.
    private static float NormalizePitch(float pitch) => pitch > 180f ? pitch - 360f : pitch;

    private void OnGUI()
    {
        if (_active)
            DrawBar("FREE LOOK  —  Right-click to pick position  ·  WASD move · Space/Ctrl up·down · Shift sprint", 760f);
        else if (_showEnterPrompt)
            DrawBar("Right-click to free-look around the rink", 360f);
    }

    private static void DrawBar(string msg, float w)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = Color.white;

        const float h = 28f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - 24f;

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.Label(new Rect(x, y, w, h), msg, style);
    }

    private void OnDisable()
    {
        if (_active) Exit();
    }
}
