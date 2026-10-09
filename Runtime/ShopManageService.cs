using DrakeModsLibs.API;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using DrakesItemShop.Schema;
using DrakesItemShop.UI;
using UnityEngine;

namespace DrakesItemShop.Runtime;

internal static class ShopManageService
{
    public static bool TryOpen(Player player, MerchantBoxBehaviour box)
    {
        if (player == null || box == null)
            return false;

        if (!CanManagePiece(player, box.GetShopId(), box.GetOwnerUid()))
        {
            CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, "You cannot manage this shop.");
            return false;
        }

        ShopManagePanel.Open(new ShopManageAnchor { Box = box, Sign = null });
        return true;
    }

    public static bool TryOpen(Player player, ShopSignBehaviour sign)
    {
        if (player == null || sign == null)
            return false;

        if (!CanManagePiece(player, sign.GetShopId(), 0))
        {
            CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, "You cannot manage this shop sign.");
            return false;
        }

        ShopManagePanel.Open(new ShopManageAnchor { Box = null, Sign = sign });
        return true;
    }

    public static bool TryLinkLedger(Player player, MerchantBoxBehaviour box)
    {
        if (player == null || box == null)
            return false;

        if (!CanManagePiece(player, box.GetShopId(), box.GetOwnerUid()))
        {
            CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, "You cannot manage this shop.");
            return false;
        }

        var ledger = FindLedgerInInventory(player.GetInventory());
        if (ledger == null)
        {
            CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, "Put a Shop Ledger in your inventory first.");
            return false;
        }

        ShopLinkService.LinkLedgerToBox(ledger, box, player);
        return true;
    }

    private static bool CanManagePiece(Player player, string shopId, long ownerUid)
    {
        if (ShopRegistry.Instance.TryGetShop(shopId, out var shop))
            return ShopPermissions.CanManage(player, shop, ownerUid);

        return ShopPermissions.IsElevated(player) || ownerUid == 0 || player.GetPlayerID() == ownerUid;
    }

    public static ItemDrop.ItemData? FindLedgerInInventory(Inventory? inventory)
    {
        if (inventory == null)
            return null;

        foreach (var item in inventory.GetAllItems())
        {
            if (item?.m_shared == null)
                continue;

            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (prefab == ShopItemNames.Ledger || item.m_shared.m_name.Contains(ShopItemNames.Ledger))
                return item;
        }

        return null;
    }
}
