using System.Collections.Generic;
using DrakesItemShop.Config;
using DrakesItemShop.Pieces;
using DrakesItemShop.Schema;
using TMPro;
using UnityEngine;

namespace DrakesItemShop.Runtime;

internal static class SignCatalogRenderer
{
    public static void ApplyToSign(GameObject signObject, ShopDefinitionDto shop, CatalogDto catalog)
    {
        var sign = signObject.GetComponentInChildren<Sign>();
        var tmp = signObject.GetComponentInChildren<TextMeshPro>();
        if (tmp == null && sign == null)
            return;

        int maxLines = shop.Sign?.MaxLines ?? ItemShopConfig.MaxSignLines;
        var lines = BuildLines(shop, catalog, maxLines);
        string text = string.Join("\n", lines);

        if (tmp != null)
            tmp.text = text;
    }

    public static List<string> BuildLines(ShopDefinitionDto shop, CatalogDto catalog, int maxLines)
    {
        var lines = new List<string>();
        lines.Add(shop.DisplayName ?? shop.Id ?? "Shop");

        foreach (var entry in catalog.Entries ?? new List<CatalogEntryDto>())
        {
            if (lines.Count >= maxLines)
                break;
            if (string.IsNullOrWhiteSpace(entry.Item))
                continue;

            int price = entry.BuyPrice?.ResolveCoinPrice() ?? 0;
            if (price <= 0)
                continue;

            lines.Add($"{ItemResolver.GetDisplayName(entry.Item!)} {price}g");
        }

        return lines;
    }

    public static void RefreshAllSigns()
    {
        foreach (var sign in Object.FindObjectsOfType<ShopSignBehaviour>())
            sign.RefreshFromCatalog();
    }
}
