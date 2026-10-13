using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Display case (and display mat): the chest only opens from the store side (where the sliding glass door is), that door slides open
/// while the chest is in use, and what's inside lies on the jute, one spot per slot. "In use" and the contents come
/// from the container's synced ZDO, so every client shows its own copy. A display mat has no door and opens from any side.
/// </summary>
public class DisplayCaseBehaviour : MonoBehaviour
{
    /// <summary>How far the door slides along the case's length (Unity -Z): to the right, seen from the door. Set per piece.</summary>
    public float m_slideDistance = 1.2f;
    /// <summary>The jute floor along the case's length (local Z), set per piece; slots split it evenly.</summary>
    public float m_floorZ0 = -2.224f;
    public float m_floorZ1 = 1.821f;
    /// <summary>Height of the cloth items rest on, and its middle across (local X). The case's values by default.</summary>
    public float m_floorY = 1.038f;
    public float m_floorX = 0.365f;
    /// <summary>Largest an item may be across the floor, and in height.</summary>
    public float m_maxAcross = 1.15f;
    public float m_maxHeight = 0.5f;
    /// <summary>A display mat: no glass and no store side, it opens from anywhere (wards and LockSmith still decide who).</summary>
    public bool m_anySide;
    /// <summary>Height buy cards stand at: the glass lid on cases, just above the stock on mats.</summary>
    public float m_cardY = 1.72f;
    /// <summary>Customers' edge of the lid (local X, just inside the frame): cards stand up against it.</summary>
    public float m_cardEdgeX = 1.04f;

    private const float SlideSeconds = 0.45f;

    /// <summary>Local X of the case's middle: the door side is below it (see Forge/DisplayCase/README.md).</summary>
    private const float MiddleX = 0.36f;

    /// <summary>How much of its slot an item may fill along the floor.</summary>
    private const float SlotFill = 0.85f;
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
        m_anySide || transform.InverseTransformPoint(viewer.transform.position).x < MiddleX;

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
            Show(item, new Vector3(m_floorX, m_floorY, z), slotLength * SlotFill);
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
    private void Show(ItemDrop.ItemData item, Vector3 slotCentre, float slotLength)
    {
        var model = StillCopy(item.m_dropPrefab);
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
        var scale = Mathf.Min(1f, m_maxAcross / Mathf.Max(across, 0.001f), slotLength / Mathf.Max(along, 0.001f), m_maxHeight / Mathf.Max(height, 0.001f));

        var t = model.transform;
        t.SetParent(_shelf, false);
        t.localRotation = lay;
        t.localScale = Vector3.one * scale;
        // Centre the model's bounds on the slot, resting on the cloth.
        var centre = lay * (bounds.center * scale);
        t.localPosition = slotCentre - centre + Vector3.up * (height * scale / 2f + 0.003f);
        model.AddComponent<DisplayedItemTag>().Item = item;
        model.SetActive(true);
    }

    /// <summary>
    /// The item's prefab copied while inactive (so none of its scripts wake up), stripped of scripts, physics and
    /// sound and particles (the ground sparkle): only meshes and lights remain. Returned inactive.
    /// </summary>
    internal static GameObject? StillCopy(GameObject prefab)
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
        // Small items would be culled by their LOD group once shrunk onto the shelf: keep only the nearest LOD's meshes.
        foreach (var lods in copy.GetComponentsInChildren<LODGroup>(true))
        {
            var levels = lods.GetLODs();
            for (var level = 1; level < levels.Length; level++)
                foreach (var lodRenderer in levels[level].renderers)
                    if (lodRenderer != null)
                        lodRenderer.enabled = false;
            DestroyImmediate(lods);
        }
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

    /// <summary>
    /// True when <paramref name="renderer"/> will draw once the copy under <paramref name="root"/> is switched on: enabled,
    /// and every object between it and the root active. Food also carries its inactive "placed on a table" model, which
    /// must not count, or the item is sized and centred on both and the visible one ends up buried or off its spot.
    /// </summary>
    internal static bool WillShow(Renderer renderer, Transform root)
    {
        if (!renderer.enabled)
            return false;
        for (var t = renderer.transform; t != null && t != root; t = t.parent)
            if (!t.gameObject.activeSelf)
                return false;
        return true;
    }

    /// <summary>Bounds of the copy's meshes in its own root space (renderer bounds are empty while it's inactive).</summary>
    internal static bool LocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        var any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!WillShow(renderer, root))
                continue;
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
