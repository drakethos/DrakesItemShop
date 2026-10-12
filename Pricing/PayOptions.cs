using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrakeModsLibs.Display;

namespace DrakesItemShop.Pricing;

/// <summary>
/// One way to pay for a deal: optional coins and/or item stacks (AND). Several of these are OR alternatives.
/// Example: 200 coins <b>or</b> 10 Iron + 10 coins.
/// </summary>
internal sealed class PayOption
{
    public int Coins;
    public List<TradeWant> Items = new();

    public bool IsEmpty => Coins <= 0 && Items.Count == 0;

    public string Label
    {
        get
        {
            var parts = new List<string>();
            if (Coins > 0)
                parts.Add($"{Coins} coins");
            foreach (var w in Items)
                parts.Add(w.Label);
            return parts.Count == 0 ? "(empty)" : string.Join(" + ", parts);
        }
    }

    public string Encode()
    {
        var sb = new StringBuilder();
        if (Coins > 0)
            sb.Append('c').Append(':').Append(Coins);
        foreach (var w in Items)
        {
            if (string.IsNullOrWhiteSpace(w.Prefab) || w.Count <= 0)
                continue;
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(w.Prefab.Trim()).Append(':').Append(w.Count);
        }
        return sb.ToString();
    }

    public static PayOption? Decode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var opt = new PayOption();
        foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var bit = part.Trim();
            var colon = bit.LastIndexOf(':');
            if (colon <= 0)
                continue;
            var key = bit.Substring(0, colon).Trim();
            if (!int.TryParse(bit.Substring(colon + 1).Trim(), out var n) || n <= 0)
                continue;
            if (string.Equals(key, "c", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "coins", StringComparison.OrdinalIgnoreCase))
                opt.Coins += n;
            else
                opt.Items.Add(new TradeWant(key, n));
        }
        return opt.IsEmpty ? null : opt;
    }

    public bool BuyerCanPay(Inventory inv, int deals)
    {
        if (deals <= 0)
            return false;
        if (Coins > 0 && ShopState.CountCoins(inv) < (long)Coins * deals)
            return false;
        foreach (var w in Items)
        {
            if (TradePrice.CountIn(inv, w.Prefab) < (long)w.Count * deals)
                return false;
        }
        return true;
    }
}

/// <summary>
/// OR list of <see cref="PayOption"/> on a stack. Stored as <c>drake_pay</c> =
/// <c>c:200|Iron:10;c:10</c> (<c>|</c> = OR, <c>;</c> = AND). Legacy <c>drake_price</c> /
/// <c>drake_trade</c> still read as a single option each (and become two ORs if both exist).
/// </summary>
internal static class PayOptions
{
    public const string Key = "drake_pay";

    public static bool Has(ItemDrop.ItemData? item) => All(item).Count > 0;

    public static List<PayOption> All(ItemDrop.ItemData? item)
    {
        var result = new List<PayOption>();
        if (item == null)
            return result;

        if (item.m_customData != null && item.m_customData.TryGetValue(Key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            foreach (var chunk in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (PayOption.Decode(chunk) is { } opt)
                    result.Add(opt);
            }
            if (result.Count > 0)
                return result;
        }

        var coins = ItemPrice.Coins(item);
        if (coins > 0)
            result.Add(new PayOption { Coins = coins });
        var wants = TradePrice.Wants(item);
        if (wants.Count > 0)
            result.Add(new PayOption { Items = wants.ToList() });
        return result;
    }

    public static void Set(ItemDrop.ItemData item, IReadOnlyList<PayOption> options)
    {
        item.m_customData ??= new Dictionary<string, string>();
        var valid = options?.Where(o => o != null && !o.IsEmpty).ToList() ?? new List<PayOption>();
        ItemPrice.Set(item, "");
        TradePrice.Clear(item);
        if (valid.Count == 0)
        {
            item.m_customData.Remove(Key);
            return;
        }

        item.m_customData[Key] = string.Join("|", valid.Select(o => o.Encode()));

        // Mirror simple single-option cases onto legacy keys for older hover/tools.
        if (valid.Count == 1)
        {
            var only = valid[0];
            if (only.Coins > 0 && only.Items.Count == 0)
                ItemPrice.Set(item, only.Coins.ToString());
            else if (only.Coins <= 0 && only.Items.Count > 0)
                TradePrice.Set(item, only.Items);
        }
    }

    public static void Clear(ItemDrop.ItemData item)
    {
        item.m_customData?.Remove(Key);
        ItemPrice.Set(item, "");
        TradePrice.Clear(item);
    }

    public static string Summary(ItemDrop.ItemData? item)
    {
        var opts = All(item);
        if (opts.Count == 0)
            return "";
        var pay = string.Join("  OR  ", opts.Select(o => o.Label));
        var lot = ItemLot.Of(item);
        if (lot <= 1 || item == null)
            return pay;
        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        return $"{pay} → {lot} {name}";
    }

    public static string Raw(ItemDrop.ItemData? item)
    {
        var opts = All(item);
        return opts.Count == 0 ? "" : string.Join("|", opts.Select(o => o.Encode()));
    }

    public static bool Same(string a, string b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
}
