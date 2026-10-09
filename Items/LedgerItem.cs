using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace DrakesItemShop.Items;

internal static class LedgerItem
{
    public static void Register()
    {
        var config = new ItemConfig
        {
            Name = "$drake_shop_ledger_name",
            Description = "$drake_shop_ledger_desc",
            Weight = 0.5f,
            StackSize = 1,
        };

        var item = new CustomItem(ShopItemNames.Ledger, ShopVanillaPrefabs.Ledger, config);
        if (!item.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError(
                $"Failed to register {ShopItemNames.Ledger} — clone '{ShopVanillaPrefabs.Ledger}' invalid (no Scroll in Valheim; resolver uses LeatherScraps/Coins).");
            return;
        }

        ItemManager.Instance.AddItem(item);

        var drop = item.ItemDrop;
        if (drop != null)
        {
            drop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            drop.m_itemData.m_shared.m_consumeStatusEffect = null;
        }

        ItemShopPlugin.LogSource?.LogInfo($"Registered item {ShopItemNames.Ledger} (clone: {ShopVanillaPrefabs.Ledger})");
    }
}
