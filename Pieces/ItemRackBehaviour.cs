using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Armor, weapon or wall rack: a chest whose contents hang on the rack, one hook/fork per slot (container row 0 = the top
/// hooks, row 1 = the pegs below on the armor stands). Weapons hang the way a vanilla item stand shows them; armor
/// hangs its own model upright. Only the right kind of item goes in (see Patches/ItemRackPatches.cs).
/// Contents come from the container's synced ZDO, so every client shows its own copy.
/// </summary>
public class ItemRackBehaviour : MonoBehaviour
{
    public enum RackKind
    {
        Armor,
        Weapon,
        /// <summary>Wall racks: armor and weapons.</summary>
        Any,
    }

    public RackKind m_kind;
    /// <summary>Slots per row (= container width).</summary>
    public int m_columns = 1;
    /// <summary>Hang points, row by row (index = row * columns + column).</summary>
    public Vector3[] m_hangPoints = System.Array.Empty<Vector3>();
    /// <summary>How far below its hang point an item may reach, per row.</summary>
    public float[] m_rowDrop = System.Array.Empty<float>();
    /// <summary>Local Z of the bar/board face the hooks come out of.</summary>
    public float m_front;
    /// <summary>Distance between neighbouring slots.</summary>
    public float m_pitch = 0.6f;

    private const float RefreshSeconds = 0.5f;
    /// <summary>Items may be a bit wider than their slot (shields overlap their neighbours a little).</summary>
    private const float WidthAllowance = 1.25f;
    private const float MaxAbove = 0.4f;

    private static readonly Dictionary<Inventory, ItemRackBehaviour> Racks = new();
    private static Quaternion? _standPose;

    private Container? _container;
    private Inventory? _inventory;
    private Transform? _display;
    private string _shownContents = "";
    private float _nextRefresh;

    /// <summary>The rack an inventory belongs to, if any.</summary>
    public static ItemRackBehaviour? Of(Inventory? inventory) =>
        inventory != null && Racks.TryGetValue(inventory, out var rack) && rack != null ? rack : null;

    /// <summary>Armor racks take armor (helmet, chest, legs, cape), weapon racks weapons and shields (they're held), wall racks both.</summary>
    public bool Accepts(ItemDrop.ItemData? item) => m_kind switch
    {
        RackKind.Armor => IsArmor(item),
        RackKind.Weapon => IsWeapon(item),
        _ => IsArmor(item) || IsWeapon(item),
    };

    private static bool IsArmor(ItemDrop.ItemData? item) =>
        item?.m_shared?.m_itemType is ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest
            or ItemDrop.ItemData.ItemType.Legs or ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Hands;

    /// <summary>True for items a rack poses by weapon type (the pose settings apply).</summary>
    internal static bool HasPose(ItemDrop.ItemData? item) => IsWeapon(item);

    private static bool IsWeapon(ItemDrop.ItemData? item) =>
        item?.m_shared?.m_itemType is ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
            or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow
            or ItemDrop.ItemData.ItemType.Shield or ItemDrop.ItemData.ItemType.Tool;

    public string RefusalMessage => m_kind switch
    {
        RackKind.Armor => "$drake_rack_armor_only",
        RackKind.Weapon => "$drake_rack_weapons_only",
        _ => "$drake_rack_gear_only",
    };

    private void Start()
    {
        _container = GetComponent<Container>();
        _inventory = _container != null ? _container.GetInventory() : null;
        if (_inventory != null)
            Racks[_inventory] = this;
    }

    private void Update()
    {
        if (Time.time < _nextRefresh)
            return;
        _nextRefresh = Time.time + RefreshSeconds;
        Refresh();
    }

    private void Refresh()
    {
        RackPoseSettings.Poll();
        if (_inventory == null || ZNet.instance == null || ZNet.instance.IsDedicated())
            return;

        var contents = RackPoseSettings.Version + "#" + Describe(_inventory);
        if (contents == _shownContents)
            return;
        _shownContents = contents;

        if (_display != null)
            Destroy(_display.gameObject);
        _display = new GameObject("drake_rack_items").transform;
        _display.SetParent(transform, false);

        foreach (var item in _inventory.GetAllItems())
        {
            if (item?.m_dropPrefab == null || !Accepts(item) || item.m_gridPos.x >= m_columns)
                continue;
            var index = item.m_gridPos.y * m_columns + item.m_gridPos.x;
            if (index < 0 || index >= m_hangPoints.Length)
                continue;
            var row = Mathf.Clamp(item.m_gridPos.y, 0, Mathf.Max(0, m_rowDrop.Length - 1));
            Hang(item, m_hangPoints[index], m_rowDrop.Length > 0 ? m_rowDrop[row] : 1f);
        }
    }

    private static string Describe(Inventory inventory)
    {
        var text = new StringBuilder();
        foreach (var item in inventory.GetAllItems())
            text.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|').Append(item.m_gridPos.x).Append(',')
                .Append(item.m_gridPos.y).Append('|').Append(item.m_quality).Append('|').Append(item.m_variant).Append('|')
                .Append(DrakeModsLibs.Display.ItemLookService.GetModelOverride(item)).Append(';');
        return text.ToString();
    }

    // ---- hanging one item ----

    private void Hang(ItemDrop.ItemData item, Vector3 hangPoint, float maxDrop)
    {
        var shown = _display.childCount;
        HangCore(item, hangPoint, maxDrop);
        // A reskinned item keeps its colour on the rack.
        if (_display.childCount > shown)
            DrakeModsLibs.Display.WorldLook.TintFor(_display.GetChild(_display.childCount - 1).gameObject, item);
    }

    private void HangCore(ItemDrop.ItemData item, Vector3 hangPoint, float maxDrop)
    {
        var holder = new GameObject("slot").transform;
        holder.SetParent(_display, false);
        holder.localPosition = hangPoint;
        TagItem(holder, item, hangPoint);

        // Armor looks the way an armor stand shows it: worn, on an invisible body.
        if (IsArmor(item) && HangWorn(item, holder))
            return;

        // What it's shown as (a reskinned item is posed and sized as the item it pretends to be).
        var (prefab, shared) = LookOf(item);
        var held = IsWeapon(item) && HangLikeItemStand(prefab, shared, holder);
        if (!held && !HangUpright(prefab, holder))
        {
            Destroy(holder.gameObject);
            return;
        }

        // Second row of a double-sided stand: out of the back of the bar, facing -Z.
        var back = hangPoint.z < 0f;
        var face = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        holder.localRotation = face * holder.localRotation;
        var maxAbove = MaxAbove;
        var widthAllowance = WidthAllowance;
        var pose = held ? RackPoseSettings.Resolve(prefab.name, RackPoseSettings.GroupOf(prefab.name, shared)) : null;
        if (pose != null)
            Orient(prefab.name, pose, face, holder, hangPoint, ref maxDrop, ref maxAbove, ref widthAllowance);
        if (!Bounds(holder, transform, out var bounds))
            return;

        // Shrink around the hang point only as far as needed to fit: width, how far it may hang down, how far up.
        var scale = pose is { Fit: false } ? 1f : Mathf.Min(1f,
            m_pitch * widthAllowance / Mathf.Max(bounds.size.x, 0.001f),
            maxDrop / Mathf.Max(hangPoint.y - bounds.min.y, 0.001f),
            maxAbove / Mathf.Max(bounds.max.y - hangPoint.y, 0.001f));
        holder.localScale = Vector3.one * scale;
        var min = hangPoint + (bounds.min - hangPoint) * scale;
        // Never behind the bar/board the hooks come out of.
        if (!back && min.z < m_front + 0.005f)
            holder.localPosition += Vector3.forward * (m_front + 0.005f - min.z);
        var max = hangPoint + (bounds.max - hangPoint) * scale;
        if (back && max.z > -m_front - 0.005f)
            holder.localPosition -= Vector3.forward * (max.z + m_front + 0.005f);
        if (pose != null)
            holder.localPosition += face * pose.Offset;
    }

    /// <summary>Room above the fork for something standing on its head, or a long weapon leaning in it.</summary>
    private const float MaxAboveStanding = 1.6f;
    private const float MaxAboveShield = 1.0f;
    /// <summary>Head-on-hook tools may be this many slots wide.</summary>
    private const float HeadWidth = 2.4f;
    /// <summary>How far the top of a hanging head rises above the fork it rests on.</summary>
    private const float HeadAboveFork = 0.12f;
    /// <summary>Shields may be this many slots wide and overlap their neighbours.</summary>
    private const float ShieldWidth = 2.6f;
    /// <summary>How far in front of the hang point a shield's back sits (past the peg tips).</summary>
    private const float ShieldStandOff = 0.07f;
    /// <summary>Hanging weapons reach down to just above the floor (stand forks are at 1.36 m).</summary>
    private const float HangDrop = 1.3f;

    /// <summary>
    /// Poses a weapon the way its <see cref="RackPose"/> says, at its real size: style (how it rests), then the configured
    /// size, turn and (after fitting, in <see cref="Hang"/>) offset. A vanilla item stand shows every weapon point/head up,
    /// which is where each style starts from. Limits for the final shrink-to-fit are adjusted by ref.
    /// </summary>
    private void Orient(string name, RackPose pose, Quaternion face, Transform holder, Vector3 hangPoint,
        ref float maxDrop, ref float maxAbove, ref float width)
    {
        if (holder.childCount == 0)
            return;
        var model = holder.GetChild(0);
        var style = pose.Style == RackStyle.Floor && m_kind != RackKind.Weapon ? RackStyle.AsPosed : pose.Style;

        if (!Mathf.Approximately(pose.Scale, 1f))
            model.localScale *= pose.Scale;

        if (style == RackStyle.HeadDown && Bounds(holder, transform, out var first))
        {
            var wider = WiderEndIsUp(holder);
            var farEndUp = first.max.y - hangPoint.y > hangPoint.y - first.min.y;
            // The wide (head) end down. Without mesh data the far end is the head.
            var flip = wider ?? farEndUp;
            if (flip)
                holder.localRotation = Quaternion.Euler(0f, 0f, 180f) * holder.localRotation;
            Debug.Log($"[ItemRack] {name}: HeadDown, {(wider.HasValue ? "mesh" : "grip point")} says head {(flip ? "up, flipped" : "down")}");
        }

        // The configured turn, in the item's own frame (the back row faces the other way).
        if (pose.Rotation != Vector3.zero)
            holder.localRotation = face * Quaternion.Euler(pose.Rotation) * Quaternion.Inverse(face) * holder.localRotation;

        if (style == RackStyle.AsPosed || !Bounds(holder, transform, out var bounds))
            return;

        var lift = Vector3.zero;
        switch (style)
        {
            case RackStyle.PointUp:
                maxAbove = MaxAboveStanding;                                       // swords keep their length
                break;
            case RackStyle.HeadOnHook:
                lift = Vector3.up * (hangPoint.y + HeadAboveFork - bounds.max.y);  // the head sits on the hook
                maxDrop = Mathf.Max(maxDrop, HangDrop);
                width = HeadWidth;                                                 // broad heads (pickaxe) may overlap neighbours
                break;
            case RackStyle.HeadDown:
                lift = Vector3.up * (hangPoint.y - bounds.min.y);                  // the head rests on the fork
                maxAbove = MaxAboveStanding;
                break;
            case RackStyle.Floor:
                lift = Vector3.up * (0.04f - bounds.min.y) + Vector3.right * (hangPoint.x - bounds.center.x);   // butt on the floor
                maxDrop = float.MaxValue;
                maxAbove = MaxAboveStanding;
                break;
            case RackStyle.Shield:
                // Centred on the fork so it covers the pegs, its back just in front of their tips.
                lift = Vector3.up * (hangPoint.y - bounds.center.y) + Vector3.right * (hangPoint.x - bounds.center.x);
                lift += hangPoint.z < 0f
                    ? Vector3.forward * (hangPoint.z - ShieldStandOff - bounds.max.z)
                    : Vector3.forward * (hangPoint.z + ShieldStandOff - bounds.min.z);
                maxAbove = MaxAboveShield;
                width = ShieldWidth;
                break;
        }
        if (lift != Vector3.zero)
            model.localPosition += holder.InverseTransformVector(transform.TransformVector(lift));
    }

    private static void TagItem(Transform holder, ItemDrop.ItemData item, Vector3 hangPoint)
    {
        var tag = holder.gameObject.AddComponent<DisplayedItemTag>();
        tag.Item = item;
        tag.Hook = hangPoint;
        tag.HookSpace = holder.parent;
    }

    /// <summary>
    /// True when the wider outer end of the (posed) item points up. The guard or head is wider than the blade tip or
    /// handle end. Null when a mesh can't be read on the CPU.
    /// </summary>
    private bool? WiderEndIsUp(Transform holder)
    {
        var points = new List<Vector3>();
        var toRack = transform.worldToLocalMatrix;
        foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
        {
            if (!WillShow(renderer, holder))
                continue;
            Mesh? mesh = renderer switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                MeshRenderer => renderer.GetComponent<MeshFilter>()?.sharedMesh,
                _ => null
            };
            if (mesh == null)
                continue;
            if (!mesh.isReadable)
                return null;
            var matrix = toRack * renderer.transform.localToWorldMatrix;
            foreach (var vertex in mesh.vertices)
                points.Add(matrix.MultiplyPoint3x4(vertex));
        }
        if (points.Count < 8)
            return null;

        var lowest = float.MaxValue;
        var highest = float.MinValue;
        foreach (var p in points)
        {
            lowest = Mathf.Min(lowest, p.y);
            highest = Mathf.Max(highest, p.y);
        }
        var band = (highest - lowest) * 0.25f;
        return EndWidth(points, highest - band, float.MaxValue) > EndWidth(points, float.MinValue, lowest + band);
    }

    private static float EndWidth(List<Vector3> points, float fromY, float toY)
    {
        var lo = new Vector2(float.MaxValue, float.MaxValue);
        var hi = new Vector2(float.MinValue, float.MinValue);
        foreach (var p in points)
        {
            if (p.y < fromY || p.y > toY)
                continue;
            lo = new Vector2(Mathf.Min(lo.x, p.x), Mathf.Min(lo.y, p.z));
            hi = new Vector2(Mathf.Max(hi.x, p.x), Mathf.Max(hi.y, p.z));
        }
        return Mathf.Max(hi.x - lo.x, hi.y - lo.y);
    }

    /// <summary>The prefab and data an item is shown as: its model override (reskin) when it has a valid one, else itself.</summary>
    internal static (GameObject Prefab, ItemDrop.ItemData.SharedData Shared) LookOf(ItemDrop.ItemData item)
    {
        if (DrakeModsLibs.Display.ItemLookService.GetModelOverride(item) is { Length: > 0 } sourceRef && ObjectDB.instance != null)
        {
            DrakeModsLibs.Display.ItemIconService.ParseSourceRef(sourceRef, out var prefabName, out _);
            var look = ObjectDB.instance.GetItemPrefab(prefabName);
            if (look != null && look.GetComponent<ItemDrop>()?.m_itemData?.m_shared is { } shared)
                return (look, shared);
        }
        return (item.m_dropPrefab, item.m_shared);
    }

    /// <summary>
    /// Armor, shown worn as on an armor stand (baked once per item by <see cref="WornArmor"/>, so each hook only holds
    /// a few static meshes). Hung from the hook at the neck (chest, cape), the top of the head (helmet) or the hips
    /// (legs). False when the item can't be shown that way.
    /// </summary>
    private static bool HangWorn(ItemDrop.ItemData item, Transform holder)
    {
        if (WornArmor.Get(item) is not { } look)
            return false;

        var worn = new GameObject("worn").transform;
        worn.SetParent(holder, false);
        foreach (var part in look.Parts)
        {
            var go = new GameObject("part");
            go.transform.SetParent(worn, false);
            go.transform.localPosition = part.Pose.GetColumn(3);
            go.transform.localRotation = part.Pose.rotation;
            go.transform.localScale = part.Pose.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = part.Materials;
        }

        var anchorY = item.m_shared.m_itemType switch
        {
            ItemDrop.ItemData.ItemType.Helmet => look.Head.y + 0.2f,
            ItemDrop.ItemData.ItemType.Legs => look.Head.y * 0.57f,
            _ => look.Head.y - 0.1f,
        };
        worn.localPosition = -new Vector3(look.Head.x, anchorY, look.Head.z);
        return true;
    }

    /// <summary>
    /// Weapons and shields: the item's "attach" model posed exactly as a vanilla (vertical) item stand poses it,
    /// grip on the fork. False when the item has no attach model.
    /// </summary>
    private static bool HangLikeItemStand(GameObject prefab, ItemDrop.ItemData.SharedData shared, Transform holder)
    {
        var attach = ItemStand.GetAttachPrefab(prefab);
        if (attach == null || StandPose() is not { } pose)
            return false;

        holder.localRotation = pose;
        var model = StillCopy(ItemStand.GetAttachGameObject(attach));
        if (model == null)
            return false;
        var t = model.transform;
        t.SetParent(holder, false);
        t.localPosition = attach.transform.localPosition;
        t.localRotation = attach.transform.localRotation;
        t.localScale = attach.transform.localScale;
        // The item's own item-stand offsets (first one for vertical stands), like ItemStand.SetVisualItem.
        if (VerticalStandOffset(shared) is { } offset)
        {
            t.localPosition += offset.Position;
            t.localRotation *= Quaternion.Euler(offset.Rotation);
            t.localScale = Vector3.Scale(t.localScale, offset.Scale);
        }
        model.SetActive(true);
        return true;
    }

    /// <summary>
    /// The item's first item-stand offset for vertical stands (SharedData.m_itemStandOffsets). Read by reflection:
    /// the API stubs we build against predate it, and older games simply have none.
    /// </summary>
    private static (Vector3 Position, Vector3 Rotation, Vector3 Scale)? VerticalStandOffset(ItemDrop.ItemData.SharedData shared)
    {
        const int vertical = 1;   // ItemStand.Orientation.Vertical
        if (OffsetsField?.GetValue(shared) is not System.Collections.IEnumerable offsets)
            return null;
        foreach (var offset in offsets)
        {
            if (offset == null)
                continue;
            var type = offset.GetType();
            var orientations = System.Convert.ToInt32(type.GetField("m_orientations")?.GetValue(offset) ?? vertical);
            if ((orientations & vertical) == 0)
                continue;
            return ((Vector3)(type.GetField("m_position")?.GetValue(offset) ?? Vector3.zero),
                    (Vector3)(type.GetField("m_rotation")?.GetValue(offset) ?? Vector3.zero),
                    (Vector3)(type.GetField("m_scale")?.GetValue(offset) ?? Vector3.one));
        }
        return null;
    }

    private static readonly System.Reflection.FieldInfo? OffsetsField =
        typeof(ItemDrop.ItemData.SharedData).GetField("m_itemStandOffsets");

    /// <summary>Rotation of the vanilla wall item stand's attach point, turned so it faces out of our racks (+Z).</summary>
    private static Quaternion? StandPose()
    {
        if (_standPose != null)
            return _standPose;
        var stand = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("itemstand")?.GetComponent<ItemStand>() : null;
        if (stand == null || stand.m_attachOther == null)
            return null;
        var root = stand.transform;
        var pose = Quaternion.Inverse(root.rotation) * stand.m_attachOther.rotation;
        if (root.InverseTransformPoint(stand.m_attachOther.position).z < 0f)
            pose = Quaternion.Euler(0f, 180f, 0f) * pose;
        _standPose = pose;
        return pose;
    }

    /// <summary>Armor (and anything without an attach model): its dropped model stood up, flat side to the front, hanging from the hook.</summary>
    private static bool HangUpright(GameObject prefab, Transform holder)
    {
        var model = StillCopy(prefab);
        if (model == null)
            return false;
        if (!Bounds(model.transform, model.transform, out var bounds))
        {
            Destroy(model);
            return false;
        }

        // Thinnest side to the front, longest side up.
        var axes = new List<(Vector3 Dir, float Size)> { (Vector3.right, bounds.size.x), (Vector3.up, bounds.size.y), (Vector3.forward, bounds.size.z) };
        axes.Sort((a, b) => b.Size.CompareTo(a.Size));
        var stand = Quaternion.Inverse(Quaternion.LookRotation(axes[2].Dir, axes[0].Dir));

        var t = model.transform;
        t.SetParent(holder, false);
        t.localRotation = stand;
        // Top of the model just under the hook, centred on it.
        var centre = stand * bounds.center;
        t.localPosition = -centre - Vector3.up * (axes[0].Size / 2f + 0.02f);
        model.SetActive(true);
        return true;
    }

    /// <summary>
    /// A copy of <paramref name="source"/> made while it's inactive (so none of its scripts wake up), stripped of
    /// scripts, physics, sound and particles: only meshes and lights remain. Returned inactive.
    /// </summary>
    private static GameObject StillCopy(GameObject source)
    {
        var wasActive = source.activeSelf;
        source.SetActive(false);
        GameObject copy;
        try
        {
            copy = Instantiate(source);
        }
        finally
        {
            source.SetActive(wasActive);
        }

        copy.name = source.name + "_rack";
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
    private static bool WillShow(Renderer renderer, Transform root)
    {
        if (!renderer.enabled)
            return false;
        for (var t = renderer.transform; t != null && t != root; t = t.parent)
            if (!t.gameObject.activeSelf)
                return false;
        return true;
    }

    /// <summary>Bounds of the meshes under <paramref name="root"/> in <paramref name="space"/>'s local space (works while inactive).</summary>
    private static bool Bounds(Transform root, Transform space, out Bounds bounds)
    {
        bounds = default;
        var any = false;
        var toSpace = space.worldToLocalMatrix;
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
            var matrix = toSpace * renderer.transform.localToWorldMatrix;
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
        if (_inventory != null && Racks.TryGetValue(_inventory, out var rack) && rack == this)
            Racks.Remove(_inventory);
        if (_display != null)
            Destroy(_display.gameObject);
    }
}
