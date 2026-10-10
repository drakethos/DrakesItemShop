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
        loc.AddToken("drake_armor_rack_name", "Armor Rack");
        loc.AddToken("drake_armor_rack_desc", "Finewood rack to hang armor on (6 slots: hooks on top, pegs below). Armor only.");
        loc.AddToken("drake_armor_rack_small_name", "Small Armor Rack");
        loc.AddToken("drake_armor_rack_small_desc", "Small finewood rack to hang armor on (4 slots). Armor only.");
        loc.AddToken("drake_wall_rack_name", "Wall Rack");
        loc.AddToken("drake_trade_dummy_name", "Trade Dummy");
        loc.AddToken("drake_trade_dummy_desc", "A mannequin to test trading with. Press E or T on it to trade; Shift+E makes it ask you.");
        loc.AddToken("drake_wall_rack_desc", "Row of iron hooks on a finewood board (5 slots) for armor, weapons and shields.");
        loc.AddToken("drake_wall_rack_small_name", "Small Wall Rack");
        loc.AddToken("drake_wall_rack_small_desc", "Row of iron hooks on a finewood board (3 slots) for armor, weapons and shields.");
        loc.AddToken("drake_weapon_rack_name", "Weapon Rack");
        loc.AddToken("drake_weapon_rack_desc", "Finewood rack to hang weapons and shields on (6 slots). Weapons only.");
        loc.AddToken("drake_weapon_rack_small_name", "Small Weapon Rack");
        loc.AddToken("drake_weapon_rack_small_desc", "Small finewood rack to hang weapons and shields on (4 slots). Weapons only.");
        loc.AddToken("drake_rack_armor_only", "Only armor goes on an armor rack");
        loc.AddToken("drake_rack_weapons_only", "Only weapons and shields go on a weapon rack");
        loc.AddToken("drake_rack_gear_only", "Only armor, weapons and shields go on a wall rack");
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
