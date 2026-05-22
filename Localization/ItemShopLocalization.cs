using Jotunn.Managers;

namespace DrakesItemShop.Localization;

internal static class ItemShopLocalization
{
    public static void Init()
    {
        var loc = LocalizationManager.Instance;
        loc.AddToken("drake_merchant_box_name", "Merchant Box");
        loc.AddToken("drake_merchant_box_desc", "Placeable shop stock container. Interact to trade; hold Alt to open storage.");
        loc.AddToken("drake_shop_sign_name", "Shop Sign");
        loc.AddToken("drake_shop_sign_desc", "Displays prices from linked shop catalog.");
        loc.AddToken("drake_shop_ledger_name", "Shop Ledger");
        loc.AddToken("drake_shop_ledger_desc", "Opens a linked or portable shop. Bind to a merchant box via manage UI.");
    }
}
