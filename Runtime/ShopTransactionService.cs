using DrakesItemShop.Config;
using DrakesItemShop.Integration;
using DrakesItemShop.Schema;
using UnityEngine;

namespace DrakesItemShop.Runtime;

internal enum TransactionResult
{
    Ok,
    Denied,
    InvalidItem,
    OutOfStock,
    NotEnoughCurrency,
    InventoryFull,
    SellDisabled,
    NoSellPrice,
}

internal static class ShopTransactionService
{
    public static TransactionResult TryBuy(Player player, ShopSession session, string itemId, int quantity)
    {
        if (!ItemShopConfig.Enabled || player == null || session == null || quantity < 1)
            return TransactionResult.Denied;

        if (!WardAccessRouter.CanAccess(player, session.AnchorPosition, session.Shop.Ward, ShopAction.Buy))
            return TransactionResult.Denied;

        if (!ShopPermissions.CanOpen(player, session.Shop, session.OwnerUid))
            return TransactionResult.Denied;

        var entry = session.Catalog.Entries?.Find(e =>
            string.Equals(e.Item, itemId, System.StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return TransactionResult.InvalidItem;

        int unitPrice = entry.BuyPrice?.ResolveCoinPrice() ?? 0;
        if (unitPrice <= 0 && !(entry.BuyPrice?.IsItemCurrency ?? false))
            return TransactionResult.InvalidItem;

        int totalCoins = unitPrice * quantity;
        if (entry.BuyPrice?.IsItemCurrency == true)
        {
            string cur = entry.BuyPrice.Item!;
            int need = (entry.BuyPrice.Amount ?? 1) * quantity;
            if (player.GetInventory().CountItems(cur) < need)
                return TransactionResult.NotEnoughCurrency;
        }
        else if (player.GetInventory().CountItems(ItemResolver.CoinItemName) < totalCoins)
        {
            return TransactionResult.NotEnoughCurrency;
        }

        if (!HasStock(session, entry, quantity))
            return TransactionResult.OutOfStock;

        var drop = ItemResolver.GetItemDrop(itemId);
        if (drop == null)
            return TransactionResult.InvalidItem;

        if (!player.GetInventory().CanAddItem(drop.m_itemData, quantity))
            return TransactionResult.InventoryFull;

        if (entry.BuyPrice?.IsItemCurrency == true)
            RemoveItemFromPlayer(player, entry.BuyPrice.Item!, (entry.BuyPrice.Amount ?? 1) * quantity);
        else
            RemoveItemFromPlayer(player, ItemResolver.CoinItemName, totalCoins);

        RemoveStock(session, entry, quantity);

        for (int i = 0; i < quantity; i++)
            player.GetInventory().AddItem(drop.m_itemData.Clone());

        ShopPriceTagger.SetMarketPrice(drop.m_itemData, unitPrice);
        return TransactionResult.Ok;
    }

    public static TransactionResult TrySell(Player player, ShopSession session, string itemId, int quantity)
    {
        if (!ItemShopConfig.Enabled || !ItemShopConfig.EnableSellBack || player == null || session == null || quantity < 1)
            return TransactionResult.SellDisabled;

        if (!WardAccessRouter.CanAccess(player, session.AnchorPosition, session.Shop.Ward, ShopAction.Sell))
            return TransactionResult.Denied;

        var entry = session.Catalog.Entries?.Find(e =>
            string.Equals(e.Item, itemId, System.StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return TransactionResult.InvalidItem;

        int unitSell = entry.SellPrice?.ResolveCoinPrice() ?? 0;
        if (unitSell <= 0 && !(entry.SellPrice?.IsItemCurrency ?? false))
            return TransactionResult.NoSellPrice;

        if (player.GetInventory().CountItems(itemId) < quantity)
            return TransactionResult.NotEnoughCurrency;

        RemoveItemFromPlayer(player, itemId, quantity);
        int totalCoins = unitSell * quantity;

        if (entry.SellPrice?.IsItemCurrency == true)
        {
            var curDrop = ItemResolver.GetItemDrop(entry.SellPrice.Item!);
            if (curDrop != null)
            {
                for (int i = 0; i < (entry.SellPrice.Amount ?? 1) * quantity; i++)
                    player.GetInventory().AddItem(curDrop.m_itemData.Clone());
            }
        }
        else
        {
            var coinDrop = ItemResolver.GetItemDrop(ItemResolver.CoinItemName);
            if (coinDrop != null)
            {
                for (int i = 0; i < totalCoins; i++)
                    player.GetInventory().AddItem(coinDrop.m_itemData.Clone());
            }
        }

        AddStockToContainer(session, itemId, quantity);
        return TransactionResult.Ok;
    }

    private static bool HasStock(ShopSession session, CatalogEntryDto entry, int quantity)
    {
        if (UsesContainerStock(session))
        {
            int have = CountInContainer(session.StockContainer, entry.Item!);
            return have >= quantity;
        }

        if (entry.Stock < 0)
            return true;

        return entry.Stock >= quantity;
    }

    private static void RemoveStock(ShopSession session, CatalogEntryDto entry, int quantity)
    {
        if (UsesContainerStock(session))
            RemoveFromContainer(session.StockContainer, entry.Item!, quantity);
        else if (entry.Stock >= 0)
            entry.Stock -= quantity;
    }

    private static void AddStockToContainer(ShopSession session, string itemId, int quantity)
    {
        if (!UsesContainerStock(session) || session.StockContainer == null)
            return;

        var drop = ItemResolver.GetItemDrop(itemId);
        if (drop == null)
            return;

        for (int i = 0; i < quantity; i++)
            session.StockContainer.GetInventory().AddItem(drop.m_itemData.Clone());
    }

    private static bool UsesContainerStock(ShopSession session) =>
        session.StockContainer != null &&
        string.Equals(session.Shop.StockSource, "container", System.StringComparison.OrdinalIgnoreCase);

    private static int CountInContainer(Container? container, string itemName)
    {
        if (container == null)
            return 0;
        return container.GetInventory().CountItems(itemName);
    }

    private static void RemoveFromContainer(Container? container, string itemName, int amount)
    {
        if (container == null)
            return;

        var drop = ItemResolver.GetItemDrop(itemName);
        if (drop == null)
            return;

        var inv = container.GetInventory();
        for (int i = 0; i < amount; i++)
            RemoveOneStack(inv, drop.m_itemData);
    }

    private static void RemoveItemFromPlayer(Player player, string itemName, int amount)
    {
        var drop = ItemResolver.GetItemDrop(itemName);
        if (drop == null)
            return;

        var inv = player.GetInventory();
        for (int i = 0; i < amount; i++)
            RemoveOneStack(inv, drop.m_itemData);
    }

    private static void RemoveOneStack(Inventory inv, ItemDrop.ItemData template)
    {
        foreach (var item in inv.GetAllItems())
        {
            if (item.m_shared.m_name == template.m_shared.m_name)
            {
                inv.RemoveItem(item, 1);
                return;
            }
        }
    }
}
