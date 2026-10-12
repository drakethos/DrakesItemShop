using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Pricing;

/// <summary>
/// When the server runs this mod, a purchase is checked by the server before anything is taken. Prefers the live chest if this
/// peer has it spawned (prices on stacks are reliable there); falls back to the ZDO item blob only when the piece is not loaded.
/// </summary>
internal static class ShopServer
{
    private const string Rpc = "DrakesItemShop_ShopCheck";
    private const float WaitSeconds = 6f;

    private enum Msg { Check = 1, Reply = 2 }

    private static ZRoutedRpc? _registeredFor;
    private static readonly Dictionary<string, (Action<string?> Done, float Since)> Waiting = new();
    private static readonly int OpenKey = "drake_shop_open".GetStableHashCode();
    /// <summary>Same hash as <c>ZDOVars.s_items</c> (−938864442).</summary>
    private static readonly int ItemsKey = "items".GetStableHashCode();

    private static long ServerPeer() =>
        ZNet.instance == null ? 0L : ZNet.instance.IsServer() ? ZNet.GetUID() : ZNet.instance.GetServerPeer()?.m_uid ?? 0L;

    private static void EnsureRegistered()
    {
        if (ZRoutedRpc.instance == null || ZRoutedRpc.instance == _registeredFor)
            return;
        ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnMessage);
        _registeredFor = ZRoutedRpc.instance;
        Waiting.Clear();
    }

    internal static void Tick()
    {
        EnsureRegistered();
        foreach (var entry in Waiting.ToList())
        {
            if (Time.unscaledTime - entry.Value.Since <= WaitSeconds)
                continue;
            Waiting.Remove(entry.Key);
            entry.Value.Done("The server didn't answer. Nothing was taken.");
        }
    }

    /// <summary>Ask the server to confirm a coin purchase. <paramref name="done"/> gets null for yes, or the reason for no.</summary>
    internal static void Validate(Container container, ItemDrop.ItemData item, int quantity, int price, Action<string?> done) =>
        ValidateInternal(container, item, quantity, price, tradeKey: "", done);

    /// <summary>Ask the server to confirm an item-trade purchase.</summary>
    internal static void ValidateTrade(Container container, ItemDrop.ItemData item, int quantity, string tradeKey, Action<string?> done) =>
        ValidateInternal(container, item, quantity, price: 0, tradeKey ?? "", done);

    /// <summary>Ask the server to confirm a purchase with OR payment fingerprint (<c>drake_pay</c> raw).</summary>
    internal static void ValidatePay(Container container, ItemDrop.ItemData item, int deals, string payRaw, Action<string?> done) =>
        ValidateInternal(container, item, deals, price: 0, tradeKey: payRaw ?? "", done);

    private static void ValidateInternal(Container container, ItemDrop.ItemData item, int quantity, int price, string tradeKey, Action<string?> done)
    {
        EnsureRegistered();
        var nview = container.GetComponent<ZNetView>();
        if (!Trade.TradeLedger.ServerMode || nview == null || !nview.IsValid() || item.m_dropPrefab == null)
        {
            done(null);
            return;
        }

        // Hosting locally: check the live chest here and skip the RPC round-trip (same result, no ZDO-blob desync).
        if (ZNet.instance != null && ZNet.instance.IsServer())
        {
            done(CheckLiveOrSaved(nview.GetZDO(), item.m_dropPrefab.name, price, quantity, tradeKey));
            return;
        }

        var id = Guid.NewGuid().ToString("N").Substring(0, 8);
        Waiting[id] = (done, Time.unscaledTime);
        var uid = nview.GetZDO().m_uid;
        var pkg = new ZPackage();
        pkg.Write((int)Msg.Check);
        pkg.Write(id);
        pkg.Write(uid.UserID);
        pkg.Write(uid.ID);
        pkg.Write(item.m_dropPrefab.name);
        pkg.Write(price);
        pkg.Write(quantity);
        pkg.Write(tradeKey ?? "");
        ZRoutedRpc.instance.InvokeRoutedRPC(ServerPeer(), Rpc, pkg);
    }

    private static void OnMessage(long sender, ZPackage pkg)
    {
        var type = (Msg)pkg.ReadInt();
        var id = pkg.ReadString();
        if (type == Msg.Reply)
        {
            var ok = pkg.ReadInt() == 1;
            var reason = pkg.ReadString();
            if (Waiting.TryGetValue(id, out var waiting))
            {
                Waiting.Remove(id);
                waiting.Done(ok ? null : reason);
            }
            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;
        var user = pkg.ReadLong();
        var zid = pkg.ReadUInt();
        var prefab = pkg.ReadString();
        var price = pkg.ReadInt();
        var quantity = pkg.ReadInt();
        var tradeKey = pkg.ReadString();
        var reply = CheckLiveOrSaved(ZDOMan.instance?.GetZDO(new ZDOID(user, zid)), prefab, price, quantity, tradeKey);
        var answer = new ZPackage();
        answer.Write((int)Msg.Reply);
        answer.Write(id);
        answer.Write(reply == null ? 1 : 0);
        answer.Write(reply ?? "");
        ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc, answer);
    }

    /// <summary>Null when stock + price back the purchase. Coin sales use <paramref name="price"/>; trades use <paramref name="tradeKey"/>.</summary>
    private static string? CheckLiveOrSaved(ZDO? zdo, string prefab, int price, int quantity, string tradeKey)
    {
        if (zdo == null)
            return "The shop isn't there.";
        if (zdo.GetInt(OpenKey) != 1)
            return "The shop is closed.";
        var isTrade = !string.IsNullOrWhiteSpace(tradeKey);
        if (quantity <= 0 || (!isTrade && price <= 0))
            return "That isn't a valid purchase.";

        try
        {
            var items = StockItems(zdo);
            if (items == null)
                return "Sold out or changed. Look again.";

            var same = items.Where(i => i.m_dropPrefab != null && PrefabMatches(i.m_dropPrefab.name, prefab)).ToList();
            if (same.Count == 0)
            {
                ItemShopPlugin.LogSource?.LogInfo($"[Shop] Check fail: no '{prefab}' in shop (live/saved has {items.Count} stacks).");
                return "Sold out or changed. Look again.";
            }
            if (same.Sum(i => i.m_stack) < quantity)
                return "Sold out or changed. Look again.";

            if (isTrade)
            {
                var pricedStacks = same.Where(i => PayOptions.Same(PayOptions.Raw(i), tradeKey)
                                                   || TradePrice.Same(TradePrice.Raw(i), tradeKey)).ToList();
                if (pricedStacks.Count == 0)
                {
                    ItemShopPlugin.LogSource?.LogInfo(
                        $"[Shop] Check fail: '{prefab}' pay '{tradeKey}' not on stacks (have: {string.Join(",", same.Select(i => PayOptions.Raw(i) is { Length: > 0 } r ? r : "none"))}).");
                    return "Price isn't saved on the stock. Re-open Shop, set the price again, wait a second, try again.";
                }
                var lot = ItemLot.Of(pricedStacks[0]);
                var need = quantity * Math.Max(1, lot);
                if (pricedStacks.Sum(i => i.m_stack) >= need)
                    return null;
                return "Sold out or changed. Look again.";
            }

            var coinPriced = same.Where(i => ItemPrice.Coins(i) == price).Sum(i => i.m_stack);
            if (coinPriced >= quantity)
                return null;

            ItemShopPlugin.LogSource?.LogInfo(
                $"[Shop] Check fail: '{prefab}' in stock but price {price} not on stacks (have: {string.Join(",", same.Select(i => ItemPrice.Raw(i) is { Length: > 0 } r ? r : "none"))}).");
            return "Price isn't saved on the stock. Re-open Shop, re-type the price, wait a second, try again.";
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Shop] Could not check a purchase: {e.Message}");
            return "The server couldn't check that. Nothing was taken.";
        }
    }

    private static bool PrefabMatches(string have, string want) =>
        string.Equals(have, want, StringComparison.OrdinalIgnoreCase)
        || string.Equals(have.Replace("(Clone)", ""), want.Replace("(Clone)", ""), StringComparison.OrdinalIgnoreCase);

    /// <summary>Live inventory when the piece is spawned here; otherwise the ZDO item blob.</summary>
    private static List<ItemDrop.ItemData>? StockItems(ZDO zdo)
    {
        if (ZNetScene.instance != null)
        {
            var go = ZNetScene.instance.FindInstance(zdo);
            if (go != null && go.GetComponent<Container>() is { } container)
                return container.GetInventory().GetAllItems().ToList();
        }

        var saved = zdo.GetString(ItemsKey);
        if (string.IsNullOrEmpty(saved))
            return null;
        var inventory = new Inventory("shop check", null, 12, 12);
        inventory.Load(new ZPackage(saved));
        return inventory.GetAllItems().ToList();
    }
}

/// <summary>The builder's client says who bought what when a sale lands (and they are nearby).</summary>
public class ShopWatcher : MonoBehaviour
{
    private Container? _container;
    private int _seen = -1;
    private float _next;

    private void Awake() => _container = GetComponent<Container>();

    private void Update()
    {
        if (_container == null || Time.time < _next || Player.m_localPlayer == null)
            return;
        _next = Time.time + 1f;
        var seq = ShopState.Seq(_container);
        if (_seen >= 0 && seq > _seen && _container.GetComponent<Piece>() is { } piece && piece.GetCreator() == Player.m_localPlayer.GetPlayerID()
            && ShopState.Sales(_container).FirstOrDefault() is { Buyer: { Length: > 0 } buyer } sale)
            Trade.TradeNet.Say(sale.Coins > 0
                ? $"{buyer} bought {sale.Quantity} {sale.Item} for {sale.Coins} coins."
                : $"{buyer} traded for {sale.Quantity} {sale.Item}.");
        _seen = seq;
    }
}

[HarmonyPatch(typeof(Container), "Awake")]
internal static class ShopWatcherPatch
{
    [HarmonyPostfix]
    private static void Postfix(Container __instance)
    {
        if (ShopState.IsPiece(__instance) && __instance.GetComponent<ShopWatcher>() == null)
            __instance.gameObject.AddComponent<ShopWatcher>();
    }
}
