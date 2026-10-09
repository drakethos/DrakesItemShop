using System.Collections.Generic;
using DrakesItemShop.Data;
using Jotunn.Entities;
using Jotunn.Managers;

namespace DrakesItemShop.Localization;

internal static class ItemShopLocalization
{
    public static void Init()
    {
        var loc = LocalizationManager.Instance;
        loc.AddToken("drake_merchant_box_name", "Merchant Box");
        loc.AddToken("drake_merchant_box_desc", "Vending shop. Owner: E stock, Shift+E shop, Alt+E manage. Customer: E pickup slot, Shift+E buy/sell.");
        loc.AddToken("drake_vend_dispense", "Dispense slot");
        loc.AddToken("drake_vend_treasury", "Treasury");
        loc.AddToken("drake_vend_sellhold", "Purchased goods");
        loc.AddToken("drake_display_case_name", "Display Case");
        loc.AddToken("drake_display_case_desc", "Glass case to show your wares (6 slots). Stock it from the door side.");
        loc.AddToken("drake_display_case_small_name", "Small Display Case");
        loc.AddToken("drake_display_case_small_desc", "Small glass case to show your wares (3 slots). Stock it from the door side.");
        loc.AddToken("drake_shop_sign_name", "Shop Sign");
        loc.AddToken("drake_shop_sign_desc", "Price sign. Shift+E picks shop id from your JSON shops.");
        loc.AddToken("drake_shop_ledger_name", "Shop Ledger");
        loc.AddToken("drake_shop_ledger_desc", "Use from inventory to open its shop. Link at a Merchant Box with Alt+E, or shop_give_ledger <shopId>.");
        loc.AddToken("drake_shop_npc_spawner_name", "Shop NPC Spawner");
        loc.AddToken("drake_shop_npc_spawner_desc", "Interact to spawn the configured silent trader NPC.");

        RegisterCategoryTranslations();
    }

    private static void RegisterCategoryTranslations()
    {
        CustomLocalization custom = LocalizationManager.Instance.GetLocalization();

        custom.AddTranslation("English", new Dictionary<string, string>
        {
            { ShopPieceCategories.DrakeShopLocalizationKey, "Drake Shop" },
        });

        custom.AddTranslation("German", new Dictionary<string, string>
        {
            { ShopPieceCategories.DrakeShopLocalizationKey, "Drake Shop" },
        });
    }
}
