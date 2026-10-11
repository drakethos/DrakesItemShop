using System;
using System.Collections.Generic;

namespace DrakesItemShop.Trade;

/// <summary>Items on offer as bytes, so the other player's client can rebuild them exactly (quality, durability, names, tints).</summary>
internal static class TradeItemCodec
{
    public static void Write(ZPackage pkg, IReadOnlyList<TradeEntry> entries)
    {
        var valid = new List<TradeEntry>();
        foreach (var entry in entries)
            if (entry.Item.m_dropPrefab != null)
                valid.Add(entry);

        pkg.Write(valid.Count);
        foreach (var entry in valid)
        {
            var item = entry.Item;
            pkg.Write(item.m_dropPrefab.name);
            pkg.Write(entry.Amount);
            pkg.Write(item.m_durability);
            pkg.Write(item.m_quality);
            pkg.Write(item.m_variant);
            pkg.Write(item.m_crafterID);
            pkg.Write(item.m_crafterName ?? "");
            var data = item.m_customData;
            pkg.Write(data?.Count ?? 0);
            if (data != null)
            {
                foreach (var pair in data)
                {
                    pkg.Write(pair.Key);
                    pkg.Write(pair.Value);
                }
            }
        }
    }

    /// <summary>Rebuild the entries. Anything the game doesn't know (a missing mod) is skipped, amounts are kept within a stack.</summary>
    public static List<TradeEntry> Read(ZPackage pkg)
    {
        var entries = new List<TradeEntry>();
        var count = Math.Min(pkg.ReadInt(), TradeSession.MaxSlots * 2);
        for (var i = 0; i < count; i++)
        {
            var name = pkg.ReadString();
            var amount = pkg.ReadInt();
            var durability = pkg.ReadSingle();
            var quality = pkg.ReadInt();
            var variant = pkg.ReadInt();
            var crafterId = pkg.ReadLong();
            var crafterName = pkg.ReadString();
            var customCount = pkg.ReadInt();
            var custom = new Dictionary<string, string>();
            for (var c = 0; c < customCount; c++)
                custom[pkg.ReadString()] = pkg.ReadString();

            var item = Make(name, amount);
            if (item == null)
                continue;
            item.Item.m_durability = durability;
            item.Item.m_quality = Math.Max(1, quality);
            item.Item.m_variant = variant;
            item.Item.m_crafterID = crafterId;
            item.Item.m_crafterName = crafterName;
            item.Item.m_customData = custom;
            entries.Add(item);
        }
        return entries;
    }

    /// <summary>A fresh item from its prefab, <paramref name="amount"/> of it (at most one stack).</summary>
    public static TradeEntry? Make(string prefabName, int amount)
    {
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
            return null;
        var item = drop.m_itemData.Clone();
        item.m_dropPrefab = prefab;
        item.m_stack = Math.Max(1, Math.Min(amount, Math.Max(1, item.m_shared.m_maxStackSize)));
        item.m_quality = 1;
        item.m_durability = item.GetMaxDurability();
        item.m_customData = new Dictionary<string, string>();
        return new TradeEntry(item, item.m_stack);
    }
}
