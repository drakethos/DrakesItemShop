using System.Collections.Generic;
using DrakesItemShop.Runtime;
using DrakesItemShop.Schema;

namespace DrakesItemShop.Npc;

internal static class TraderCatalogBuilder
{
    public static void ApplyCatalog(Trader trader, CatalogDto catalog)
    {
        if (trader == null || catalog?.Entries == null)
            return;

        var tradeItems = new List<Trader.TradeItem>();
        foreach (var entry in catalog.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Item))
                continue;

            var drop = ItemResolver.GetItemDrop(entry.Item);
            if (drop == null)
                continue;

            int price = entry.BuyPrice?.ResolveCoinPrice() ?? 0;
            if (price <= 0)
                price = (int)(drop.m_itemData.m_shared.m_value * 1.2f);

            tradeItems.Add(new Trader.TradeItem
            {
                m_prefab = drop,
                m_stack = 1,
                m_price = price,
                m_requiredGlobalKey = "",
            });
        }

        trader.m_items = tradeItems;
    }
}
