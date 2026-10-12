using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrakeModsLibs.Display;
using UnityEngine;

namespace DrakesItemShop.Pricing;

/// <summary>One stack the buyer must give: prefab name + count (per unit of the shop goods).</summary>
internal readonly struct TradeWant
{
    public TradeWant(string prefab, int count)
    {
        Prefab = prefab;
        Count = Math.Max(1, count);
    }

    public string Prefab { get; }
    public int Count { get; }

    public string Label
    {
        get
        {
            var nice = DisplayName(Prefab);
            return Count == 1 ? nice : $"{Count}× {nice}";
        }
    }

    public static string DisplayName(string prefab)
    {
        if (ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>() is { } drop)
            return ItemDisplayService.GetDisplayNameForUi(drop.m_itemData, localize: true);
        return prefab;
    }
}

/// <summary>
/// Item barter want-list on a stack (legacy single option). Stored as
/// <c>drake_trade</c> = <c>Iron:4;Copper:2</c>. Prefer <see cref="PayOptions"/> for OR payments.
/// </summary>
internal static class TradePrice
{
    public const string Key = "drake_trade";

    public static bool Has(ItemDrop.ItemData? item) => Wants(item).Count > 0;

    /// <summary>Raw <c>drake_trade</c> string (for matching stock / server checks), or "".</summary>
    public static string Raw(ItemDrop.ItemData? item) =>
        item?.m_customData != null && item.m_customData.TryGetValue(Key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : "";

    public static List<TradeWant> Wants(ItemDrop.ItemData? item)
    {
        var result = new List<TradeWant>();
        var raw = Raw(item);
        if (raw.Length == 0)
            return result;
        foreach (var part in raw.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var bit = part.Trim();
            var colon = bit.LastIndexOf(':');
            if (colon <= 0)
                continue;
            var prefab = bit.Substring(0, colon).Trim();
            if (prefab.Length == 0 || !int.TryParse(bit.Substring(colon + 1).Trim(), out var count) || count <= 0)
                continue;
            result.Add(new TradeWant(prefab, count));
        }
        return result;
    }

    /// <summary>True when two trade fingerprints mean the same wants (order-insensitive).</summary>
    public static bool Same(string a, string b)
    {
        if (string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        static string Norm(string raw) =>
            string.Join(";", WantsFromRaw(raw).OrderBy(w => w.Prefab, StringComparer.OrdinalIgnoreCase)
                .Select(w => w.Prefab + ":" + w.Count));
        return string.Equals(Norm(a ?? ""), Norm(b ?? ""), StringComparison.OrdinalIgnoreCase);
    }

    private static List<TradeWant> WantsFromRaw(string raw)
    {
        var result = new List<TradeWant>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        foreach (var part in raw.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var bit = part.Trim();
            var colon = bit.LastIndexOf(':');
            if (colon <= 0)
                continue;
            var prefab = bit.Substring(0, colon).Trim();
            if (prefab.Length == 0 || !int.TryParse(bit.Substring(colon + 1).Trim(), out var count) || count <= 0)
                continue;
            result.Add(new TradeWant(prefab, count));
        }
        return result;
    }

    public static void Set(ItemDrop.ItemData item, IReadOnlyList<TradeWant> wants)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (wants == null || wants.Count == 0)
        {
            item.m_customData.Remove(Key);
            return;
        }
        var sb = new StringBuilder();
        foreach (var w in wants)
        {
            if (string.IsNullOrWhiteSpace(w.Prefab) || w.Count <= 0)
                continue;
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(w.Prefab.Trim()).Append(':').Append(w.Count);
        }
        if (sb.Length == 0)
            item.m_customData.Remove(Key);
        else
            item.m_customData[Key] = sb.ToString();
    }

    /// <summary>Apply a raw <c>Iron:4;Copper:2</c> fingerprint as the trade price.</summary>
    public static void SetRaw(ItemDrop.ItemData item, string raw) => Set(item, WantsFromRaw(raw));

    public static void Clear(ItemDrop.ItemData item) => item.m_customData?.Remove(Key);

    public static string Summary(ItemDrop.ItemData? item)
    {
        var wants = Wants(item);
        if (wants.Count == 0)
            return "";
        var pay = string.Join(", ", wants.Select(w => w.Label));
        var lot = ItemLot.Of(item);
        if (lot <= 1 || item == null)
            return pay;
        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        return $"{pay} → {lot} {name}";
    }

    /// <summary>How many of <paramref name="prefab"/> the buyer has across all stacks.</summary>
    public static int CountIn(Inventory inventory, string prefab) =>
        inventory.GetAllItems()
            .Where(i => i.m_dropPrefab != null
                        && string.Equals(i.m_dropPrefab.name, prefab, StringComparison.OrdinalIgnoreCase))
            .Sum(i => i.m_stack);

    /// <summary>Take <paramref name="count"/> of <paramref name="prefab"/> from the inventory. False if short.</summary>
    public static bool TakeFrom(Inventory inventory, string prefab, int count)
    {
        if (CountIn(inventory, prefab) < count)
            return false;
        var left = count;
        foreach (var stack in inventory.GetAllItems()
                     .Where(i => i.m_dropPrefab != null
                                 && string.Equals(i.m_dropPrefab.name, prefab, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            var take = Math.Min(stack.m_stack, left);
            inventory.RemoveItem(stack, take);
            left -= take;
            if (left == 0)
                return true;
        }
        return left == 0;
    }

    /// <summary>Put items into the shop chest (several stacks if needed). False if no room.</summary>
    public static bool GiveTo(Inventory inventory, string prefab, int count)
    {
        var left = count;
        while (left > 0)
        {
            var entry = Trade.TradeItemCodec.Make(prefab, left);
            if (entry == null)
                return false;
            var take = entry.Item.m_stack;
            if (!inventory.AddItem(entry.Item))
                return false;
            left -= take;
        }
        return true;
    }

    /// <summary>Search ObjectDB-backed catalog for picker rows (unique prefabs, materials first).</summary>
    public static List<IconCatalogEntry> Search(string query, int limit = 12)
    {
        var q = (query ?? "").Trim();
        var all = IconCatalog.GetAll();
        // One row per prefab (variant 0 preferred).
        var byPrefab = new Dictionary<string, IconCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in all)
        {
            if (!byPrefab.ContainsKey(e.PrefabName) || e.Variant == 0)
                byPrefab[e.PrefabName] = e;
        }

        IEnumerable<IconCatalogEntry> list = byPrefab.Values;
        if (q.Length > 0)
        {
            list = list.Where(e =>
            {
                var local = global::Localization.instance != null
                    ? global::Localization.instance.Localize(e.NameToken)
                    : e.NameToken;
                return e.PrefabName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                       || (local != null && local.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                       || (e.NameToken != null && e.NameToken.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        return list
            .OrderBy(e => e.Category == IconCategory.Materials ? 0 : 1)
            .ThenBy(e => global::Localization.instance != null
                ? global::Localization.instance.Localize(e.NameToken)
                : e.PrefabName)
            .Take(limit)
            .ToList();
    }
}
