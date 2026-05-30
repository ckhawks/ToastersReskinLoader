using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.swappers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader;

public enum PuckPreviewMode
{
    Row,
    Carousel,
    Drop,
}

/// <summary>
/// Spawns display-only pucks in the locker room so the user can preview the skins in
/// their puck-randomizer list. These are plain GameObjects (MeshFilter + MeshRenderer) —
/// NOT the networked Puck — so they carry no physics or netcode. The mesh + material are
/// borrowed from the loaded puck prefab (works before ever entering a game), with the
/// in-game spawn hook as a backup source.
/// </summary>
public static class PuckPreview
{
    // ── Borrowed puck assets ────────────────────────────────────────
    private static Mesh _puckMesh;
    private static Material _puckMaterialTemplate;
    private static float _puckScale = 1f;

    // ── Live preview state ──────────────────────────────────────────
    private static GameObject _root;
    private static PuckPreviewDriver _driver;
    private static Camera _camera;
    private static readonly List<Material> _ownedMaterials = new();

    public static PuckPreviewMode Mode { get; private set; } = PuckPreviewMode.Row;
    public static bool IsShown => _root != null;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Captures the puck mesh/material from a live in-game puck (backup source).</summary>
    public static void TryCaptureAssets(Puck puck)
    {
        if (_puckMesh != null && _puckMaterialTemplate != null) return;
        if (puck == null) return;
        var meshTransform = puck.transform.Find("puck")?.Find("Puck");
        CaptureFromTransform(meshTransform);
    }

    /// <summary>Shows the preview for the current puck-randomizer list. Main menu only.</summary>
    public static void Show()
    {
        if (!ChangingRoomHelper.IsInMainMenu()) return;
        if (!EnsureAssets())
        {
            Plugin.LogDebug("[PuckPreview] Could not resolve a puck mesh yet — skipping preview.");
            return;
        }

        if (_root == null)
        {
            Transform anchor = FindCameraAnchor();
            if (anchor == null)
            {
                Plugin.LogDebug("[PuckPreview] No locker room camera found — skipping preview.");
                return;
            }

            _root = new GameObject("TRL_PuckPreview");
            _root.transform.SetParent(anchor, false);
            _root.transform.localRotation = Quaternion.identity;

            // Place the rig in the visible right portion of the screen (the reskin menu
            // overlays the left ~45%). Viewport-based so it's correct regardless of FOV.
            if (_camera != null)
                _root.transform.position = _camera.ViewportToWorldPoint(
                    new Vector3(PreviewViewportX, PreviewViewportY, PreviewDepth));
            else
                _root.transform.localPosition = new Vector3(0.3f, -0.1f, PreviewDepth);

            _driver = _root.AddComponent<PuckPreviewDriver>();
        }

        BuildDisplayPucks();
        _driver.SetMode(Mode);
    }

    /// <summary>Tears down the preview pucks and their materials.</summary>
    public static void Hide()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
            _driver = null;
        }
        foreach (var mat in _ownedMaterials)
            if (mat != null) Object.Destroy(mat);
        _ownedMaterials.Clear();
    }

    /// <summary>Rebuilds the display pucks (call after the active list changes).</summary>
    public static void Refresh()
    {
        if (_root == null) return;
        BuildDisplayPucks();
        _driver.SetMode(Mode);
    }

    public static void SetMode(PuckPreviewMode mode)
    {
        Mode = mode;
        _driver?.SetMode(mode);
    }

    // ── Asset resolution ────────────────────────────────────────────

    private static bool EnsureAssets()
    {
        if (_puckMesh != null && _puckMaterialTemplate != null) return true;

        // Primary source: the loaded puck prefab. It's referenced by the netcode prefab
        // list at startup, so it's in memory even in the main menu (no game required).
        foreach (var puck in Resources.FindObjectsOfTypeAll<Puck>())
        {
            var meshTransform = puck.transform.Find("puck")?.Find("Puck");
            if (CaptureFromTransform(meshTransform)) return true;
        }

        return _puckMesh != null && _puckMaterialTemplate != null;
    }

    private static bool CaptureFromTransform(Transform meshTransform)
    {
        if (meshTransform == null) return false;
        var mf = meshTransform.GetComponent<MeshFilter>();
        var mr = meshTransform.GetComponent<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null)
            return false;

        _puckMesh = mf.sharedMesh;
        _puckMaterialTemplate = mr.sharedMaterial;

        // Normalize size: scale so the puck's largest dimension matches DisplayDiameter,
        // regardless of the mesh's authored units.
        Vector3 size = _puckMesh.bounds.size;
        float maxExtent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        _puckScale = maxExtent > 0.0001f ? DisplayDiameter / maxExtent : 1f;

        Plugin.LogDebug($"[PuckPreview] Captured puck mesh '{_puckMesh.name}' (scale {_puckScale:0.000}).");
        return true;
    }

    private static Transform FindCameraAnchor()
    {
        var cam = Object.FindObjectsByType<LockerRoomCamera>(FindObjectsSortMode.None).FirstOrDefault();
        if (cam == null) return null;
        _camera = cam.GetComponentInChildren<Camera>(true);
        return _camera != null ? _camera.transform : cam.transform;
    }

    // ── Display puck construction ───────────────────────────────────

    private static void BuildDisplayPucks()
    {
        // Clear previous display pucks + their materials.
        var driverItems = new List<Transform>();
        for (int i = _root.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(_root.transform.GetChild(i).gameObject);
        foreach (var mat in _ownedMaterials)
            if (mat != null) Object.Destroy(mat);
        _ownedMaterials.Clear();

        var entries = ReskinProfileManager.currentProfile.puckList ?? new List<ReskinRegistry.ReskinEntry>();

        // Nothing selected → show a single default puck so the user always sees something.
        var toShow = entries.Count > 0
            ? entries
            : new List<ReskinRegistry.ReskinEntry> { new ReskinRegistry.ReskinEntry { Name = "Default", Path = null, Type = "puck" } };

        foreach (var entry in toShow)
        {
            var go = new GameObject($"PreviewPuck_{entry?.Name ?? "?"}");
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = Vector3.one * _puckScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _puckMesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(_puckMaterialTemplate);
            _ownedMaterials.Add(mat);

            // null path = the vanilla default puck → leave the template's textures untouched.
            if (entry != null && entry.Path != null)
            {
                var tex = TextureManager.GetTexture(entry);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    var bump = PuckSwapper.GetCleanBumpMap();
                    if (bump != null) mat.SetTexture("_BumpMap", bump);
                }
            }
            mr.material = mat;

            driverItems.Add(go.transform);
        }

        _driver.SetItems(driverItems);
    }

    // ── Tunable framing/layout constants ────────────────────────────
    // Viewport placement: the reskin menu overlays the left ~45%, so bias right.
    internal const float PreviewViewportX = 0.72f; // 0 = left edge, 1 = right edge
    internal const float PreviewViewportY = 0.46f; // 0 = bottom, 1 = top
    internal const float PreviewDepth = 1.1f;      // metres in front of the camera
    internal const float DisplayDiameter = 0.26f;  // apparent puck diameter
    internal const float RowSpacing = 0.34f;
}

/// <summary>Per-frame animator for the preview pucks. Lives on the preview root GameObject.</summary>
public class PuckPreviewDriver : MonoBehaviour
{
    private class Item
    {
        public Transform Transform;
        public Vector3 TumbleAxis;
        public float DropPos;
        public float DropVel;
        public float RestTime;
        public float StartDelay;
    }

    private readonly List<Item> _items = new();
    private PuckPreviewMode _mode = PuckPreviewMode.Row;
    private float _time;

    // Layout bounds (keep big lists framed on screen)
    private const float MaxRowWidth = 1.5f;
    private const float MinCarouselRadius = 0.42f;
    private const float MaxCarouselRadius = 0.8f;
    // Carousel
    private const float CarouselDegPerSec = 28f;
    // Tumble (Row / Carousel / Drop spin)
    private const float TumbleDegPerSec = 55f;
    // Drop physics (local units)
    private const float Gravity = 3.2f;
    private const float DropTop = 0.55f;
    private const float DropFloor = -0.38f;
    private const float Restitution = 0.55f;
    private const float SettleSpeed = 0.45f;
    private const float RestDuration = 1.1f;

    public void SetItems(List<Transform> transforms)
    {
        _items.Clear();
        for (int i = 0; i < transforms.Count; i++)
        {
            _items.Add(new Item
            {
                Transform = transforms[i],
                // Slight per-index variation so they don't tumble in lockstep.
                TumbleAxis = new Vector3(1f, 0.25f + 0.12f * (i % 3), 0.18f * ((i % 2 == 0) ? 1f : -1f)).normalized,
            });
        }
        ResetDropState();
    }

    public void SetMode(PuckPreviewMode mode)
    {
        _mode = mode;
        if (mode == PuckPreviewMode.Drop) ResetDropState();
    }

    // Row/Drop spacing, compressed so a long list still fits the framed width.
    private static float EffectiveSpacing(int n) =>
        n > 1 ? Mathf.Min(PuckPreview.RowSpacing, MaxRowWidth / (n - 1)) : 0f;

    private void ResetDropState()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].DropPos = DropTop;
            _items[i].DropVel = 0f;
            _items[i].RestTime = 0f;
            _items[i].StartDelay = i * 0.22f;
        }
        _time = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _time += dt;
        int n = _items.Count;
        if (n == 0) return;

        switch (_mode)
        {
            case PuckPreviewMode.Row:
            {
                float spacing = EffectiveSpacing(n);
                float totalWidth = (n - 1) * spacing;
                for (int i = 0; i < n; i++)
                {
                    var it = _items[i];
                    if (it.Transform == null) continue;
                    float x = i * spacing - totalWidth / 2f;
                    it.Transform.localPosition = new Vector3(x, 0f, 0f);
                    it.Transform.Rotate(it.TumbleAxis, TumbleDegPerSec * dt, Space.Self);
                }
                break;
            }
            case PuckPreviewMode.Carousel:
            {
                // Clamp so a big list doesn't balloon the circle into the camera.
                float radius = Mathf.Clamp(n * PuckPreview.RowSpacing / (2f * Mathf.PI),
                    MinCarouselRadius, MaxCarouselRadius);
                float baseAngle = _time * CarouselDegPerSec * Mathf.Deg2Rad;
                for (int i = 0; i < n; i++)
                {
                    var it = _items[i];
                    if (it.Transform == null) continue;
                    float a = baseAngle + i * (2f * Mathf.PI / n);
                    // Circle centred on the root: nearer pucks (−z) read larger, farther (+z) smaller.
                    it.Transform.localPosition = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                    it.Transform.Rotate(it.TumbleAxis, TumbleDegPerSec * 0.6f * dt, Space.Self);
                }
                break;
            }
            case PuckPreviewMode.Drop:
            {
                float spacing = EffectiveSpacing(n);
                float totalWidth = (n - 1) * spacing;
                for (int i = 0; i < n; i++)
                {
                    var it = _items[i];
                    if (it.Transform == null) continue;
                    float x = i * spacing - totalWidth / 2f;

                    if (_time >= it.StartDelay)
                    {
                        it.DropVel += Gravity * dt;
                        it.DropPos -= it.DropVel * dt;
                        if (it.DropPos <= DropFloor)
                        {
                            it.DropPos = DropFloor;
                            it.DropVel = -it.DropVel * Restitution;
                            if (Mathf.Abs(it.DropVel) < SettleSpeed)
                            {
                                it.DropVel = 0f;
                                it.RestTime += dt;
                                if (it.RestTime >= RestDuration)
                                {
                                    it.DropPos = DropTop;
                                    it.RestTime = 0f;
                                }
                            }
                        }
                    }

                    it.Transform.localPosition = new Vector3(x, it.DropPos, 0f);
                    it.Transform.Rotate(it.TumbleAxis, TumbleDegPerSec * dt, Space.Self);
                }
                break;
            }
        }
    }
}
