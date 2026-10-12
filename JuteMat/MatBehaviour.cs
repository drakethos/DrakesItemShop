using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Mats;

/// <summary>Where one item lies on the mat: mat-local metres, degrees, a lift off the weave, and a size factor.</summary>
internal struct MatPose
{
    public float X, Z, Yaw, Lift, Scale;

    public const string Key = "drake_mat_pose";

    public static MatPose Default(int index) => new()
    {
        // Unplaced items fan out in rows until the stocker places them.
        X = -0.7f + (index % 4) * 0.46f,
        Z = -0.55f + (index / 4) * 0.55f,
        Yaw = 0f, Lift = 0f, Scale = 1f,
    };

    public static bool TryRead(ItemDrop.ItemData item, out MatPose pose)
    {
        pose = default;
        if (item.m_customData == null || !item.m_customData.TryGetValue(Key, out var text))
            return false;
        var f = text.Split(',');
        if (f.Length != 5)
            return false;
        var v = new float[5];
        for (var i = 0; i < 5; i++)
            if (!float.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
                return false;
        pose = new MatPose { X = v[0], Z = v[1], Yaw = v[2], Lift = v[3], Scale = v[4] };
        return true;
    }

    public void Write(ItemDrop.ItemData item)
    {
        item.m_customData ??= new Dictionary<string, string>();
        item.m_customData[Key] = string.Join(",", new[] { X, Z, Yaw, Lift, Scale }.Select(n => n.ToString("0.###", CultureInfo.InvariantCulture)));
    }

    public static void Clear(ItemDrop.ItemData item) => item.m_customData?.Remove(Key);

    public override string ToString() => $"{X:0.00},{Z:0.00},{Yaw:0},{Lift:0.00},{Scale:0.00}";
}

/// <summary>
/// Free-layout display mat (jute / wolf / lox fabric): goods lie anywhere on a 2 x 2 m mat, each with its own position, turn,
/// lift and size (kept on the item stack). Capacity differs by piece; layout is the Shift+E Layout tab.
/// </summary>
public class MatBehaviour : MonoBehaviour
{
    /// <summary>Top of the weave (model space).</summary>
    internal const float Top = 0.028f;
    /// <summary>The longest side of a laid item never exceeds this (metres) at size 1.</summary>
    private const float MaxLongest = 1.3f;
    /// <summary>Half-extent of the usable mat surface (metres). Full jute/wolf/lox = 0.92; small deer ≈ 0.45.</summary>
    internal const float DefaultHalf = 0.92f;
    /// <summary>Per-piece usable half-size; set at register time for small mats.</summary>
    public float Half = DefaultHalf;
    private const float RefreshSeconds = 0.5f;

    private Container? _container;
    private Transform? _shelf;
    private string _shown = "\u0001";
    private float _next;

    internal Container? Box => _container;

    private void Awake() => _container = GetComponent<Container>();

    private void Update()
    {
        if (_container == null || Time.time < _next || ZNet.instance == null || ZNet.instance.IsDedicated())
            return;
        _next = Time.time + RefreshSeconds;
        Refresh();
    }

    /// <summary>Force the shelf to rebuild now (after a Layout tab edit).</summary>
    internal void Invalidate()
    {
        _shown = "\u0001";
        _next = 0f;
        if (_container != null && ZNet.instance != null && !ZNet.instance.IsDedicated())
            Refresh();
    }

    internal static IEnumerable<ItemDrop.ItemData> OrderedItems(Container container) =>
        container.GetInventory().GetAllItems().OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x);

    private void Refresh()
    {
        var items = OrderedItems(_container!).ToList();
        var text = new StringBuilder();
        foreach (var item in items)
            text.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|').Append(item.m_variant).Append('|')
                .Append(item.m_customData != null && item.m_customData.TryGetValue(MatPose.Key, out var p) ? p : "").Append(';');
        if (text.ToString() == _shown)
            return;
        _shown = text.ToString();

        if (_shelf != null)
            Destroy(_shelf.gameObject);
        _shelf = new GameObject("drake_mat_items").transform;
        _shelf.SetParent(transform, false);
        for (var i = 0; i < items.Count; i++)
        {
            var pose = MatPose.TryRead(items[i], out var stored) ? stored : MatPose.Default(i);
            pose.X = Mathf.Clamp(pose.X, -Half, Half);
            pose.Z = Mathf.Clamp(pose.Z, -Half, Half);
            if (Build(items[i], pose, _shelf) is { } model)
                model.AddComponent<DisplayedItemTag>().Item = items[i];
        }
    }

    /// <summary>A still copy of an item's model (as it looks, reskins included) lying flat at <paramref name="pose"/> under <paramref name="parent"/>.</summary>
    internal static GameObject? Build(ItemDrop.ItemData item, MatPose pose, Transform parent)
    {
        var (prefab, _) = ItemRackBehaviour.LookOf(item);
        var model = prefab != null ? DisplayCaseBehaviour.StillCopy(prefab) : null;
        if (model == null)
            return null;
        if (!DisplayCaseBehaviour.LocalBounds(model.transform, out var bounds))
        {
            UnityEngine.Object.Destroy(model);
            return null;
        }

        // Lay it down: longest side across X, the middle one along Z, the thinnest up.
        var axes = new List<(Vector3 Dir, float Size)> { (Vector3.right, bounds.size.x), (Vector3.up, bounds.size.y), (Vector3.forward, bounds.size.z) };
        axes.Sort((a, b) => b.Size.CompareTo(a.Size));
        var lay = Quaternion.Inverse(Quaternion.LookRotation(axes[1].Dir, axes[2].Dir));
        var scale = Mathf.Min(1f, MaxLongest / Mathf.Max(axes[0].Size, 0.001f)) * Mathf.Clamp(pose.Scale, 0.3f, 2f);
        var yaw = Quaternion.Euler(0f, pose.Yaw, 0f);

        var t = model.transform;
        t.SetParent(parent, false);
        t.localRotation = yaw * lay;
        t.localScale = Vector3.one * scale;
        var centre = yaw * (lay * (bounds.center * scale));
        t.localPosition = new Vector3(pose.X, Top + Mathf.Max(0f, pose.Lift) + axes[2].Size * scale / 2f + 0.002f, pose.Z) - centre;
        model.SetActive(true);
        return model;
    }

    private void OnDestroy()
    {
        if (_shelf != null)
            Destroy(_shelf.gameObject);
    }
}

/// <summary>Pointing at the mat, a manager sees that layout is in the Lock / Shop menu.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class MatHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (__instance.GetComponent<MatBehaviour>() == null || Player.m_localPlayer == null)
            return;
        if (Pricing.ShopState.CanManage(__instance, Player.m_localPlayer))
            __result += "\n[<color=yellow><b>Shift+E</b></color>] Layout tab";
    }
}
