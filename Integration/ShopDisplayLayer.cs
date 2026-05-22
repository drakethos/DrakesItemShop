using DrakesWorkshopLibs.API;
using DrakesWorkshopLibs.Data;

namespace DrakesItemShop.Integration;

internal sealed class ShopDisplayLayer : IItemDisplayNameLayer
{
    public int Priority => DisplayNameLayerPriority.ShopOverlay;

    public bool TryApply(ItemDrop.ItemData? item, ref string displayName)
    {
        if (item?.m_customData == null)
            return false;

        if (!item.m_customData.TryGetValue(DrakeCustomDataKeys.MarketPrice, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
            return false;

        string suffix = raw.Trim().StartsWith("—", System.StringComparison.Ordinal)
            ? raw.Trim()
            : $"— {raw.Trim()}";

        if (string.IsNullOrEmpty(displayName))
            return false;

        displayName = $"{displayName} {suffix}";
        return true;
    }
}
