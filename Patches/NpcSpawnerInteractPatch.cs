using DrakesItemShop.Pieces;
using HarmonyLib;

namespace DrakesItemShop.Patches;

[HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
internal static class NpcSpawnerInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Bed __instance, Humanoid human, bool repeat, bool alt, ref bool __result)
    {
        if (repeat || alt || human is not Player player)
            return true;

        var spawner = __instance.GetComponent<ShopNpcSpawnerBehaviour>();
        if (spawner == null)
            return true;

        spawner.Interact(player);
        __result = true;
        return false;
    }
}
