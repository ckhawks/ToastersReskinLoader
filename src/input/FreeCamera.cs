// Global free-fly "noclip" camera, toggled with F8 anywhere in the game.
//
// Unlike PositionSelectFreeLook (which flies the static bench camera during
// position select only), this works in every context — locker room, warmup,
// live play, replays, spectate — so you can orbit your reskinned jerseys and
// models from any angle and line up screenshots.
//
// Approach differs from PositionSelectFreeLook on purpose. That one drives the
// active camera's transform directly, which is safe only because the bench cam
// is a static scene object nobody else moves. In live play the active camera is
// the PlayerCamera, driven every frame by PlayerCameraController.LateUpdate — so
// driving its transform would just get overwritten and snap back. Instead we
// spawn our OWN camera on top (higher depth) and park the game's camera + audio
// listener while flying, then restore them on exit. It never touches anything
// networked, so — like the sibling free-look — it's purely client-local and can
// cause no desync.
//
// Controls: F8 toggle · WASD move · Space / Left-Ctrl up-down · Left-Shift boost
// · mouse to look. Opening a blocking UI (pause, chat, reskin menu, dev console)
// releases the cursor and pauses the fly until you close it again.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using ToasterReskinLoader.core;
using ToasterReskinLoader.diagnostics;

namespace ToasterReskinLoader.input;

internal sealed class FreeCamera : MonoBehaviour
{
    private const float MoveSpeed = 6f;
    private const float BoostMult = 4f;
    private const float LookSens = 0.12f;
    private const float PitchMin = -89f;
    private const float PitchMax = 89f;
    private const float FovMin = 10f;
    private const float FovMax = 100f;
    private const float ZoomStep = 4f; // FOV degrees per scroll notch

    private bool _active;
    private Camera _flyCam;
    private float _pitch, _yaw;

    // Game components we've parked, so we can restore them exactly on exit even
    // if the active camera swapped (team change, phase change) while flying.
    private readonly List<Camera> _parkedCams = new List<Camera>();
    private readonly List<AudioListener> _parkedAudio = new List<AudioListener>();

    public static void AttachTo(GameObject go)
    {
        if (go.GetComponent<FreeCamera>() == null)
            go.AddComponent<FreeCamera>();
    }

    private void Update()
    {
        try { Tick(); }
        catch (System.Exception e)
        {
            Plugin.LogWarning($"[QoL] free camera tick failed: {e.Message}");
            if (_active) Exit();
        }
    }

    private void Tick()
    {
        bool enabled = Settings.Current?.enableFreeCamera ?? false;
        if (!enabled)
        {
            if (_active) Exit();
            return;
        }

        var kb = Keyboard.current;
        Key toggleKey = Settings.Current?.freeCameraKey ?? Key.F8;
        if (!KeyRebinder.IsCapturing && kb != null && toggleKey != Key.None
            && kb[toggleKey].wasPressedThisFrame)
        {
            if (_active) Exit();
            else Enter();
            return;
        }

        if (!_active) return;

        // Lost our camera somehow — bail cleanly.
        if (_flyCam == null) { Exit(); return; }

        // Keep whatever the game currently considers "active" parked underneath us.
        ParkActiveGameCamera();

        // Yield to any UI that needs the cursor; hold position until it closes.
        if (IsBlockingUIOpen())
        {
            ApplyCursorLock(false);
            return;
        }

        ApplyCursorLock(true);
        DriveCamera(Time.unscaledDeltaTime);
    }

    private void Enter()
    {
        var active = CameraManager.GetActiveCamera();
        Camera src = (active != null && active.UnityCamera != null) ? active.UnityCamera : Camera.main;
        if (src == null) return; // no camera yet; try again on next F8

        var go = new GameObject("ToasterFreeCamera");
        _flyCam = go.AddComponent<Camera>();
        _flyCam.CopyFrom(src);
        _flyCam.depth = src.depth + 10f; // render on top of the parked game camera
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);

        var euler = go.transform.rotation.eulerAngles;
        _pitch = NormalizePitch(euler.x);
        _yaw = euler.y;

        _active = true;
        ParkActiveGameCamera();
        ApplyCursorLock(true);
    }

    private void Exit()
    {
        _active = false;
        ApplyCursorLock(false);

        if (_flyCam != null) { Destroy(_flyCam.gameObject); _flyCam = null; }

        foreach (var c in _parkedCams) if (c != null) c.enabled = true;
        foreach (var a in _parkedAudio) if (a != null) a.enabled = true;
        _parkedCams.Clear();
        _parkedAudio.Clear();
    }

    // Disable the game's currently-active camera (and its audio listener) so only
    // our fly camera renders. Re-checked every frame so a mid-flight camera swap
    // gets parked too; all parked components are restored in Exit().
    private void ParkActiveGameCamera()
    {
        var active = CameraManager.GetActiveCamera();
        if (active == null) return;

        var cam = active.UnityCamera;
        if (cam != null && cam != _flyCam && cam.enabled)
        {
            cam.enabled = false;
            _parkedCams.Add(cam);
        }

        var audio = active.AudioListener;
        if (audio != null && audio.enabled)
        {
            audio.enabled = false;
            _parkedAudio.Add(audio);
        }
    }

    private void DriveCamera(float dt)
    {
        Transform t = _flyCam.transform;

        var kb = Keyboard.current;
        if (kb != null)
        {
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += t.forward;
            if (kb.sKey.isPressed) dir -= t.forward;
            if (kb.dKey.isPressed) dir += t.right;
            if (kb.aKey.isPressed) dir -= t.right;
            if (kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.leftCtrlKey.isPressed) dir -= Vector3.up;

            float speed = MoveSpeed * (kb.leftShiftKey.isPressed ? BoostMult : 1f);
            t.position += dir * (speed * dt);
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * LookSens;
            _yaw += d.x;
            _pitch = Mathf.Clamp(_pitch - d.y, PitchMin, PitchMax);
            t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // Scroll wheel zooms by narrowing/widening the field of view. The
            // Input System reports scroll magnitude inconsistently across
            // platforms (1 vs 120 per notch), so step by sign for predictability.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _flyCam.fieldOfView = Mathf.Clamp(_flyCam.fieldOfView - Mathf.Sign(scroll) * ZoomStep, FovMin, FovMax);
        }
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

    // Mirrors PositionSelectFreeLook: yield to anything that needs the cursor or
    // text input so the fly cam doesn't fight the pause menu, chat, reskin menu
    // or dev console.
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

        var root = ToasterReskinLoader.ui.ReskinManagerMenu.rootContainer;
        if (root != null && root.style.display == UnityEngine.UIElements.DisplayStyle.Flex) return true;

        if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return true;

        return false;
    }

    private static float NormalizePitch(float pitch) => pitch > 180f ? pitch - 360f : pitch;

    private void OnGUI()
    {
        if (!_active) return;
        if (Settings.Current?.freeCameraHideHint ?? false) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
        };
        const float w = 720f, h = 26f;
        var rect = new Rect((Screen.width - w) * 0.5f, 8f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        Key toggleKey = Settings.Current?.freeCameraKey ?? Key.F8;
        GUI.Label(rect, $"FREE CAMERA  —  {toggleKey} to exit  ·  WASD move · Space/Ctrl up·down · Shift boost · mouse look · scroll zoom", style);
    }

    private void OnDestroy() { if (_active) Exit(); }
}
