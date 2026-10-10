using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Patches;

/// <summary>
/// Armor racks only take armor, weapon racks only weapons and shields. Every way an item gets into an inventory
/// (drag and drop, shift-click, take/put all) ends in one of these AddItem overloads; container loading doesn't.
/// </summary>
internal static class ItemRackPatches
{
    private static float _nextMessage;

    /// <summary>True when <paramref name="item"/> may not go into <paramref name="inventory"/> (and tells the player why).</summary>
    internal static bool Refuses(Inventory inventory, ItemDrop.ItemData? item)
    {
        var rack = ItemRackBehaviour.Of(inventory);
        if (rack == null || item == null || rack.Accepts(item))
            return false;

        if (Player.m_localPlayer != null && Time.time >= _nextMessage)
        {
            _nextMessage = Time.time + 1f;
            DrakeModsLibs.API.CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, rack.RefusalMessage);
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), "AddItem",
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
    private static class AddItemAtPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!Refuses(__instance, item))
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    private static class AddItemPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!Refuses(__instance, item))
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
    private static class AddItemPosPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!Refuses(__instance, item))
                return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Dropping onto an occupied slot swaps: vanilla removes the dragged item first and then moves both, so a refused
    /// half would lose an item. Refuse the whole swap up front instead.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
    private static class DropItemPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
        {
            var target = __instance.GetInventory();
            var refused = Refuses(target, item);
            if (!refused && fromInventory != null && target != null)
            {
                var swapped = target.GetItemAt(pos.x, pos.y);
                refused = swapped != null && swapped != item && Refuses(fromInventory, swapped);
            }
            if (!refused)
                return true;
            __result = false;
            return false;
        }
    }
}
