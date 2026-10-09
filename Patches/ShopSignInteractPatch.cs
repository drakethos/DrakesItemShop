using DrakesItemShop.Pieces;
using DrakesItemShop.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
internal static class ShopSignInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Sign __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (hold || alt || character is not Player player)
            return true;

        var sign = __instance.GetComponent<ShopSignBehaviour>();
        if (sign == null)
            return true;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift)
        {
            if (ShopManageService.TryOpen(player, sign))
            {
                __result = true;
                return false;
            }
        }

        return true;
    }
}
