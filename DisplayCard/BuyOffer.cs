using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DrakesItemShop.DisplayCard;

/// <summary>
/// One buy line on a shop: we take <see cref="WantAmount"/> of <see cref="WantPrefab"/> per deal,
/// pay <see cref="PayCoins"/>, up to <see cref="Cap"/> total units (<c>0</c> = no limit).
/// </summary>
internal readonly struct BuyOfferEntry
{
    public BuyOfferEntry(string wantPrefab, int wantAmount, int payCoins, int cap = 0, int filled = 0, bool tillTopUp = false)
    {
        WantPrefab = wantPrefab ?? "";
        WantAmount = wantAmount > 0 ? wantAmount : 1;
        PayCoins = payCoins > 0 ? payCoins : 0;
        Cap = cap > 0 ? cap : 0;
        Filled = filled > 0 ? filled : 0;
        TillTopUp = tillTopUp;
    }

    /// <summary>Copy with a new <see cref="Filled"/> count.</summary>
    public BuyOfferEntry WithFilled(int filled) => new(WantPrefab, WantAmount, PayCoins, Cap, filled, TillTopUp);

    public string WantPrefab { get; }
    public int WantAmount { get; }
    public int PayCoins { get; }
    /// <summary>Total units this stand will buy (0 = unlimited).</summary>
    public int Cap { get; }
    /// <summary>Units already bought toward <see cref="Cap"/>.</summary>
    public int Filled { get; }
    /// <summary>When the buy budget runs dry, pay the rest from the till (coins from sales). Off: only the budget pays.</summary>
    public bool TillTopUp { get; }

    public bool IsValid => !string.IsNullOrEmpty(WantPrefab) && PayCoins > 0;

    /// <summary>Units still wanted. Unlimited caps report a large remaining.</summary>
    public int Remaining => Cap <= 0 ? int.MaxValue / 4 : Math.Max(0, Cap - Filled);

    public bool Accepting => IsValid && Remaining >= WantAmount;

    public string Summary
    {
        get
        {
            if (!IsValid)
                return "Empty";
            var name = BuyOffers.ItemLabel(WantPrefab);
            var get = WantAmount == 1 ? name : $"{WantAmount} {name}";
            var line = $"Buys {get} for {PayCoins} coins";
            if (Cap > 0)
                line += $" — {Math.Min(Filled, Cap)}/{Cap} taken";
            if (TillTopUp)
                line += " · till tops up";
            return line;
        }
    }
}

/// <summary>
/// Buy offers + buy budget on a shop piece ZDO (display case). Cards are visuals + hover targets —
/// they appear when offers exist, not as hammer pieces.
/// </summary>
internal static class BuyOffers
{
    private static readonly int ListKey = "drake_buys".GetStableHashCode();
    private static readonly int BudgetKey = "drake_buy_budget".GetStableHashCode();

    public static List<BuyOfferEntry> All(ZDO? zdo)
    {
        var result = new List<BuyOfferEntry>();
        if (zdo == null)
            return result;
        var raw = zdo.GetString(ListKey) ?? "";
        if (raw.Length == 0)
            return result;
        foreach (var part in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(':');
            if (f.Length < 3)
                continue;
            if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amt))
                amt = 1;
            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pay))
                pay = 0;
            var cap = 0;
            var filled = 0;
            if (f.Length >= 4)
                int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out cap);
            if (f.Length >= 5)
                int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out filled);
            var topUp = f.Length >= 6 && f[5] == "1";
            var entry = new BuyOfferEntry(f[0], amt, pay, cap, filled, topUp);
            if (entry.IsValid)
                result.Add(entry);
        }
        return result;
    }

    public static void SetAll(ZDO zdo, IReadOnlyList<BuyOfferEntry> offers)
    {
        var sb = new StringBuilder();
        foreach (var o in offers.Where(o => o.IsValid))
        {
            if (sb.Length > 0)
                sb.Append('|');
            sb.Append(o.WantPrefab).Append(':')
                .Append(o.WantAmount.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(o.PayCoins.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(o.Cap.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(o.Filled.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(o.TillTopUp ? '1' : '0');
        }
        zdo.Set(ListKey, sb.ToString());
    }

    public static void Add(ZDO zdo, BuyOfferEntry entry)
    {
        var list = All(zdo);
        list.Add(entry);
        SetAll(zdo, list);
    }

    public static void RemoveAt(ZDO zdo, int index)
    {
        var list = All(zdo);
        if (index < 0 || index >= list.Count)
            return;
        list.RemoveAt(index);
        SetAll(zdo, list);
    }

    public static void ReplaceAt(ZDO zdo, int index, BuyOfferEntry entry)
    {
        var list = All(zdo);
        if (index < 0 || index >= list.Count)
            return;
        list[index] = entry;
        SetAll(zdo, list);
    }

    public static void AddFilled(ZDO zdo, int index, int units)
    {
        if (units <= 0)
            return;
        var list = All(zdo);
        if (index < 0 || index >= list.Count)
            return;
        list[index] = list[index].WithFilled(list[index].Filled + units);
        SetAll(zdo, list);
    }

    // ---- buying switch (per stand) ----

    private static readonly int EnabledKey = "drake_buy_on".GetStableHashCode();

    /// <summary>
    /// Whether this stand buys from customers. Never set yet: on when it already has offers (stands set up before the switch
    /// existed keep buying), off otherwise.
    /// </summary>
    public static bool Enabled(ZDO? zdo)
    {
        if (zdo == null)
            return false;
        var stored = zdo.GetInt(EnabledKey);
        return stored == 0 ? (zdo.GetString(ListKey) ?? "").Length > 0 : stored == 1;
    }

    public static void SetEnabled(ZDO zdo, bool on) => zdo.Set(EnabledKey, on ? 1 : 2);

    /// <summary>Coins that can pay for one <paramref name="offer"/> deal: the budget, plus the till when the offer may dip into it.</summary>
    public static int Spendable(Container shop, ZDO zdo, BuyOfferEntry offer) =>
        Budget(zdo) + (offer.TillTopUp ? Pricing.ShopState.Till(shop) : 0);

    /// <summary>Pay <paramref name="coins"/>: the budget first, then the till if the offer allows. Nothing is taken unless all of it can be.</summary>
    public static bool TrySpend(Container shop, ZDO zdo, BuyOfferEntry offer, int coins, out int fromBudget, out int fromTill)
    {
        fromBudget = fromTill = 0;
        if (coins <= 0)
            return true;
        if (Spendable(shop, zdo, offer) < coins)
            return false;
        fromBudget = Math.Min(Budget(zdo), coins);
        fromTill = coins - fromBudget;
        zdo.Set(BudgetKey, Budget(zdo) - fromBudget);
        if (fromTill > 0)
            Pricing.ShopState.AddTill(shop, -fromTill);
        return true;
    }

    /// <summary>Undo <see cref="TrySpend"/>.</summary>
    public static void Refund(Container shop, ZDO zdo, int fromBudget, int fromTill)
    {
        AddBudget(zdo, fromBudget);
        if (fromTill > 0)
            Pricing.ShopState.AddTill(shop, fromTill);
    }

    // ---- goods bought into the stand's buy crates ----

    private static readonly int HeldKey = "drake_buy_held".GetStableHashCode();

    /// <summary>Units of each item the stand's buy crates hold (bought from customers, not collected yet).</summary>
    public static Dictionary<string, int> Held(ZDO? zdo)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (zdo == null)
            return result;
        foreach (var part in (zdo.GetString(HeldKey) ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(':');
            if (f.Length == 2 && f[0].Length > 0 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                result[f[0]] = n;
        }
        return result;
    }

    public static int HeldOf(ZDO? zdo, string prefab) => Held(zdo).TryGetValue(prefab, out var n) ? n : 0;

    public static void AddHeld(ZDO zdo, string prefab, int units)
    {
        var held = Held(zdo);
        held.TryGetValue(prefab, out var n);
        n = Math.Max(0, n + units);
        if (n == 0)
            held.Remove(prefab);
        else
            held[prefab] = n;
        zdo.Set(HeldKey, string.Join("|", held.Select(kv => kv.Key + ":" + kv.Value.ToString(CultureInfo.InvariantCulture))));
    }

    public static int Budget(ZDO? zdo) => zdo != null ? Math.Max(0, zdo.GetInt(BudgetKey)) : 0;

    public static void AddBudget(ZDO zdo, int coins)
    {
        if (coins == 0)
            return;
        zdo.Set(BudgetKey, Math.Max(0, Budget(zdo) + coins));
    }

    public static bool TrySpendBudget(ZDO zdo, int coins)
    {
        if (coins <= 0)
            return true;
        var have = Budget(zdo);
        if (have < coins)
            return false;
        zdo.Set(BudgetKey, have - coins);
        return true;
    }

    public static int TakeBudget(ZDO zdo)
    {
        var n = Budget(zdo);
        if (n > 0)
            zdo.Set(BudgetKey, 0);
        return n;
    }

    public static string ItemLabel(string prefab)
    {
        if (string.IsNullOrEmpty(prefab))
            return "?";
        var drop = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();
        if (drop == null)
            return prefab;
        var token = drop.m_itemData.m_shared.m_name;
        return global::Localization.instance != null ? global::Localization.instance.Localize(token) : token;
    }

    public static Sprite? ItemIcon(string prefab)
    {
        if (string.IsNullOrEmpty(prefab) || ObjectDB.instance == null)
            return null;
        return ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
    }

    /// <summary>Fingerprint so the case can skip rebuilding identical card shelves.</summary>
    public static string Fingerprint(ZDO? zdo) =>
        zdo == null ? "" : (zdo.GetString(ListKey) ?? "") + "#" + Budget(zdo) + "#" + Enabled(zdo);
}
