using DrakeModsLibs.API;
using DrakesItemShop.Data;

using DrakesItemShop.Npc;

using DrakesItemShop.Schema;

using DrakesItemShop.UI;

using UnityEngine;



namespace DrakesItemShop.Runtime;



internal static class ShopOpener

{

    public static void TryOpenFromMerchantBox(Player player, MerchantBoxBehaviour box)

    {

        if (player == null || box == null)

            return;



        string shopId = box.GetShopId();

        if (!ShopRegistry.Instance.TryGetShop(shopId, out var shop))

        {

            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, $"Unknown shop: {shopId}");

            return;

        }



        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);

        if (catalog == null)

            return;



        OpenSession(shop, catalog, ShopAnchorType.MerchantBox, box.transform.position, box.m_nview,

            box.GetStockContainer(), box.GetOwnerUid(), box.GetDispenseInventory(), box.GetTreasuryInventory(),

            box.GetSellHoldInventory());

    }



    public static void TryOpenFromLedger(Player player, ItemDrop.ItemData ledgerItem)

    {

        if (player == null || ledgerItem?.m_customData == null)

            return;



        if (!ledgerItem.m_customData.TryGetValue(ShopItemKeys.ShopId, out var shopId) ||

            string.IsNullOrWhiteSpace(shopId))

        {

            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, "Ledger has no shop id.");

            return;

        }



        if (!ShopRegistry.Instance.TryGetShop(shopId, out var shop))

            return;



        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);

        if (catalog == null)

            return;



        Vector3 anchorPos = player.transform.position;

        Container? stock = null;

        Inventory? dispense = null;

        Inventory? treasury = null;

        Inventory? sellHold = null;

        var anchorType = ShopAnchorType.Ledger;

        long ownerUid = player.GetPlayerID();



        if (ledgerItem.m_customData.TryGetValue(ShopItemKeys.LinkBox, out var linkRaw) &&

            TryParseZdoId(linkRaw, out var boxId) &&

            ZNetScene.instance?.FindInstance(boxId)?.GetComponent<MerchantBoxBehaviour>() is { } linked)

        {

            anchorPos = linked.transform.position;

            stock = linked.GetStockContainer();

            dispense = linked.GetDispenseInventory();

            treasury = linked.GetTreasuryInventory();

            sellHold = linked.GetSellHoldInventory();

            ownerUid = linked.GetOwnerUid();

            anchorType = ShopAnchorType.MerchantBox;

        }

        else if (ledgerItem.m_customData.TryGetValue(ShopItemKeys.Portable, out var portable) &&

                 (portable == "1" || portable.Equals("true", System.StringComparison.OrdinalIgnoreCase)))

        {

            stock = LedgerContainerHost.GetOrCreate(player, ledgerItem);

        }



        OpenSession(shop, catalog, anchorType, anchorPos, null, stock, ownerUid, dispense, treasury, sellHold);

    }



    public static void TryOpenFromNpc(Player player, SilentShopTrader trader)

    {

        if (player == null || trader == null)

            return;



        if (!ShopRegistry.Instance.TryGetShop(trader.ShopId, out var shop))

            return;



        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);

        if (catalog == null && !string.IsNullOrWhiteSpace(trader.CatalogId))

            ShopRegistry.Instance.TryGetCatalog(trader.CatalogId, out catalog!);



        if (catalog == null)

            return;



        OpenSession(shop, catalog, ShopAnchorType.NpcTrader, trader.transform.position, null, null, 0, null, null, null);

    }



    private static void OpenSession(

        ShopDefinitionDto shop,

        CatalogDto catalog,

        ShopAnchorType anchorType,

        Vector3 anchorPos,

        ZNetView? netView,

        Container? stock,

        long ownerUid,

        Inventory? dispense,

        Inventory? treasury,

        Inventory? sellHold)

    {

        var session = new ShopSession

        {

            Shop = shop,

            Catalog = catalog,

            AnchorType = anchorType,

            AnchorPosition = anchorPos,

            NetView = netView,

            StockContainer = stock,

            DispenseInventory = dispense,

            TreasuryInventory = treasury,

            SellHoldInventory = sellHold,

            OwnerUid = ownerUid,

        };



        ShopSessionState.Current = session;

        ShopPanel.Open(session);

    }



    private static bool TryParseZdoId(string raw, out ZDOID id)

    {

        id = ZDOID.None;

        if (string.IsNullOrWhiteSpace(raw))

            return false;



        var parts = raw.Split(',');

        if (parts.Length >= 2 &&

            ulong.TryParse(parts[0].Trim(), out var userId) &&

            uint.TryParse(parts[1].Trim(), out var idPart))

        {

            id = new ZDOID((long)userId, idPart);

            return true;

        }



        return false;

    }

}


