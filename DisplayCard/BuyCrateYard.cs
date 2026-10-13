using System.Collections.Generic;
using DrakesItemShop.OpenCrate;
using DrakesItemShop.Pricing;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.DisplayCard;

/// <summary>
/// The stand's buy crates in the world: visual-only copies of the hammer crates (no cost, no network object) standing on the
/// stand's floor grid, showing the goods bought into them. They belong to the stand, so hover and E route to its Container.
/// Collision and item placement match the placeable open crates — only the inventory is virtual (held buy-stock).
/// </summary>
internal static class BuyCrateYard
{
    private const float FloorY = 0.08f;
    private const float Depth = 1f;
    private const int Handful = 4;
    private const float LongRatio = 2.0f;
    private const float LongMin = 0.35f;

    private enum Fit
    {
        Upright,
        FlatAlongX,
        FlatAlongZ,
    }

    public static void Build(Transform root, DisplayCaseBehaviour stand, ZDO zdo)
    {
        var crates = BuyCrates.All(zdo);
        if (crates.Count == 0)
            return;
        var grid = new BuyCrates.Grid(stand);
        var held = BuyOffers.Held(zdo);
        // Goods of one item are spread over its crates in layout order.
        var left = new Dictionary<string, int>(held);

        for (var i = 0; i < crates.Count; i++)
        {
            var plan = crates[i];
            if (ZNetScene.instance?.GetPrefab(plan.Kind.Prefab) is not { } prefab)
                continue;
            var crate = DisplayCaseBehaviour.StillCopy(prefab);
            if (crate == null)
                continue;
            var t = crate.transform;
            t.SetParent(root, false);
            t.localPosition = grid.Centre(plan.Col, plan.Row, plan.Kind.Cells);
            // The long crate's length (its X) runs along the stand (Z).
            t.localRotation = Quaternion.Euler(0f, 90f, 0f);
            t.localScale = Vector3.one;

            // Same floor + wall boxes as the hammer open crates (StillCopy strips colliders).
            OpenCrateShell.Apply(crate, plan.Kind.Cells, Depth, plan.Kind.Height);

            // Separate nonsolid volume for crosshair — shell boxes alone are thin floors/walls and miss side looks.
            var hit = new GameObject("drake_buy_crate_hit");
            hit.transform.SetParent(t, false);
            var hoverLayer = LayerMask.NameToLayer("piece_nonsolid");
            if (hoverLayer >= 0)
                hit.layer = hoverLayer;
            var box = hit.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, plan.Kind.Height / 2f, 0f);
            box.size = new Vector3(plan.Kind.Cells, plan.Kind.Height, Depth);

            var tag = crate.AddComponent<BuyCrateTag>();
            tag.Index = i;
            tag.Holds = plan.Holds;

            if (plan.Holds.Length > 0 && left.TryGetValue(plan.Holds, out var units) && units > 0)
                left[plan.Holds] = units - Fill(t, plan, units);
            crate.SetActive(true);
        }
    }

    /// <summary>
    /// Lays held stock like <see cref="OpenCrateBehaviour"/>: full size, upright/flat by crate shape, a handful per stack.
    /// Returns units this crate accounts for.
    /// </summary>
    private static int Fill(Transform crate, CratePlan plan, int units)
    {
        if (ObjectDB.instance?.GetItemPrefab(plan.Holds) is not { } prefab)
            return 0;
        var drop = prefab.GetComponent<ItemDrop>();
        if (drop == null)
            return 0;

        var maxStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
        var stacks = Mathf.Min(plan.Kind.Stacks, Mathf.CeilToInt(units / (float)maxStack));
        var cols = plan.Kind.InvW;
        var rows = Mathf.Max(1, plan.Kind.Stacks / cols);
        var width = (float)plan.Kind.Cells;
        var height = plan.Kind.Height;
        var tall = plan.Kind.Height > 1f;
        var longCrate = width > Depth * 1.15f;

        OpenCrateShell.InnerBounds(width, Depth, out var x0, out var x1, out var z0, out var z1);
        var cellW = (x1 - x0) / cols;
        var cellD = tall ? Mathf.Abs(z1 - z0) : (z1 - z0) / rows;
        var layerH = tall ? Mathf.Max(0.2f, (height - FloorY) * 0.85f / rows) : 0f;

        var left = units;
        for (var s = 0; s < stacks && left > 0; s++)
        {
            var stack = Mathf.Min(maxStack, left);
            left -= stack;

            var gx = s % cols;
            var gy = s / cols;
            var cx = x0 + (gx + 0.5f) * cellW;
            var cz = tall ? (z0 + z1) * 0.5f : z1 - (gy + 0.5f) * cellD;
            var cy = tall ? FloorY + gy * layerH : FloorY;
            var slotCentre = new Vector3(cx, cy, cz);

            var copies = stack <= 1 ? 1 : Mathf.Min(Handful, stack);
            for (var n = 0; n < copies; n++)
            {
                var jitter = copies == 1
                    ? Vector3.zero
                    : new Vector3(
                        ((n % 2) - 0.5f) * cellW * 0.22f,
                        n * 0.01f,
                        ((n / 2) - 0.5f) * (tall ? 0.06f : cellD * 0.22f));
                Place(crate, prefab, drop, slotCentre + jitter, width, height, tall, longCrate);
            }
        }
        return Mathf.Min(units, stacks * maxStack);
    }

    private static void Place(Transform crate, GameObject prefab, ItemDrop drop, Vector3 slotCentre,
        float outerW, float outerH, bool tall, bool longCrate)
    {
        var model = VisualCopy(prefab, drop);
        if (model == null)
            return;
        if (!DisplayCaseBehaviour.LocalBounds(model.transform, out var bounds) || bounds.size.sqrMagnitude < 1e-6f)
        {
            Object.Destroy(model);
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

        var fit = ChooseFit(tall, longCrate, isLong, longest.Size, Depth);
        var rot = fit switch
        {
            Fit.Upright => Quaternion.Inverse(Quaternion.LookRotation(thin.Dir, longest.Dir)),
            Fit.FlatAlongX => Quaternion.Inverse(Quaternion.LookRotation(mid.Dir, thin.Dir)),
            _ => Quaternion.Inverse(Quaternion.LookRotation(longest.Dir, thin.Dir)),
        };

        var t = model.transform;
        t.SetParent(crate, false);
        t.localRotation = rot;
        t.localScale = Vector3.one;

        var oriented = OrientedBounds(bounds, rot);
        t.localPosition = new Vector3(
            slotCentre.x - oriented.center.x,
            slotCentre.y - oriented.min.y + 0.004f,
            slotCentre.z - oriented.center.z);
        model.SetActive(true);
    }

    private static Fit ChooseFit(bool tall, bool longCrate, bool itemLong, float longest, float outerD)
    {
        if (tall)
            return Fit.Upright;
        if (longCrate)
            return itemLong || longest > outerD * 0.85f ? Fit.FlatAlongX : Fit.FlatAlongZ;
        if (itemLong)
            return Fit.Upright;
        return Fit.FlatAlongZ;
    }

    private static GameObject? VisualCopy(GameObject prefab, ItemDrop drop)
    {
        var item = drop.m_itemData.Clone();
        item.m_dropPrefab = prefab;
        var (look, _) = ItemRackBehaviour.LookOf(item);
        if (look == null)
            look = prefab;

        var dropCopy = DisplayCaseBehaviour.StillCopy(look);
        if (dropCopy != null && DisplayCaseBehaviour.LocalBounds(dropCopy.transform, out var b)
            && b.size.sqrMagnitude > 1e-6f)
            return dropCopy;
        if (dropCopy != null)
            Object.Destroy(dropCopy);

        var attach = ItemStand.GetAttachPrefab(look);
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

/// <summary>Marks a buy crate visual so the crosshair can hover it.</summary>
public class BuyCrateTag : MonoBehaviour
{
    public int Index;
    public string Holds = "";

    private const float MaxDistance = 8f;

    public static BuyCrateTag? UnderCrosshair(Transform root)
    {
        var camera = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
        if (camera == null)
            return null;
        var ray = new Ray(camera.transform.position, camera.transform.forward);
        BuyCrateTag? best = null;
        var bestDistance = MaxDistance;
        foreach (var tag in root.GetComponentsInChildren<BuyCrateTag>())
        {
            // Prefer the dedicated hover volume; fall back to any box (shell) if it's missing.
            var hit = tag.transform.Find("drake_buy_crate_hit");
            var boxes = hit != null
                ? hit.GetComponents<BoxCollider>()
                : tag.GetComponentsInChildren<BoxCollider>();
            foreach (var box in boxes)
            {
                if (box == null)
                    continue;
                if (box.bounds.IntersectRay(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tag;
                }
            }
        }
        return best;
    }
}

/// <summary>Hover a buy crate: what it holds; the owner can press E to collect.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class BuyCrateHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (__instance.GetComponent<BuyCardShelf>() == null || BuyCrateTag.UnderCrosshair(__instance.transform) is not { } crate)
            return;
        if (BuyCardTag.UnderCrosshair(__instance.transform) != null)
            return;
        var zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
        var shopName = global::Localization.instance.Localize(__instance.m_name);
        if (crate.Holds.Length == 0)
        {
            __result = $"Buy crate (not assigned)\n{shopName}\n<color=grey>Shift+E → Layout to choose what it holds</color>";
            return;
        }
        var name = BuyOffers.ItemLabel(crate.Holds);
        __result = $"{name} crate\n{shopName}\n<color=#e0ad4f>{BuyOffers.HeldOf(zdo, crate.Holds)} / {BuyCrates.Capacity(zdo, crate.Holds)} held</color>";
        var player = Player.m_localPlayer;
        if (player != null && ShopState.CanManage(__instance, player) && BuyOffers.HeldOf(zdo, crate.Holds) > 0)
            __result += "\n[<color=yellow><b>E</b></color>] Collect";
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class BuyCrateInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (alt || __instance.GetComponent<BuyCardShelf>() == null || character is not Player player)
            return true;
        if (BuyCrateTag.UnderCrosshair(__instance.transform) is not { } crate || BuyCardTag.UnderCrosshair(__instance.transform) != null)
            return true;
        __result = true;
        if (hold || crate.Holds.Length == 0 || !ShopState.CanManage(__instance, player))
            return false;
        var moved = ShopSell.Collect(__instance, crate.Holds, player);
        Trade.TradeNet.Say(moved > 0
            ? $"Collected {moved} {BuyOffers.ItemLabel(crate.Holds)}."
            : BuyOffers.HeldOf(__instance.GetComponent<ZNetView>()?.GetZDO(), crate.Holds) > 0 ? "No room in your inventory." : "The crate is empty.");
        return false;
    }
}
