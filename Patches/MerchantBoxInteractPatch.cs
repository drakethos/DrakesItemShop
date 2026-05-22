using DrakesItemShop.Runtime;
using HarmonyLib;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class MerchantBoxInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance, Humanoid user, bool hold, bool alt, bool craft, bool showPickupPopup)
    {
        var box = __instance.GetComponent<MerchantBoxBehaviour>();
        if (box == null || user is not Player player)
            return true;

        if (!alt && !hold)
        {
            ShopOpener.TryOpenFromMerchantBox(player, box);
            return false;
        }

        return true;
    }
}
