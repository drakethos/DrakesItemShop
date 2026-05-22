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
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, $"Unknown shop: {shopId}");
            return;
        }

        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);
        if (catalog == null)
            return;

        var session = new ShopSession
        {
            Shop = shop,
            Catalog = catalog,
            AnchorType = ShopAnchorType.MerchantBox,
            AnchorPosition = box.transform.position,
            NetView = box.m_nview,
            StockContainer = box.GetContainer(),
            OwnerUid = box.GetOwnerUid(),
        };

        ShopSessionState.Current = session;
        ShopPanel.Open(session);
    }

    public static void TryOpenFromLedger(Player player, ItemDrop.ItemData ledgerItem)
    {
        if (player == null || ledgerItem?.m_customData == null)
            return;

        if (!ledgerItem.m_customData.TryGetValue(ShopItemKeys.ShopId, out var shopId) ||
            string.IsNullOrWhiteSpace(shopId))
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Ledger has no shop id.");
            return;
        }

        if (!ShopRegistry.Instance.TryGetShop(shopId, out var shop))
            return;

        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);
        if (catalog == null)
            return;

        Vector3 anchorPos = player.transform.position;
        Container? stock = null;
        var anchorType = ShopAnchorType.Ledger;

        if (ledgerItem.m_customData.TryGetValue(ShopItemKeys.LinkBox, out var linkRaw) &&
            TryParseZdoId(linkRaw, out var boxId) &&
            ZNetScene.instance?.FindInstance(boxId)?.GetComponent<MerchantBoxBehaviour>() is { } linked)
        {
            anchorPos = linked.transform.position;
            stock = linked.GetContainer();
            anchorType = ShopAnchorType.MerchantBox;
        }
        else if (ledgerItem.m_customData.TryGetValue(ShopItemKeys.Portable, out var portable) &&
                 (portable == "1" || portable.Equals("true", System.StringComparison.OrdinalIgnoreCase)))
        {
            stock = LedgerContainerHost.GetOrCreate(player, ledgerItem);
        }

        var session = new ShopSession
        {
            Shop = shop,
            Catalog = catalog,
            AnchorType = anchorType,
            AnchorPosition = anchorPos,
            StockContainer = stock,
            OwnerUid = player.GetPlayerID(),
        };

        ShopSessionState.Current = session;
        ShopPanel.Open(session);
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

        var session = new ShopSession
        {
            Shop = shop,
            Catalog = catalog,
            AnchorType = ShopAnchorType.NpcTrader,
            AnchorPosition = trader.transform.position,
            OwnerUid = 0,
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
