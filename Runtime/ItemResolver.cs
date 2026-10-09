using UnityEngine;

namespace DrakesItemShop.Runtime;

internal static class ItemResolver
{
    public const string CoinItemName = "Coins";

    public static GameObject? GetItemPrefab(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || ObjectDB.instance == null)
            return null;

        string id = itemId.Trim();

        if (id.StartsWith("drake_forge:", System.StringComparison.OrdinalIgnoreCase))
            id = id.Substring("drake_forge:".Length);

        try
        {
            return ObjectDB.instance.GetItemPrefab(id);
        }
        catch
        {
            return null;
        }
    }

    public static ItemDrop? GetItemDrop(string itemId)
    {
        var prefab = GetItemPrefab(itemId);
        return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
    }

    public static string GetDisplayName(string itemId)
    {
        var drop = GetItemDrop(itemId);
        if (drop?.m_itemData?.m_shared == null)
            return itemId;

        return global::Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
    }

    public static Sprite? GetItemIcon(string itemId)
    {
        var drop = GetItemDrop(itemId);
        return drop?.m_itemData?.GetIcon();
    }
}
