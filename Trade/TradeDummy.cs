using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Trade;

/// <summary>Local test settings for the trade menu.</summary>
internal static class TradeDebug
{
    private static ConfigEntry<bool> _dummy = default!;

    /// <summary>The Trade Dummy piece in the build menu (a mannequin to trade with while testing; local only).</summary>
    internal static bool DummyEnabled => _dummy.Value;

    internal static void Bind(ConfigFile config) =>
        _dummy = config.Bind("06 Trade debug", "Trade dummy piece", false,
            "A mannequin piece (Drake Shop tab) you can trade with while testing the trade menu. It stands in for another player; its offer is controlled from buttons in the trade window. Local only, takes effect on restart.");
}

/// <summary>The mannequin as a trade partner: it keeps what you hand it and offers whatever the test buttons set.</summary>
internal sealed class DummyPartner : ITradePartner
{
    public string Name => "Bjorn";
    public readonly List<TradeEntry> Received = new();

    public void SendOffer(IReadOnlyList<TradeEntry> mine, int version)
    {
    }

    public void SendAccept(int mineVersion, int theirsVersion)
    {
    }

    public void SendCancel()
    {
    }

    public void Receive(IReadOnlyList<TradeEntry> items)
    {
        Received.AddRange(items);
        Debug.Log($"[Trade] Test dummy received {items.Count} stack(s): " + string.Join(", ", System.Linq.Enumerable.Select(items, e => e.Item.m_shared.m_name + " x" + e.Amount)));
    }
}


internal static class TradeDummyPiece
{
    public const string PieceName = "Drake_TradeDummy";

    internal static bool IsDummy(Component c) => c != null && Utils.GetPrefabName(c.gameObject) == PieceName;

    public static void Register()
    {
        if (!TradeDebug.DummyEnabled)
            return;

        var config = new PieceConfig
        {
            Name = "$drake_trade_dummy_name",
            Description = "$drake_trade_dummy_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = new[] { new RequirementConfig("Wood", 2, 0, true) },
        };

        // A plain wooden chest: E opens its storage as usual, Shift+E opens the trade menu (TradeDummyPatches).
        var piece = new CustomPiece(PieceName, "piece_chest_wood", config);
        if (!piece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {PieceName}: clone 'piece_chest_wood' is not a piece.");
            return;
        }

        var prefab = piece.Piece.gameObject;
        if (prefab.GetComponent<Container>() is { } container)
            container.m_name = "$drake_trade_dummy_name";
        if (prefab.GetComponent<TradeDummyBehaviour>() == null)
            prefab.AddComponent<TradeDummyBehaviour>();

        PieceManager.Instance.AddPiece(piece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {PieceName} (clone: piece_chest_wood), components: " +
            string.Join(", ", System.Linq.Enumerable.Select(prefab.GetComponents<Component>(), c => c == null ? "null" : c.GetType().Name)));
    }
}
