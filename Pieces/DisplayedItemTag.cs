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
        if (__instance.GetComponent<ItemRackBehaviour>() == null && __instance.GetComponent<DisplayCaseBehaviour>() == null)
            return;
        if (DisplayedItemTag.UnderCrosshair(__instance.transform) is not { } item)
            return;
        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        if (!string.IsNullOrEmpty(name))
            __result = string.IsNullOrEmpty(__result) ? name : name + "\n" + __result;
        if (__instance.GetComponent<ItemRackBehaviour>() != null)
            __result += RackPoseDebug.Update(item);
    }
}
