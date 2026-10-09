using HarmonyLib;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class MerchantBoxHoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (__instance.GetComponent<MerchantBoxBehaviour>() == null)
            return;

        if (string.IsNullOrEmpty(__result))
            __result = "";
        else if (!__result.Contains("Shift"))
            __result += "\n";

        __result += "[Shift + $KEY_Use] Shop menu";
    }
}
