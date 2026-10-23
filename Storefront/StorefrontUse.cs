using HarmonyLib;

namespace DrakesItemShop.Storefront;

/// <summary>
/// What the Storefront pieces implement for hover text and E. They can't implement Valheim's Hoverable / Interactable directly
/// (the game's Hoverable has members the compile-time references lack, so such a class fails to load), so the pieces keep the
/// chest clone's Container and these patches hand its hover and E over to the piece's IStorefrontUse.
/// </summary>
public interface IStorefrontUse
{
    string GetHoverText();
    bool Interact(Humanoid user, bool hold, bool alt);
}

internal static class StorefrontUse
{
    /// <summary>Set while a piece opens its own register chest, so that call reaches the real Container.</summary>
    internal static bool Bypass;
}

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class StorefrontHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (StorefrontUse.Bypass || __instance.GetComponent<IStorefrontUse>() is not { } use)
            return;
        __result = use.GetHoverText();
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class StorefrontInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (StorefrontUse.Bypass || __instance.GetComponent<IStorefrontUse>() is not { } use)
            return true;
        __result = use.Interact(character, hold, alt);
        return false;
    }
}
