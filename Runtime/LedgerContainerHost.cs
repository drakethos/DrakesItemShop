using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop.Runtime;

/// <summary>Portable ledger stock — in-memory per player + ledger key.</summary>
internal static class LedgerContainerHost
{
    private static readonly Dictionary<string, Inventory> Inventories = new();

    public static Container? GetOrCreate(Player player, ItemDrop.ItemData ledgerItem)
    {
        string key = BuildKey(player, ledgerItem);
        if (!Inventories.TryGetValue(key, out var inv))
        {
            inv = new Inventory(nameof(LedgerContainerHost), null!, 8, 4);
            Inventories[key] = inv;
        }

        var go = new GameObject("Drake_LedgerContainerHost");
        Object.DontDestroyOnLoad(go);
        var container = go.AddComponent<Container>();
        GameAccess.SetInventory(container, inv);
        return container;
    }

    private static string BuildKey(Player player, ItemDrop.ItemData item)
    {
        string uid = item.m_crafterID.ToString();
        return $"{player.GetPlayerID()}_{uid}_{item.m_shared.m_name}";
    }
}
