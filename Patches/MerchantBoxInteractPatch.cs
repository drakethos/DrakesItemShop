using DrakesItemShop.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class MerchantBoxInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt)
    {
        var box = __instance.GetComponent<MerchantBoxBehaviour>();
        if (box == null || character is not Player player || hold)
            return true;

        if (alt)
        {
            if (ShopManageService.TryOpen(player, box))
                return false;
            return true;
        }

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift)
        {
            ShopOpener.TryOpenFromMerchantBox(player, box);
            return false;
        }

        if (box.UsesVendingMachine() && !box.CanManageStock(player))
        {
            box.OpenDispense(player);
            return false;
        }

        return true;
    }
}
