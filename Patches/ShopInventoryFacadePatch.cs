using HarmonyLib;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.GetInventory))]
internal static class ShopInventoryFacadeGetInventoryPatch
{
    [HarmonyPostfix]
    private static void Postfix(Container __instance, ref Inventory __result)
    {
        var facade = __instance.GetComponent<ShopInventoryFacade>();
        if (facade?.BoundInventory != null)
            __result = facade.BoundInventory;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Save))]
internal static class ShopInventoryFacadeSavePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance)
    {
        return __instance.GetComponent<ShopInventoryFacade>() == null;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Load))]
internal static class ShopInventoryFacadeLoadPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance)
    {
        return __instance.GetComponent<ShopInventoryFacade>() == null;
    }
}
