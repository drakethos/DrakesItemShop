using DrakesItemShop.Trade;
using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>The trade dummy is a chest: E opens it as usual, Shift+E opens the trade menu.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class TradeDummyInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance, bool hold, bool alt, ref bool __result)
    {
        if (!alt || !TradeDummyPiece.IsDummy(__instance))
            return true;

        __result = true;
        if (!hold)
            TradeDummyUi.StartTrade(TradeDummyUi.Partner);
        return false;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class TradeDummyHoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (TradeDummyPiece.IsDummy(__instance))
            __result += "\n[<color=yellow><b>Shift+E</b></color>] Trade";
    }
}
