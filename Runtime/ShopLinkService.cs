using DrakeModsLibs.API;
using DrakesItemShop.Data;
using UnityEngine;

namespace DrakesItemShop.Runtime;

internal static class ShopLinkService
{
    public static void LinkLedgerToBox(ItemDrop.ItemData ledger, MerchantBoxBehaviour box, Player player)
    {
        if (ledger.m_customData == null)
            ledger.m_customData = new System.Collections.Generic.Dictionary<string, string>();

        string shopId = box.GetShopId();
        ledger.m_customData[ShopItemKeys.ShopId] = shopId;
        ledger.m_customData[ShopItemKeys.Portable] = "0";
        ledger.m_customData[ShopItemKeys.LedgerMode] = LedgerMode.Linked.ToString();
        var zdoId = box.m_nview.GetZDO()?.m_uid ?? ZDOID.None;
        ledger.m_customData[ShopItemKeys.LinkBox] = $"{zdoId.UserID},{zdoId.ID}";

        box.LinkLedger(player.GetComponent<ZNetView>());
        CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, $"Ledger linked to shop '{shopId}'.");
    }

    public static void SetPortableLedger(ItemDrop.ItemData ledger, string shopId)
    {
        if (ledger.m_customData == null)
            ledger.m_customData = new System.Collections.Generic.Dictionary<string, string>();

        ledger.m_customData[ShopItemKeys.ShopId] = shopId;
        ledger.m_customData[ShopItemKeys.Portable] = "1";
        ledger.m_customData[ShopItemKeys.LedgerMode] = LedgerMode.Portable.ToString();
        ledger.m_customData.Remove(ShopItemKeys.LinkBox);
    }
}
