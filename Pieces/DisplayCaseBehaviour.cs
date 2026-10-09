using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Display case: the chest only opens from the store side (where the sliding glass door is), that door slides open
/// while the chest is in use, and what's inside lies on the jute, one spot per slot. "In use" and the contents come
/// from the container's synced ZDO, so every client shows its own copy.
/// </summary>
public class DisplayCaseBehaviour : MonoBehaviour
{
    /// <summary>How far the door slides along the case's length (Unity -Z): to the right, seen from the door. Set per piece.</summary>
    public float m_slideDistance = 1.2f;
    /// <summary>The jute floor along the case's length (local Z), set per piece; slots split it evenly.</summary>
    public float m_floorZ0 = -2.224f;
    public float m_floorZ1 = 1.821f;

    private const float SlideSeconds = 0.45f;

    /// <summary>Local X of the case's middle: the door side is below it (see Forge/DisplayCase/README.md).</summary>
    private const float MiddleX = 0.36f;

    private const float FloorY = 1.038f;
    private const float FloorX = 0.365f;
    /// <summary>Largest an item may be across the case, along its slot (fraction of the slot) and in height.</summary>
    private const float MaxAcross = 1.15f;
    private const float SlotFill = 0.85f;
    private const float MaxHeight = 0.5f;
    private const float RefreshSeconds = 0.5f;

    private static readonly int InUseKey = "InUse".GetStableHashCode();

    private ZNetView? _nview;
    private Container? _container;
    private readonly List<(Transform Part, Vector3 Closed)> _door = new();
    private float _open;

    private Transform? _shelf;
    private string _shownContents = "";
    private float _nextRefresh;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _container = GetComponent<Container>();
        // The .glb's "GlassPanel" mesh (crystal pane and its wooden frame) becomes renderers named glb{i}_GlassPanel.
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.name.EndsWith("_GlassPanel"))
                _door.Add((renderer.transform, renderer.transform.localPosition));
    }

    private void Update()
    {
        if (_nview == null || !_nview.IsValid())
            return;

        AnimateDoor();
        if (Time.time >= _nextRefresh)
        {
            _nextRefresh = Time.time + RefreshSeconds;
            RefreshShelf();
        }
    }

    /// <summary>True when <paramref name="viewer"/> stands on the door (store) side of the case.</summary>
    public bool IsStoreSide(Component viewer) =>
        transform.InverseTransformPoint(viewer.transform.position).x < MiddleX;

    private void AnimateDoor()
    {
        if (_door.Count == 0)
            return;

        var target = _nview!.GetZDO().GetInt(InUseKey) == 1 ? 1f : 0f;
        if (Mathf.Approximately(_open, target))
            return;

        _open = Mathf.MoveTowards(_open, target, Time.deltaTime / SlideSeconds);
        var offset = Vector3.back * (m_slideDistance * Mathf.SmoothStep(0f, 1f, _open));
        foreach (var (part, closed) in _door)
            part.localPosition = closed + offset;
    }

    // ---- items on display ----

    private void RefreshShelf()
    {
        if (_container == null || ZNet.instance == null || ZNet.instance.IsDedicated() || _container.GetInventory() is not { } inventory)
            return;

        var contents = Describe(inventory);
        if (contents == _shownContents)
            return;
        _shownContents = contents;

        if (_shelf != null)
            Destroy(_shelf.gameObject);
        _shelf = new GameObject("drake_display_items").transform;
        _shelf.SetParent(transform, false);

        var slots = Mathf.Max(1, inventory.GetWidth());
        var slotLength = (m_floorZ1 - m_floorZ0) / slots;
        foreach (var item in inventory.GetAllItems())
        {
            if (item?.m_dropPrefab == null || item.m_gridPos.y != 0 || item.m_gridPos.x >= slots)
                continue;
            // Slot 0 is the stocker's left (they stand at the door, facing +X): the +Z end.
            var z = m_floorZ1 - (item.m_gridPos.x + 0.5f) * slotLength;
            Show(item.m_dropPrefab, new Vector3(FloorX, FloorY, z), slotLength * SlotFill);
        }
    }

    private static string Describe(Inventory inventory)
    {
        var text = new StringBuilder();
        foreach (var item in inventory.GetAllItems())
            text.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|').Append(item.m_gridPos.x).Append(',')
                .Append(item.m_gridPos.y).Append('|').Append(item.m_quality).Append('|').Append(item.m_variant).Append(';');
        return text.ToString();
    }

    /// <summary>A still copy of the item's dropped model, lying flat (thinnest side down, longest across the case), shrunk to fit its slot.</summary>
    private void Show(GameObject prefab, Vector3 slotCentre, float slotLength)
    {
        var model = StillCopy(prefab);
        if (model == null)
            return;

        if (!LocalBounds(model.transform, out var bounds))
        {
            Destroy(model);
            return;
        }

        // Axes of the model, longest to shortest; lay it down: longest along X (across), middle along Z (the slot), shortest up.
        var axes = new List<(Vector3 Dir, float Size)> { (Vector3.right, bounds.size.x), (Vector3.up, bounds.size.y), (Vector3.forward, bounds.size.z) };
        axes.Sort((a, b) => b.Size.CompareTo(a.Size));
        var lay = Quaternion.Inverse(Quaternion.LookRotation(axes[1].Dir, axes[2].Dir));
        var across = axes[0].Size;
        var along = axes[1].Size;
        var height = axes[2].Size;
        var scale = Mathf.Min(1f, MaxAcross / Mathf.Max(across, 0.001f), slotLength / Mathf.Max(along, 0.001f), MaxHeight / Mathf.Max(height, 0.001f));

        var t = model.transform;
        t.SetParent(_shelf, false);
        t.localRotation = lay;
        t.localScale = Vector3.one * scale;
        // Centre the model's bounds on the slot, resting on the cloth.
        var centre = lay * (bounds.center * scale);
        t.localPosition = slotCentre - centre + Vector3.up * (height * scale / 2f + 0.003f);
        model.SetActive(true);
    }

    /// <summary>
    /// The item's prefab copied while inactive (so none of its scripts wake up), stripped of scripts, physics and
    /// sound and particles (the ground sparkle): only meshes and lights remain. Returned inactive.
    /// </summary>
    private static GameObject? StillCopy(GameObject prefab)
    {
        var wasActive = prefab.activeSelf;
        prefab.SetActive(false);
        GameObject copy;
        try
        {
            copy = Instantiate(prefab);
        }
        finally
        {
            prefab.SetActive(wasActive);
        }

        copy.name = prefab.name + "_display";
        var behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);
        for (var i = behaviours.Length - 1; i >= 0; i--)
            if (behaviours[i] is not ZNetView)
                DestroyImmediate(behaviours[i]);
        foreach (var nview in copy.GetComponentsInChildren<ZNetView>(true))
            DestroyImmediate(nview);
        foreach (var joint in copy.GetComponentsInChildren<Joint>(true))
            DestroyImmediate(joint);
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(body);
        foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(collider);
        foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true))
            DestroyImmediate(audio);
        // Dropped items sparkle so they're easy to spot on the ground; not on a shelf. Renderer first: it depends on the system.
        foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.GetComponent<ParticleSystemRenderer>() is { } renderer)
                DestroyImmediate(renderer);
            DestroyImmediate(particles);
        }
        return copy;
    }

    /// <summary>Bounds of the copy's meshes in its own root space (renderer bounds are empty while it's inactive).</summary>
    private static bool LocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        var any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh? mesh = renderer switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                MeshRenderer => renderer.GetComponent<MeshFilter>()?.sharedMesh,
                _ => null
            };
            if (mesh == null)
                continue;
            var matrix = toRoot * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }
        return any;
    }

    private void OnDestroy()
    {
        if (_shelf != null)
            Destroy(_shelf.gameObject);
    }
}
