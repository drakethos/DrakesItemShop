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
        loc.AddToken("drake_display_mat_wolf_name", "Wolf Display Mat");
        loc.AddToken("drake_display_mat_wolf_desc", "A wolf-pelt runner in an iron-bound tray (4 slots). Same size as the jute tray, fewer goods.");
        loc.AddToken("drake_display_mat_lox_name", "Lox Display Mat");
        loc.AddToken("drake_display_mat_lox_desc", "A lox-pelt runner in an iron-bound tray (4 slots). Same size as the jute tray, fewer goods.");
        loc.AddToken("drake_display_mat_wolf_small_name", "Small Wolf Display Mat");
        loc.AddToken("drake_display_mat_wolf_small_desc", "A small wolf-pelt runner in an iron-bound tray (2 slots).");
        loc.AddToken("drake_display_mat_lox_small_name", "Small Lox Display Mat");
        loc.AddToken("drake_display_mat_lox_small_desc", "A small lox-pelt runner in an iron-bound tray (2 slots).");
        loc.AddToken("drake_armor_rack_name", "Armor Rack");
        loc.AddToken("drake_armor_rack_desc", "Finewood rack to hang armor on (6 slots: hooks on top, pegs below). Armor only.");
        loc.AddToken("drake_armor_rack_small_name", "Small Armor Rack");
        loc.AddToken("drake_armor_rack_small_desc", "Small finewood rack to hang armor on (4 slots). Armor only.");
        loc.AddToken("drake_wall_rack_name", "Wall Rack");
        loc.AddToken("drake_mailbox_name", "Mailbox");
        loc.AddToken("drake_mailbox_desc", "A Norse post box. Anyone can send items through the front slot; only those allowed to open chests here can open the back.");
        loc.AddToken("drake_dropbox_name", "Drop Box");
        loc.AddToken("drake_jutemat_name", "Jute Mat");
        loc.AddToken("drake_jutemat_desc", "A woven mat for free layout (12 stacks). Shift+E, Layout tab: drag, turn, lift and size goods on the board.");
        loc.AddToken("drake_wolfmat_name", "Wolf Mat");
        loc.AddToken("drake_wolfmat_desc", "A wolf-pelt mat for free layout (6 stacks). Same tools as the jute mat; leather holds less.");
        loc.AddToken("drake_loxmat_name", "Lox Mat");
        loc.AddToken("drake_loxmat_desc", "A lox-pelt mat for free layout (6 stacks). Same tools as the jute mat; leather holds less.");
        loc.AddToken("drake_deermat_name", "Deer Mat");
        loc.AddToken("drake_deermat_desc", "A deer-hide mat for free layout (4 stacks). Smaller than the jute mat; hover matches the hide.");
        loc.AddToken("drake_opencrate_name", "Open Crate");
        loc.AddToken("drake_opencrate_desc", "Open crate 1×1×1 (2×2 slots). Put goods in and their models show inside.");
        loc.AddToken("drake_opencrate_smalllow_name", "Open Crate (Small Low)");
        loc.AddToken("drake_opencrate_smalllow_desc", "Half-height crate 1×1×½ (2×2 slots). Stock shows as models inside.");
        loc.AddToken("drake_opencrate_biglow_name", "Open Crate (Long)");
        loc.AddToken("drake_opencrate_biglow_desc", "Long low crate 2×1×½ (4×2 slots). Stock shows as models inside.");
        loc.AddToken("drake_opencrate_tall_name", "Open Crate (Tall)");
        loc.AddToken("drake_opencrate_tall_desc", "Open crate 1×1×1½ (2×4 slots). Stock stacks up as models inside.");
        loc.AddToken("drake_storesign_name", "Store Sign");
        loc.AddToken("drake_storesign_desc", "A bracket sign on chains. Press E to write on it: rich text, fonts, colours and outline, on both faces.");
        foreach (var apron in Apron.ApronStyles.All)
        {
            loc.AddToken(apron.PrefabName.ToLowerInvariant() + "_name", apron.Name);
            loc.AddToken(apron.PrefabName.ToLowerInvariant() + "_desc", apron.Description);
        }
        loc.AddToken("drake_shopapron_effect", "Open for business");
        loc.AddToken("drake_shopapron_effect_tip", "A shopkeeper carries a little more and tires less indoors.");
        loc.AddToken("drake_opensign_name", "Open Sign");
        loc.AddToken("drake_opensign_desc", "A door sign that flips between OPEN and CLOSED when you press E.");
        loc.AddToken("drake_doorbell_name", "Door Bell");
        loc.AddToken("drake_doorbell_desc", "A bell on a bracket by the door. When a nearby door opens it jingles and tells you \"A customer has arrived\".");
        loc.AddToken("drake_deskbell_name", "Desk Bell");
        loc.AddToken("drake_deskbell_desc", "Press E to ring for service. Everyone nearby hears it and sees who is waiting.");
        loc.AddToken("drake_tradecounter_name", "Trade Counter");
        loc.AddToken("drake_tradecounter_desc", "A two-sided counter for selling in person: shopkeeper behind it, customer in front. Press E to start a trade with whoever is across; a sale rings the till. The register chest holds stock and takings.");
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
