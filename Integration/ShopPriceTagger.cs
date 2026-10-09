using DrakeModsLibs.Data;

namespace DrakesItemShop.Integration;

internal static class ShopPriceTagger
{
    public static void SetMarketPrice(ItemDrop.ItemData item, int coinPrice)
    {
        if (item.m_customData == null)
            item.m_customData = new System.Collections.Generic.Dictionary<string, string>();

        if (coinPrice <= 0)
            item.m_customData.Remove(DrakeCustomDataKeys.MarketPrice);
        else
            item.m_customData[DrakeCustomDataKeys.MarketPrice] = $"{coinPrice}g";
    }
}
