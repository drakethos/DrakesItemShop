using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using DrakesItemShop.Schema;

namespace DrakesItemShop.Runtime;

internal sealed class ShopRegistry
{
    private static ShopRegistry? _instance;
    public static ShopRegistry Instance => _instance ??= new ShopRegistry();

    private readonly Dictionary<string, CatalogDto> _catalogs = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShopDefinitionDto> _shops = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NpcDefinitionDto> _npcs = new(System.StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, CatalogDto> Catalogs => _catalogs;
    public IReadOnlyDictionary<string, ShopDefinitionDto> Shops => _shops;
    public IReadOnlyDictionary<string, NpcDefinitionDto> Npcs => _npcs;

    public void Reload(ManualLogSource log)
    {
        ItemShopPaths.EnsureDirectories();
        FailureLogWriter.Clear();

        var failures = new List<ValidationFailure>();

        var (rawCatalogs, catLoadFail) = ShopLoader.LoadDirectory<CatalogDto>(ItemShopPaths.CatalogsDirectory);
        failures.AddRange(catLoadFail);
        var (validCatalogs, catValFail) = ShopValidator.ValidateCatalogs(rawCatalogs);
        failures.AddRange(catValFail);

        var catalogMap = validCatalogs.ToDictionary(c => c.Id!.Trim(), c => c, System.StringComparer.OrdinalIgnoreCase);

        var (rawShops, shopLoadFail) = ShopLoader.LoadDirectory<ShopDefinitionDto>(ItemShopPaths.ShopsDirectory);
        failures.AddRange(shopLoadFail);
        var (validShops, shopValFail) = ShopValidator.ValidateShops(rawShops, catalogMap);
        failures.AddRange(shopValFail);

        var shopMap = validShops.ToDictionary(s => s.Id!.Trim(), s => s, System.StringComparer.OrdinalIgnoreCase);

        var (rawNpcs, npcLoadFail) = ShopLoader.LoadDirectory<NpcDefinitionDto>(ItemShopPaths.NpcsDirectory);
        failures.AddRange(npcLoadFail);
        var (validNpcs, npcValFail) = ShopValidator.ValidateNpcs(rawNpcs, catalogMap, shopMap);
        failures.AddRange(npcValFail);

        if (failures.Count > 0)
            FailureLogWriter.WriteAll(failures);

        _catalogs.Clear();
        foreach (var c in validCatalogs)
            _catalogs[c.Id!.Trim()] = c;

        _shops.Clear();
        foreach (var s in validShops)
            _shops[s.Id!.Trim()] = s;

        _npcs.Clear();
        foreach (var n in validNpcs)
            _npcs[n.Id!.Trim()] = n;

        log.LogInfo($"ItemShop loaded {_catalogs.Count} catalogs, {_shops.Count} shops, {_npcs.Count} NPC defs" +
                    (failures.Count > 0 ? $" ({failures.Count} failures — see {ItemShopPaths.FailureLogFile})" : "."));
    }

    public bool TryGetCatalog(string? id, out CatalogDto catalog)
    {
        catalog = null!;
        return !string.IsNullOrWhiteSpace(id) && _catalogs.TryGetValue(id.Trim(), out catalog!);
    }

    public bool TryGetShop(string? id, out ShopDefinitionDto shop)
    {
        shop = null!;
        return !string.IsNullOrWhiteSpace(id) && _shops.TryGetValue(id.Trim(), out shop!);
    }

    public bool TryGetNpc(string? id, out NpcDefinitionDto npc)
    {
        npc = null!;
        return !string.IsNullOrWhiteSpace(id) && _npcs.TryGetValue(id.Trim(), out npc!);
    }

    public CatalogDto? ResolveCatalogForShop(ShopDefinitionDto shop)
    {
        if (shop.CatalogId != null && _catalogs.TryGetValue(shop.CatalogId, out var cat))
            return cat;
        return null;
    }

    public CatalogDto? ResolveCatalogForNpc(NpcDefinitionDto npc)
    {
        if (!string.IsNullOrWhiteSpace(npc.CatalogId) && _catalogs.TryGetValue(npc.CatalogId, out var cat))
            return cat;
        if (!string.IsNullOrWhiteSpace(npc.ShopId) && _shops.TryGetValue(npc.ShopId, out var shop))
            return ResolveCatalogForShop(shop);
        return null;
    }
}
