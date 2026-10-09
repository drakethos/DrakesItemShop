using DrakeModsLibs.API;
using DrakesItemShop.Config;
using DrakesItemShop.Runtime;
using DrakesItemShop.UI;
using UnityEngine;

namespace DrakesItemShop.Networking;

internal static class ShopRpc
{
    public const string Buy = "DrakesItemShop_Buy";
    public const string Sell = "DrakesItemShop_Sell";

    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered || ZRoutedRpc.instance == null)
            return;

        ZRoutedRpc.instance.Register<string, int>(Buy, OnBuy);
        ZRoutedRpc.instance.Register<string, int>(Sell, OnSell);
        _registered = true;
        ItemShopPlugin.LogSource?.LogInfo("Shop RPCs registered.");
    }

    public static void RequestBuy(string itemId, int quantity)
    {
        if (ZNet.instance == null)
        {
            RunLocalBuy(itemId, quantity);
            return;
        }

        if (ZNet.instance.IsServer() || !TryInvokeServer(Buy, itemId, quantity))
            RunLocalBuy(itemId, quantity);
    }

    public static void RequestSell(string itemId, int quantity)
    {
        if (ZNet.instance == null)
        {
            RunLocalSell(itemId, quantity);
            return;
        }

        if (ZNet.instance.IsServer())
            RunLocalSell(itemId, quantity);
        else if (!TryInvokeServer(Sell, itemId, quantity))
            RunLocalSell(itemId, quantity);
    }

    private static bool TryInvokeServer(string rpcName, string itemId, int quantity)
    {
        EnsureRegistered();
        if (ZRoutedRpc.instance == null || ZNet.instance == null)
            return false;

        long target = ZNet.instance.GetServerPeer()?.m_uid ?? ZNet.GetUID();
        ZRoutedRpc.instance.InvokeRoutedRPC(rpcName, target, itemId, quantity);
        return true;
    }

    private static void OnBuy(long sender, string itemId, int quantity) =>
        HandleTransaction(sender, itemId, quantity, buy: true);

    private static void OnSell(long sender, string itemId, int quantity) =>
        HandleTransaction(sender, itemId, quantity, buy: false);

    private static void HandleTransaction(long sender, string itemId, int quantity, bool buy)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;

        var player = FindPlayer(sender);
        var session = ShopSessionState.Current;

        if (player == null || session == null)
            return;

        if (Vector3.Distance(player.transform.position, session.AnchorPosition) > ItemShopConfig.MaxInteractDistance)
        {
            if (!ShopPanel.TryShowStatus("Too far from shop."))
                Message(player, "Too far from shop.");
            return;
        }

        var result = buy
            ? ShopTransactionService.TryBuy(player, session, itemId, quantity)
            : ShopTransactionService.TrySell(player, session, itemId, quantity);

        NotifyPlayer(player, result, itemId, buy);
    }

    private static Player? FindPlayer(long sender) => Player.GetPlayer(sender);

    private static void RunLocalBuy(string itemId, int quantity)
    {
        var player = Player.m_localPlayer;
        var session = ShopSessionState.Current;
        if (player == null || session == null)
            return;

        var result = ShopTransactionService.TryBuy(player, session, itemId, quantity);
        NotifyPlayer(player, result, itemId, buy: true);
    }

    private static void RunLocalSell(string itemId, int quantity)
    {
        var player = Player.m_localPlayer;
        var session = ShopSessionState.Current;
        if (player == null || session == null)
            return;

        var result = ShopTransactionService.TrySell(player, session, itemId, quantity);
        NotifyPlayer(player, result, itemId, buy: false);
    }

    private static void NotifyPlayer(Player player, TransactionResult result, string itemId, bool buy)
    {
        if (ShopPanel.TryShowTransactionResult(result, itemId, buy))
            return;

        if (result == TransactionResult.Ok && buy && ShopSessionState.Current?.UsesVendingMachine == true)
        {
            Message(player, "Purchase dispensed — press E to collect from slot.");
            return;
        }

        Message(player, ResultMessage(result));
    }

    private static void Message(Player player, string text) =>
        CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, text);

    internal static string ResultMessage(TransactionResult r) => r switch
    {
        TransactionResult.Ok => "Transaction complete.",
        TransactionResult.Denied => "Access denied.",
        TransactionResult.InvalidItem => "Invalid item.",
        TransactionResult.OutOfStock => "Out of stock.",
        TransactionResult.NotEnoughCurrency => "Not enough coins or currency.",
        TransactionResult.InventoryFull => "Inventory full.",
        TransactionResult.SellDisabled => "Selling disabled.",
        TransactionResult.NoSellPrice => "Cannot sell this item here.",
        TransactionResult.DispenseFull => "Dispense slot full — collect your purchase first (E).",
        TransactionResult.TreasuryEmpty => "Shop cannot pay — treasury is empty.",
        _ => "Transaction failed.",
    };
}
