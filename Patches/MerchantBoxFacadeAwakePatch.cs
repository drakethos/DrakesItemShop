using System;
using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>
/// The merchant box's inventory facades are bare Containers with no ZNetView of their own: vanilla Container.Awake
/// throws on them (the view is bound right after, see GameAccess.SetView). The throw is harmless, so don't log it.
/// </summary>
[HarmonyPatch(typeof(Container), "Awake")]
internal static class MerchantBoxFacadeAwakePatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(Container __instance, Exception? __exception) =>
        __exception != null && __instance.name.EndsWith("Facade") && __instance.GetComponentInParent<MerchantBoxBehaviour>() != null
            ? null
            : __exception;
}
