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

        var item = new CustomItem(ShopItemNames.Ledger, "Scroll", config);
        ItemManager.Instance.AddItem(item);
        ItemShopPlugin.LogSource?.LogInfo($"Registered item {ShopItemNames.Ledger}");
    }
}
