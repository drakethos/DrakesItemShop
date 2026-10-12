using DrakeModsLibs.Display;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Marks the visual copy of a stocked item on a display case or rack, so hovering it can name the item (with its
/// RenameIt name) without opening the chest.
/// </summary>
public class DisplayedItemTag : MonoBehaviour
{
    public ItemDrop.ItemData? Item;
    /// <summary>Where the item hangs (rack-local), so pointing at its hook or fork counts: an item being nudged moves away from the crosshair.</summary>
    public Vector3? Hook;
    public Transform? HookSpace;

    private const float MaxDistance = 8f;
    /// <summary>Thin things (a blade seen edge-on) are still easy to point at.</summary>
    private const float Padding = 0.03f;
    /// <summary>Size of the box around a hook that selects its item.</summary>
    private const float HookSize = 0.2f;

    /// <summary>The stocked item the camera's crosshair is on, among the displayed items under <paramref name="root"/>.</summary>
    public static ItemDrop.ItemData? UnderCrosshair(Transform root)
    {
        var camera = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
        if (camera == null)
            return null;
        var ray = new Ray(camera.transform.position, camera.transform.forward);

        ItemDrop.ItemData? best = null;
        var bestDistance = MaxDistance;
        foreach (var tag in root.GetComponentsInChildren<DisplayedItemTag>())
        {
            if (tag.Item == null)
                continue;
            if (tag.Hook is { } hook && tag.HookSpace != null)
            {
                var around = new Bounds(tag.HookSpace.TransformPoint(hook), Vector3.one * HookSize);
                if (around.IntersectRay(ray, out var hookDistance) && hookDistance < bestDistance)
                {
                    bestDistance = hookDistance;
                    best = tag.Item;
                }
            }
            foreach (var renderer in tag.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                    continue;
                var bounds = renderer.bounds;
                bounds.Expand(Padding);
                if (bounds.IntersectRay(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tag.Item;
                }
            }
        }
        return best;
    }
}

/// <summary>Pointing at a stocked item on a rack or display case adds its name to the hover text.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class DisplayedItemHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (__instance.GetComponent<ItemRackBehaviour>() == null && __instance.GetComponent<DisplayCaseBehaviour>() == null
            && __instance.GetComponent<Mats.MatBehaviour>() == null)
            return;
        if (DisplayedItemTag.UnderCrosshair(__instance.transform) is not { } shown)
            return;
        var item = Pricing.ShopState.Live(__instance, shown);
        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        var player = Player.m_localPlayer;
        var open = Pricing.ShopState.IsOpen(__instance);
        var manage = player != null && Pricing.ShopState.CanManage(__instance, player);
        var raw = Pricing.ItemPrice.Raw(item);

        // Open shop + pointing at goods: replace vanilla "No access" — buyers don't need chest rights to browse/buy.
        if (open && !string.IsNullOrEmpty(name))
        {
            __result = name + "\n" + global::Localization.instance.Localize(__instance.m_name);
            if (Pricing.PayOptions.Has(item))
            {
                var buyHint = player != null && !manage ? "\n[<color=yellow><b>E</b></color>] Buy" : "";
                __result += $"\n<color=#e0ad4f>{Pricing.PayOptions.Summary(item)}</color>  {item.m_stack} in stock{buyHint}";
            }
            else if (Pricing.ShopState.IsCoinsStack(item) && Pricing.ItemPrice.Coins(item) > 0)
            {
                __result += "\n<color=grey>Coins aren't bought with coins. Use Set and add an item payment.</color>";
            }
            else if (raw.Length > 0)
                __result += $"\n<color=#e0ad4f>{raw}</color>\n<color=grey>Old note — use Set for a real price</color>";
            else if (manage)
                __result += "\n<color=grey>No price yet (Shop tab → Set)</color>";
            if (__instance.GetComponent<ItemRackBehaviour>() != null)
                __result += RackPoseDebug.Update(item);
            return;
        }

        if (!string.IsNullOrEmpty(name))
            __result = string.IsNullOrEmpty(__result) ? name : name + "\n" + __result;
        if (!open && raw.Length > 0 && manage)
            __result += $"\n<color=grey>Priced {raw}, but the shop is closed (Shift+E, Shop tab)</color>";
        if (__instance.GetComponent<ItemRackBehaviour>() != null)
            __result += RackPoseDebug.Update(item);
    }
}
