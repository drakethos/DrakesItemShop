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
        loc.AddToken("drake_display_case_name", "Display Case");
        loc.AddToken("drake_display_case_desc", "Glass case to show your wares (6 slots). Stock it from the door side.");
        loc.AddToken("drake_display_case_small_name", "Small Display Case");
        loc.AddToken("drake_display_case_small_desc", "Small glass case to show your wares (3 slots). Stock it from the door side.");
        loc.AddToken("drake_display_mat_name", "Display Mat");
        loc.AddToken("drake_display_mat_desc", "A jute runner in an iron-bound tray (6 slots). Lay it on a table: what you put in it lies on show.");
        loc.AddToken("drake_display_mat_small_name", "Small Display Mat");
        loc.AddToken("drake_display_mat_small_desc", "A small jute runner in an iron-bound tray (3 slots). Lay it on a table: what you put in it lies on show.");
        loc.AddToken("drake_armor_rack_name", "Armor Rack");
        loc.AddToken("drake_armor_rack_desc", "Finewood rack to hang armor on (6 slots: hooks on top, pegs below). Armor only.");
        loc.AddToken("drake_armor_rack_small_name", "Small Armor Rack");
        loc.AddToken("drake_armor_rack_small_desc", "Small finewood rack to hang armor on (4 slots). Armor only.");
        loc.AddToken("drake_wall_rack_name", "Wall Rack");
        loc.AddToken("drake_mailbox_name", "Mailbox");
        loc.AddToken("drake_mailbox_desc", "A Norse post box. Anyone can send items through the front slot; only those allowed to open chests here can open the back.");
        loc.AddToken("drake_dropbox_name", "Drop Box");
        loc.AddToken("drake_pobox_name", "PO Box");
        loc.AddToken("drake_pobox_desc", "A lockable post box, built into a post wall. Anyone may post mail through the slot; only its owner (and whoever they add) can open the door.");
        loc.AddToken("drake_postwall_1x1_name", "Post Wall (1 box)");
        loc.AddToken("drake_postwall_1x1_desc", "A single PO box in its own cubby. Snaps beside or on top of other post wall pieces.");
        loc.AddToken("drake_postwall_1x3_name", "Post Wall Column (3 boxes)");
        loc.AddToken("drake_postwall_1x3_desc", "Three PO boxes stacked in a column. Line columns and rows up side by side to build a post hall.");
        loc.AddToken("drake_postwall_3x1_name", "Post Wall Row (3 boxes)");
        loc.AddToken("drake_postwall_3x1_desc", "Three PO boxes side by side. Stack rows for a taller wall.");
        loc.AddToken("drake_postwall_side1_name", "Post Wall Side (1 tall)");
        loc.AddToken("drake_postwall_side1_desc", "Finishes the end of a post wall one box tall. Rotate it for the other end.");
        loc.AddToken("drake_postwall_side3_name", "Post Wall Side (3 tall)");
        loc.AddToken("drake_postwall_side3_desc", "Finishes the end of a post wall three boxes tall. Rotate it for the other end.");
        loc.AddToken("drake_postwall_roof1_name", "Post Wall Roof (1 wide)");
        loc.AddToken("drake_postwall_roof1_desc", "Roof for one box width of post wall.");
        loc.AddToken("drake_postwall_roof3_name", "Post Wall Roof (3 wide)");
        loc.AddToken("drake_postwall_roof3_desc", "Roof for three box widths of post wall.");
        loc.AddToken("drake_postwall_roofend_name", "Post Wall Roof End");
        loc.AddToken("drake_postwall_roofend_desc", "The gable end of a post wall roof, with crossed horns. Rotate it for the other end.");
        loc.AddToken("drake_dropbox_desc", "A slotted drop box for a table or counter. Customers send items through the lid; the owner opens the back.");
        loc.AddToken("drake_trade_dummy_name", "Trade Chest");
        loc.AddToken("drake_trade_dummy_desc", "A chest to test the trade menu with. E opens it; Shift+E opens the trade menu.");
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
