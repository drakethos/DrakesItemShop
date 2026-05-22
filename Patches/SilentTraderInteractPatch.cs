using DrakesItemShop.Npc;
using DrakesItemShop.Runtime;
using HarmonyLib;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
internal static class SilentTraderInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Trader __instance, Humanoid character, bool hold)
    {
        if (__instance is not SilentShopTrader silent || character is not Player player || hold)
            return true;

        ShopOpener.TryOpenFromNpc(player, silent);
        return false;
    }
}
