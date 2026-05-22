using DrakesItemShop.Data;
using DrakesItemShop.Runtime;
using DrakesItemShop.Schema;
using UnityEngine;

namespace DrakesItemShop.Npc;

internal static class NpcTraderSpawner
{
    public static GameObject? Spawn(NpcDefinitionDto def, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrWhiteSpace(def.ClonePrefab))
            return null;

        var prefab = ZNetScene.instance?.GetPrefab(def.ClonePrefab);
        if (prefab == null)
        {
            ItemShopPlugin.LogSource?.LogWarning($"NPC clone prefab not found: {def.ClonePrefab}");
            return null;
        }

        var go = Object.Instantiate(prefab, position, rotation);
        var trader = go.GetComponent<Trader>();
        if (trader == null)
            trader = go.AddComponent<SilentShopTrader>();

        if (trader is SilentShopTrader silent)
        {
            silent.ShopId = def.ShopId ?? "market_stall";
            silent.CatalogId = def.CatalogId ?? "";
            silent.ApplyCatalogFromRegistry();
        }
        else if (trader != null)
        {
            var catalog = ShopRegistry.Instance.ResolveCatalogForNpc(def);
            if (catalog != null)
                TraderCatalogBuilder.ApplyCatalog(trader, catalog);
        }

        return go;
    }

    public static void SpawnAtPlayer(NpcDefinitionDto def, Player player)
    {
        if (player == null)
            return;

        Vector3 pos = player.transform.position + player.transform.forward * 2f;
        Spawn(def, pos, Quaternion.identity);
    }
}
