using BepInEx.Logging;
using DrakesItemShop.Commands;
using DrakesItemShop.Integration;
using DrakesItemShop.Items;
using DrakesItemShop.Localization;
using DrakesItemShop.Networking;
using DrakesItemShop.Pieces;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;
using Jotunn;
using Jotunn.Managers;

namespace DrakesItemShop.Runtime;

internal static class ItemShopRuntime
{
    private static bool _initialized;

    public static void Initialize(ManualLogSource log)
    {
        if (_initialized)
            return;

        ItemShopPaths.EnsureDirectories();
        WriteExampleConfigsIfMissing();
        ItemShopLocalization.Init();
        ShopRegistry.Instance.Reload(log);
        ShopCommands.Register();

        CustomizeLibsAPI.RegisterDisplayNameLayer(new ShopDisplayLayer());
        CustomizeLibsAPI.RegisterModCustomDataFields(
            DrakeCustomDataCatalog.ModItemShop,
            DrakeCustomDataCatalog.GetFieldsForMod(DrakeCustomDataCatalog.ModItemShop));

        _initialized = true;
    }

    public static void RegisterAssets()
    {
        ShopPieceRegistrar.RegisterAll();
        LedgerItem.Register();
        SignCatalogRenderer.RefreshAllSigns();
    }

    private static void WriteExampleConfigsIfMissing()
    {
        string catPath = System.IO.Path.Combine(ItemShopPaths.CatalogsDirectory, "blacksmith.json");
        if (!System.IO.File.Exists(catPath))
        {
            System.IO.File.WriteAllText(catPath, ExampleCatalogJson);
        }

        string shopPath = System.IO.Path.Combine(ItemShopPaths.ShopsDirectory, "market_stall.json");
        if (!System.IO.File.Exists(shopPath))
        {
            System.IO.File.WriteAllText(shopPath, ExampleShopJson);
        }

        string npcPath = System.IO.Path.Combine(ItemShopPaths.NpcsDirectory, "silent_haldor.json");
        if (!System.IO.File.Exists(npcPath))
        {
            System.IO.File.WriteAllText(npcPath, ExampleNpcJson);
        }
    }

    private const string ExampleCatalogJson = """
        {
          "id": "blacksmith",
          "entries": [
            { "item": "IronSword", "buyPrice": 200, "sellPrice": 80, "stock": -1 },
            { "item": "IronBuckler", "buyPrice": 120, "sellPrice": 50, "stock": -1 }
          ]
        }
        """;

    private const string ExampleShopJson = """
        {
          "id": "market_stall",
          "displayName": "Ironmonger",
          "catalogId": "blacksmith",
          "currency": { "type": "coins" },
          "ward": { "mode": "vanilla", "requirePermitted": true },
          "sign": { "maxLines": 8, "template": "priceList" },
          "permissions": { "open": "permitted", "manage": "ownerOrAdmin" },
          "stockSource": "container"
        }
        """;

    private const string ExampleNpcJson = """
        {
          "id": "silent_haldor",
          "clonePrefab": "Haldor",
          "shopId": "market_stall",
          "disableDialogue": true
        }
        """;
}
