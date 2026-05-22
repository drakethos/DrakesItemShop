using System.Collections.Generic;
using DrakesItemShop.Runtime;
using DrakesItemShop.Schema;
using UnityEngine;

namespace DrakesItemShop.Npc;

public class SilentShopTrader : Trader
{
    public string ShopId { get; set; } = "market_stall";
    public string CatalogId { get; set; } = "";

    private bool _catalogApplied;

    public new void Start()
    {
        base.Start();
        ApplyCatalogFromRegistry();
    }

    public void ApplyCatalogFromRegistry()
    {
        CatalogDto? catalog = null;
        if (!string.IsNullOrWhiteSpace(CatalogId) &&
            ShopRegistry.Instance.TryGetCatalog(CatalogId, out var byId))
            catalog = byId;
        else if (ShopRegistry.Instance.TryGetShop(ShopId, out var shop))
            catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);

        if (catalog != null)
        {
            TraderCatalogBuilder.ApplyCatalog(this, catalog);
            _catalogApplied = true;
        }
    }

    public void Say(List<string> texts, string trigger, string label = "")
    {
    }

    public new void RandomTalk()
    {
    }

    public new void Update()
    {
        if (!_catalogApplied)
            ApplyCatalogFromRegistry();
    }
}
