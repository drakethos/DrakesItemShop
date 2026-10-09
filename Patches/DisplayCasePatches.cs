using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>The display case is only a chest from the store (door) side; from the glass side it's just looked at.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class DisplayCaseInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance, Humanoid character, ref bool __result)
    {
        var display = __instance.GetComponent<DisplayCaseBehaviour>();
        if (display == null || character == null || display.IsStoreSide(character))
            return true;

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class DisplayCaseHoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(Container __instance, ref string __result)
    {
        var display = __instance.GetComponent<DisplayCaseBehaviour>();
        if (display == null || Player.m_localPlayer == null || display.IsStoreSide(Player.m_localPlayer))
            return;

        __result = global::Localization.instance.Localize("$drake_display_case_name");
    }
}
