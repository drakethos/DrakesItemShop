using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrakesItemShop.OpenCrate;

/// <summary>
/// Placeable open crate: stocked items appear at full size.
/// Orientation follows crate shape + mesh footprint (tall → upright, long → flat,
/// small + long items → stick up). Weapons use their attach model when the drop mesh is empty.
/// </summary>
public class OpenCrateBehaviour : MonoBehaviour
{
    public float FloorY = 0.08f;
    public float InnerX0 = -0.4f, InnerX1 = 0.4f;
    public float InnerZ0 = -0.4f, InnerZ1 = 0.4f;
    /// <summary>Outer crate size — drives upright vs flat fitting.</summary>
    public float OuterWidth = 1f, OuterDepth = 1f, OuterHeight = 1f;
    /// <summary>Tall crates: inventory rows stack upward instead of along Z.</summary>
    public bool StackRowsUp;

    private const int Handful = 4;
    private const float RefreshSeconds = 0.5f;
    /// <summary>Longest axis must beat the next by this factor to count as a "long" piece (sword, wood, spear).</summary>
    private const float LongRatio = 2.0f;
    private const float LongMin = 0.35f;

    private enum Fit
    {
        Upright,     // longest → up (sticks out of small/tall crates)
        FlatAlongX,  // longest → crate width (long crates)
        FlatAlongZ,  // longest → crate depth
    }

    private Container? _container;
    private Transform? _shelf;
    private string _shown = "";
    private float _next;

    private void Awake() => _container = GetComponent<Container>();

    private void Update()
    {
        if (_container == null || ZNet.instance == null || ZNet.instance.IsDedicated())
            return;
        if (Time.time < _next)
            return;
        _next = Time.time + RefreshSeconds;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_shelf != null)
            Destroy(_shelf.gameObject);
    }

    private bool CrateTall => StackRowsUp || OuterHeight >= OuterWidth * 1.15f;
    private bool CrateLong => OuterWidth > OuterDepth * 1.15f;

    private void Refresh()
    {
        if (_container!.GetInventory() is not { } inventory)
            return;

        var fingerprint = Describe(inventory);
        if (fingerprint == _shown)
            return;
        _shown = fingerprint;

        if (_shelf != null)
            Destroy(_shelf.gameObject);
        _shelf = new GameObject("drake_crate_items").transform;
        _shelf.SetParent(transform, false);

        var width = Mathf.Max(1, inventory.GetWidth());
        var height = Mathf.Max(1, inventory.GetHeight());
        var cellW = (InnerX1 - InnerX0) / width;
        var cellD = StackRowsUp
            ? Mathf.Abs(InnerZ1 - InnerZ0)
            : (InnerZ1 - InnerZ0) / height;
        var layerH = StackRowsUp ? Mathf.Max(0.2f, (OuterHeight - FloorY) * 0.85f / height) : 0f;

        foreach (var item in inventory.GetAllItems())
        {
            if (item?.m_dropPrefab == null)
                continue;
            var gx = item.m_gridPos.x;
            var gy = item.m_gridPos.y;
            if (gx < 0 || gy < 0 || gx >= width || gy >= height)
                continue;

            var cx = InnerX0 + (gx + 0.5f) * cellW;
            var cz = StackRowsUp
                ? (InnerZ0 + InnerZ1) * 0.5f
                : InnerZ1 - (gy + 0.5f) * cellD;
            var cy = StackRowsUp ? FloorY + gy * layerH : FloorY;

            var copies = item.m_stack <= 1 ? 1 : Mathf.Min(Handful, item.m_stack);
            for (var n = 0; n < copies; n++)
            {
                var jitter = copies == 1
                    ? Vector3.zero
                    : new Vector3(
                        ((n % 2) - 0.5f) * cellW * 0.22f,
                        n * 0.01f,
                        ((n / 2) - 0.5f) * (StackRowsUp ? 0.06f : cellD * 0.22f));
                Show(item, new Vector3(cx, cy, cz) + jitter);
            }
        }
    }

    private static string Describe(Inventory inventory)
    {
        var text = new StringBuilder();
        foreach (var item in inventory.GetAllItems())
            text.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|')
                .Append(item.m_gridPos.x).Append(',').Append(item.m_gridPos.y).Append('|')
                .Append(item.m_stack).Append('|').Append(item.m_quality).Append('|')
                .Append(item.m_variant).Append(';');
        return text.ToString();
    }

    private void Show(ItemDrop.ItemData item, Vector3 slotCentre)
    {
        var model = VisualCopy(item);
        if (model == null)
            return;
        if (!DisplayCaseBehaviour.LocalBounds(model.transform, out var bounds) || bounds.size.sqrMagnitude < 1e-6f)
        {
            Destroy(model);
            return;
        }

        var axes = new List<(Vector3 Dir, float Size)>
        {
            (Vector3.right, bounds.size.x),
            (Vector3.up, bounds.size.y),
            (Vector3.forward, bounds.size.z),
        };
        axes.Sort((a, b) => b.Size.CompareTo(a.Size));
        var longest = axes[0];
        var mid = axes[1];
        var thin = axes[2];
        var isLong = longest.Size >= LongMin && longest.Size >= mid.Size * LongRatio;

        var fit = ChooseFit(isLong, longest.Size);
        var rot = fit switch
        {
            // longest → up, thin → forward
            Fit.Upright => Quaternion.Inverse(Quaternion.LookRotation(thin.Dir, longest.Dir)),
            // longest → +X, thin → up
            Fit.FlatAlongX => Quaternion.Inverse(Quaternion.LookRotation(mid.Dir, thin.Dir)),
            // longest → +Z, thin → up
            _ => Quaternion.Inverse(Quaternion.LookRotation(longest.Dir, thin.Dir)),
        };

        var t = model.transform;
        t.SetParent(_shelf, false);
        t.localRotation = rot;
        t.localScale = Vector3.one; // full size — small crates let long items stick up

        var oriented = OrientedBounds(bounds, rot);
        t.localPosition = new Vector3(
            slotCentre.x - oriented.center.x,
            slotCentre.y - oriented.min.y + 0.004f,
            slotCentre.z - oriented.center.z);

        model.AddComponent<DisplayedItemTag>().Item = item;
        model.SetActive(true);
    }

    private Fit ChooseFit(bool itemLong, float longest)
    {
        // Tall crate: stand everything that can stand; long sticks go upright.
        if (CrateTall)
            return Fit.Upright;

        // Long/low crate: lay long pieces along the length; compact stuff stays low.
        if (CrateLong)
            return itemLong || longest > OuterDepth * 0.85f ? Fit.FlatAlongX : Fit.FlatAlongZ;

        // Square (standard / small_low): long pieces stick up; squat/coins lie flat.
        if (itemLong)
            return Fit.Upright;
        return Fit.FlatAlongZ;
    }

    /// <summary>
    /// Drop prefab when it has meshes; otherwise the item-stand attach model (weapons),
    /// matching how racks resolve visuals.
    /// </summary>
    private static GameObject? VisualCopy(ItemDrop.ItemData item)
    {
        var (prefab, _) = ItemRackBehaviour.LookOf(item);
        if (prefab == null)
            return null;

        var dropCopy = DisplayCaseBehaviour.StillCopy(prefab);
        if (dropCopy != null && DisplayCaseBehaviour.LocalBounds(dropCopy.transform, out var b)
            && b.size.sqrMagnitude > 1e-6f)
            return dropCopy;
        if (dropCopy != null)
            Destroy(dropCopy);

        var attach = ItemStand.GetAttachPrefab(prefab);
        if (attach == null)
            return null;
        var attachGo = ItemStand.GetAttachGameObject(attach);
        return attachGo != null ? DisplayCaseBehaviour.StillCopy(attachGo) : null;
    }

    private static Bounds OrientedBounds(Bounds local, Quaternion rot)
    {
        var e = local.extents;
        var c = local.center;
        var bb = new Bounds(rot * c, Vector3.zero);
        for (var i = 0; i < 8; i++)
        {
            var corner = c + Vector3.Scale(e, new Vector3(
                (i & 1) == 0 ? -1f : 1f,
                (i & 2) == 0 ? -1f : 1f,
                (i & 4) == 0 ? -1f : 1f));
            bb.Encapsulate(rot * corner);
        }
        return bb;
    }
}
