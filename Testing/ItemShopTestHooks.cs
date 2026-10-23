using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrakesItemShop.Pricing;
using UnityEngine;

namespace DrakesItemShop.Testing;

/// <summary>DrakesTestLab finds methods marked with an attribute named <c>TestHookAttribute</c>. No reference to DrakesTestLab.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TestHookAttribute : Attribute
{
    public TestHookAttribute(string name) => Name = name;
    public string Name { get; }
}

/// <summary>
/// Test hooks for automated ItemShop tests. They run the same calls the Shop tab, price editor and buy window make, as the current
/// player. The shop is a tagged display case / rack / mat (args "target"), else the nearest shop piece.
/// </summary>
internal static class ItemShopTestHooks
{
    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static string S(Dictionary<string, object?> a, string key, string fallback = "") =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    static int I(Dictionary<string, object?> a, string key, int fallback = 0) =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

    static bool B(Dictionary<string, object?> a, string key, bool fallback = false)
    {
        if (!a.TryGetValue(key, out var v) || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static Container Shop(GameObject? target)
    {
        var c = target != null ? target.GetComponentInChildren<Container>() ?? target.GetComponentInParent<Container>() : null;
        if (c != null && ShopState.IsPiece(c))
            return c;
        Container? best = null;
        var bestD = 30f;
        foreach (var cand in UnityEngine.Object.FindObjectsOfType<Container>())
        {
            if (!ShopState.IsPiece(cand))
                continue;
            var d = Vector3.Distance(cand.transform.position, P.transform.position);
            if (d < bestD)
            {
                best = cand;
                bestD = d;
            }
        }

        return best ?? throw new InvalidOperationException("no shop piece (display case, rack or mat) within 30 m");
    }

    static string PrefabOf(ItemDrop.ItemData i) => i.m_dropPrefab != null ? i.m_dropPrefab.name : i.m_shared?.m_name ?? "";

    static ItemDrop.ItemData? Stock(Container shop, string prefab) =>
        shop.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase) && PayOptions.Has(i))
        ?? shop.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase));

    static List<PayOption> Decode(string raw) =>
        raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(PayOption.Decode).Where(o => o != null).Select(o => o!).ToList();

    /// <summary>May the current player run this shop (price, open, collect)? Builder, or ward-permitted and able to open it.</summary>
    [TestHook("itemshop.canManage")]
    static bool CanManage(GameObject? target = null) => ShopState.CanManage(Shop(target), P);

    /// <summary>
    /// Put goods on the shelf and price them, as the price editor does: prefab, count, price ("c:5", "Wood:10", "c:5|Iron:2" = or),
    /// lot (items per deal, default 1). False when the current player may not manage the shop.
    /// </summary>
    [TestHook("itemshop.stock")]
    static bool StockShop(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var prefab = S(args, "prefab");
        var go = ObjectDB.instance.GetItemPrefab(prefab) ?? throw new InvalidOperationException($"unknown item '{prefab}'");
        var data = go.GetComponent<ItemDrop>().m_itemData.Clone();
        data.m_dropPrefab = go;
        data.m_stack = Math.Max(1, I(args, "count", 1));
        data.m_durability = data.GetMaxDurability();
        if (!shop.GetInventory().AddItem(data))
            throw new InvalidOperationException("the shop has no room");

        var price = S(args, "price");
        if (price.Length > 0)
        {
            var live = Stock(shop, prefab)!;
            PayOptions.Set(live, Decode(price));
            ItemLot.Set(live, Math.Max(1, I(args, "lot", 1)));
        }

        ShopState.NotifyChanged(shop);
        return true;
    }

    /// <summary>Change the price of a stocked item (manager only): prefab, price.</summary>
    [TestHook("itemshop.setPrice")]
    static bool SetPrice(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var item = Stock(shop, S(args, "prefab")) ?? throw new InvalidOperationException("not in stock");
        PayOptions.Set(item, Decode(S(args, "price")));
        ShopState.NotifyChanged(shop);
        return true;
    }

    [TestHook("itemshop.price")]
    static string Price(Dictionary<string, object?> args, GameObject? target = null) =>
        Stock(Shop(target), S(args, "prefab")) is { } item ? PayOptions.Raw(item) : "";

    /// <summary>Open / close for business (the Shop tab's switch). False when the current player may not manage it.</summary>
    [TestHook("itemshop.open")]
    static bool Open(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        ShopState.SetOpen(shop, B(args, "on", true));
        return true;
    }

    [TestHook("itemshop.isOpen")]
    static bool IsOpen(GameObject? target = null) => ShopState.IsOpen(Shop(target));

    /// <summary>Buy as the current player through the buy window's own purchase code: prefab, deals (default 1), option (pay option index), pay (the price the buyer saw; default the current one).
    /// Returns "" when it went through, else the message the buyer sees.</summary>
    [TestHook("itemshop.buy")]
    static string Buy(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        var hint = Stock(shop, S(args, "prefab"));
        if (hint == null)
            return "Sold out or changed. Look again.";
        return ShopPurchase.Purchase(shop, hint, I(args, "deals", 1), I(args, "option", 0), S(args, "pay", PayOptions.Raw(hint)), P) ?? "";
    }

    /// <summary>How many of the item the shop still has.</summary>
    [TestHook("itemshop.stockCount")]
    static int StockCount(Dictionary<string, object?> args, GameObject? target = null) =>
        Shop(target).GetInventory().GetAllItems().Where(i => PrefabOf(i).Equals(S(args, "prefab"), StringComparison.OrdinalIgnoreCase)).Sum(i => i.m_stack);

    /// <summary>Coins waiting in the till.</summary>
    [TestHook("itemshop.till")]
    static int Till(GameObject? target = null) => ShopState.Till(Shop(target));

    /// <summary>Collect the till into the inventory (manager only, as the Shop tab's Collect). Returns the coins collected, -1 when not allowed.</summary>
    [TestHook("itemshop.collect")]
    static int Collect(GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return -1;
        var coins = ShopState.TakeTill(shop);
        if (coins > 0 && !ShopState.GiveCoins(P.GetInventory(), coins))
            return 0;
        return coins;
    }

    /// <summary>The sales log, newest first: "Buyer: 2 x Item for 10".</summary>
    [TestHook("itemshop.sales")]
    static string Sales(GameObject? target = null) =>
        string.Join(" | ", ShopState.Sales(Shop(target)).Select(s => $"{s.Buyer}: {s.Quantity} x {s.Item} for {s.Coins}"));

    /// <summary>May the current player open the shop's chest itself (wards / LockSmith)?</summary>
    [TestHook("itemshop.canOpenChest")]
    static bool CanOpenChest(GameObject? target = null) => ShopState.CanOpen(Shop(target), P);
}
