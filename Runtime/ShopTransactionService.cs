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

    DispenseFull,

    TreasuryEmpty,

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



        if (!HasStock(session, entry, quantity))

            return TransactionResult.OutOfStock;



        var drop = ItemResolver.GetItemDrop(itemId);

        if (drop == null)

            return TransactionResult.InvalidItem;



        if (session.UsesVendingMachine)

            return TryBuyVending(player, session, entry, drop, itemId, quantity, unitPrice);



        return TryBuyDirect(player, session, entry, drop, quantity, unitPrice);

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



        if (session.UsesVendingMachine)

            return TrySellVending(player, session, entry, itemId, quantity, unitSell);



        return TrySellDirect(player, session, entry, itemId, quantity, unitSell);

    }



    private static TransactionResult TryBuyDirect(

        Player player,

        ShopSession session,

        CatalogEntryDto entry,

        ItemDrop drop,

        int quantity,

        int unitPrice)

    {

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



        if (!player.GetInventory().CanAddItem(drop.m_itemData, quantity))

            return TransactionResult.InventoryFull;



        CollectPaymentFromPlayer(player, entry, quantity, totalCoins);

        RemoveStock(session, entry, quantity);



        for (int i = 0; i < quantity; i++)

            player.GetInventory().AddItem(drop.m_itemData.Clone());



        ShopPriceTagger.SetMarketPrice(drop.m_itemData, unitPrice);

        return TransactionResult.Ok;

    }



    private static TransactionResult TryBuyVending(

        Player player,

        ShopSession session,

        CatalogEntryDto entry,

        ItemDrop drop,

        string itemId,

        int quantity,

        int unitPrice)

    {

        int totalCoins = unitPrice * quantity;



        if (!VendingInventoryHelper.CanAdd(session.DispenseInventory, drop.m_itemData, quantity))

            return TransactionResult.DispenseFull;



        if (entry.BuyPrice?.IsItemCurrency == true)

        {

            string cur = entry.BuyPrice.Item!;

            int need = (entry.BuyPrice.Amount ?? 1) * quantity;

            if (player.GetInventory().CountItems(cur) < need)

                return TransactionResult.NotEnoughCurrency;



            if (!VendingInventoryHelper.CanAdd(session.TreasuryInventory, ItemResolver.GetItemDrop(cur)!.m_itemData, need))

                return TransactionResult.TreasuryEmpty;

        }

        else if (player.GetInventory().CountItems(ItemResolver.CoinItemName) < totalCoins)

        {

            return TransactionResult.NotEnoughCurrency;

        }

        else if (!HasTreasuryCoinSpace(session, totalCoins))

        {

            return TransactionResult.TreasuryEmpty;

        }



        CollectPaymentFromPlayer(player, entry, quantity, totalCoins);

        DepositPaymentToTreasury(session, entry, quantity, totalCoins);

        RemoveStock(session, entry, quantity);



        if (!VendingInventoryHelper.TryAdd(session.DispenseInventory, drop.m_itemData, quantity))

            return TransactionResult.DispenseFull;



        MarkVendingDirty(session);

        ShopPriceTagger.SetMarketPrice(drop.m_itemData, unitPrice);

        return TransactionResult.Ok;

    }



    private static TransactionResult TrySellDirect(

        Player player,

        ShopSession session,

        CatalogEntryDto entry,

        string itemId,

        int quantity,

        int unitSell)

    {

        RemoveItemFromPlayer(player, itemId, quantity);

        int totalCoins = unitSell * quantity;

        PayPlayerFromShop(player, entry, quantity, totalCoins);

        AddStockToContainer(session, itemId, quantity);

        return TransactionResult.Ok;

    }



    private static TransactionResult TrySellVending(

        Player player,

        ShopSession session,

        CatalogEntryDto entry,

        string itemId,

        int quantity,

        int unitSell)

    {

        var soldDrop = ItemResolver.GetItemDrop(itemId);

        if (soldDrop == null)

            return TransactionResult.InvalidItem;



        if (!VendingInventoryHelper.CanAdd(session.SellHoldInventory, soldDrop.m_itemData, quantity))

            return TransactionResult.InventoryFull;



        if (entry.SellPrice?.IsItemCurrency == true)

        {

            string cur = entry.SellPrice.Item!;

            int need = (entry.SellPrice.Amount ?? 1) * quantity;

            if (VendingInventoryHelper.CountItems(session.TreasuryInventory, cur) < need)

                return TransactionResult.TreasuryEmpty;

        }

        else

        {

            int totalCoins = unitSell * quantity;

            if (VendingInventoryHelper.CountItems(session.TreasuryInventory, ItemResolver.CoinItemName) < totalCoins)

                return TransactionResult.TreasuryEmpty;

        }



        RemoveItemFromPlayer(player, itemId, quantity);



        if (!VendingInventoryHelper.TryAdd(session.SellHoldInventory, soldDrop.m_itemData, quantity))

            return TransactionResult.InventoryFull;



        int totalCoinPay = unitSell * quantity;

        if (!PayPlayerFromTreasury(session, player, entry, quantity, totalCoinPay))

            return TransactionResult.TreasuryEmpty;



        MarkVendingDirty(session);

        return TransactionResult.Ok;

    }



    private static void CollectPaymentFromPlayer(Player player, CatalogEntryDto entry, int quantity, int totalCoins)

    {

        if (entry.BuyPrice?.IsItemCurrency == true)

            RemoveItemFromPlayer(player, entry.BuyPrice.Item!, (entry.BuyPrice.Amount ?? 1) * quantity);

        else

            RemoveItemFromPlayer(player, ItemResolver.CoinItemName, totalCoins);

    }



    private static void DepositPaymentToTreasury(ShopSession session, CatalogEntryDto entry, int quantity, int totalCoins)

    {

        if (session.TreasuryInventory == null)

            return;



        if (entry.BuyPrice?.IsItemCurrency == true)

        {

            var curDrop = ItemResolver.GetItemDrop(entry.BuyPrice.Item!);

            if (curDrop != null)

                VendingInventoryHelper.TryAdd(session.TreasuryInventory, curDrop.m_itemData, (entry.BuyPrice.Amount ?? 1) * quantity);

            return;

        }



        var coinDrop = ItemResolver.GetItemDrop(ItemResolver.CoinItemName);

        if (coinDrop != null)

            VendingInventoryHelper.TryAdd(session.TreasuryInventory, coinDrop.m_itemData, totalCoins);

    }



    private static void PayPlayerFromShop(Player player, CatalogEntryDto entry, int quantity, int totalCoins)

    {

        if (entry.SellPrice?.IsItemCurrency == true)

        {

            var curDrop = ItemResolver.GetItemDrop(entry.SellPrice.Item!);

            if (curDrop != null)

            {

                for (int i = 0; i < (entry.SellPrice.Amount ?? 1) * quantity; i++)

                    player.GetInventory().AddItem(curDrop.m_itemData.Clone());

            }

            return;

        }



        var coinDrop = ItemResolver.GetItemDrop(ItemResolver.CoinItemName);

        if (coinDrop != null)

        {

            for (int i = 0; i < totalCoins; i++)

                player.GetInventory().AddItem(coinDrop.m_itemData.Clone());

        }

    }



    private static bool PayPlayerFromTreasury(ShopSession session, Player player, CatalogEntryDto entry, int quantity, int totalCoins)

    {

        if (session.TreasuryInventory == null)

            return false;



        if (entry.SellPrice?.IsItemCurrency == true)

        {

            string cur = entry.SellPrice.Item!;

            int need = (entry.SellPrice.Amount ?? 1) * quantity;

            if (VendingInventoryHelper.CountItems(session.TreasuryInventory, cur) < need)

                return false;



            VendingInventoryHelper.RemoveItems(session.TreasuryInventory, cur, need);

            var curDrop = ItemResolver.GetItemDrop(cur);

            if (curDrop == null)

                return false;



            for (int i = 0; i < need; i++)

                player.GetInventory().AddItem(curDrop.m_itemData.Clone());



            return true;

        }



        if (VendingInventoryHelper.CountItems(session.TreasuryInventory, ItemResolver.CoinItemName) < totalCoins)

            return false;



        VendingInventoryHelper.RemoveItems(session.TreasuryInventory, ItemResolver.CoinItemName, totalCoins);

        var coinDrop = ItemResolver.GetItemDrop(ItemResolver.CoinItemName);

        if (coinDrop == null)

            return false;



        for (int i = 0; i < totalCoins; i++)

            player.GetInventory().AddItem(coinDrop.m_itemData.Clone());



        return true;

    }



    private static bool HasTreasuryCoinSpace(ShopSession session, int totalCoins)

    {

        if (session.TreasuryInventory == null)

            return false;



        var coinDrop = ItemResolver.GetItemDrop(ItemResolver.CoinItemName);

        if (coinDrop == null)

            return false;



        return session.TreasuryInventory.CanAddItem(coinDrop.m_itemData, totalCoins);

    }



    private static void MarkVendingDirty(ShopSession session)

    {

        if (session.NetView == null)

            return;



        session.NetView.GetComponent<MerchantBoxBehaviour>()?.NotifyVendingChanged();

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

            VendingInventoryHelper.RemoveOneStack(inv, drop.m_itemData);

    }



    private static void RemoveItemFromPlayer(Player player, string itemName, int amount)

    {

        var drop = ItemResolver.GetItemDrop(itemName);

        if (drop == null)

            return;



        var inv = player.GetInventory();

        for (int i = 0; i < amount; i++)

            VendingInventoryHelper.RemoveOneStack(inv, drop.m_itemData);

    }

}


