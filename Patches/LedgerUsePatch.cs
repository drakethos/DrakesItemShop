using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using DrakesItemShop.Data;
using DrakesItemShop.Runtime;
using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>Opens shop UI when using the ledger from inventory. Applied manually — UseItem signature varies by game build.</summary>
internal static class LedgerUsePatch
{
    public static void TryApply(Harmony harmony, ManualLogSource log)
    {
        MethodInfo? target = AccessTools.Method(typeof(Humanoid), "UseItem",
            new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool) });

        if (target == null)
        {
            target = AccessTools.Method(typeof(Humanoid), "UseItem",
                new[] { typeof(ItemDrop.ItemData), typeof(bool) });
        }

        if (target == null)
            target = FindUseItemMethod(typeof(Player));

        if (target == null)
            target = FindUseItemMethod(typeof(Humanoid));

        if (target == null)
        {
            log.LogWarning("Ledger use: no UseItem method found — open shops via merchant box or shop_spawn_npc.");
            return;
        }

        var prefix = target.GetParameters().Length >= 3
            ? new HarmonyMethod(typeof(LedgerUsePatch), nameof(PrefixWithInventory))
            : new HarmonyMethod(typeof(LedgerUsePatch), nameof(PrefixItemOnly));

        harmony.Patch(target, prefix: prefix);

        log.LogInfo($"Ledger use patched: {target.DeclaringType?.Name}.{target.Name}({FormatParams(target)})");
    }

    public static bool PrefixWithInventory(
        Humanoid __instance,
        Inventory inventory,
        ItemDrop.ItemData item,
        bool showMessage,
        ref bool __result) =>
        Handle(__instance, item, ref __result);

    public static bool PrefixItemOnly(
        Humanoid __instance,
        ItemDrop.ItemData item,
        bool showMessage,
        ref bool __result) =>
        Handle(__instance, item, ref __result);

    private static bool Handle(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
    {
        if (__instance is not Player player || !IsLedgerItem(item))
            return true;

        ShopOpener.TryOpenFromLedger(player, item);
        __result = true;
        return false;
    }

    private static bool IsLedgerItem(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;

        string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
        return prefabName == ShopItemNames.Ledger ||
               item.m_shared.m_name.Contains(ShopItemNames.Ledger);
    }

    private static MethodInfo? FindUseItemMethod(System.Type type)
    {
        foreach (var method in AccessTools.GetDeclaredMethods(type))
        {
            if (method.Name != "UseItem")
                continue;

            var p = method.GetParameters();
            if (p.Length == 0 || p[p.Length - 1].ParameterType != typeof(bool))
                continue;

            if (p.Any(param => param.ParameterType == typeof(ItemDrop.ItemData)))
                return method;
        }

        return null;
    }

    private static string FormatParams(MethodInfo method) =>
        string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
}
