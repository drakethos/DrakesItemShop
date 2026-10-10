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
        _dummy = config.Bind("06 Trade debug", "Trade dummy piece", true,
            "A mannequin piece (Drake Shop tab) you can trade with while testing the trade menu. It stands in for another player; its offer is controlled from buttons in the trade window. Local only, takes effect on restart.");
}

/// <summary>The mannequin as a trade partner: it keeps what you hand it and offers whatever the test buttons set.</summary>
internal sealed class DummyPartner : ITradePartner
{
    public string Name => "Bjorn";
    public readonly List<TradeEntry> Received = new();

    public void Receive(IReadOnlyList<TradeEntry> items)
    {
        Received.AddRange(items);
        Debug.Log($"[Trade] Test dummy received {items.Count} stack(s): " + string.Join(", ", System.Linq.Enumerable.Select(items, e => e.Item.m_shared.m_name + " x" + e.Amount)));
    }
}

/// <summary>
/// The mannequin you test trading on: E or T asks it to trade (it always agrees), Shift+E makes it ask you. In the window,
/// a row of test buttons plays the other player: set its offer, accept, cancel.
/// </summary>
public class TradeDummyBehaviour : MonoBehaviour, Interactable, Hoverable
{
    private static readonly (string Label, (string Prefab, int Amount)[] Items)[] Offers =
    {
        ("Coins x120", new[] { ("Coins", 120) }),
        ("Iron x10, Leather x5", new[] { ("Iron", 10), ("LeatherScraps", 5) }),
        ("Iron Sword, Honey x3", new[] { ("SwordIron", 1), ("Honey", 3) }),
    };

    private readonly DummyPartner _partner = new();
    private TradeSession? _session;

    public string GetHoverName() => "Trade Dummy";

    public string GetHoverText() =>
        "Trade Dummy\n[<color=yellow><b>E</b></color>] or [<color=yellow><b>T</b></color>] Trade\n[<color=yellow><b>Shift+E</b></color>] Dummy asks you to trade";

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold)
            return false;
        if (alt)
            TradePrompt.Show(_partner.Name, StartTrade, () => { });
        else
            StartTrade();
        return true;
    }

    private void Update()
    {
        var player = Player.m_localPlayer;
        if (player == null || TradeWindow.IsOpen || !Input.GetKeyDown(KeyCode.T))
            return;
        if (Chat.instance != null && Chat.instance.HasFocus())
            return;
        var hover = player.GetHoverObject();
        if (hover != null && hover.GetComponentInParent<TradeDummyBehaviour>() == this)
            StartTrade();
    }

    private void StartTrade()
    {
        _session = new TradeSession(_partner);
        TradeWindow.Open(_session, BuildControls(_session));
    }

    private VisualElement BuildControls(TradeSession session)
    {
        var card = UkControls.Card().Column().Pad(12);
        card.Add(UkControls.Text("TEST CONTROLS: Bjorn", 12, UkTheme.TextMuted, wrap: false));

        var offers = new VisualElement().Row();
        offers.style.flexWrap = Wrap.Wrap;
        offers.style.marginTop = 8;
        foreach (var (label, items) in Offers)
        {
            var captured = items;
            var button = UkControls.MakeButton("Offers " + label, () => SetOffer(session, captured), UkButtonKind.Secondary);
            button.style.marginRight = 8;
            button.style.marginBottom = 6;
            offers.Add(button);
        }
        card.Add(offers);

        var actions = new VisualElement().Row();
        var clear = UkControls.MakeButton("Clear offer", () => session.SetTheirs(Array.Empty<TradeEntry>()), UkButtonKind.Ghost);
        var accept = UkControls.MakeButton("Bjorn accepts", session.PartnerAccepts, UkButtonKind.Secondary);
        var cancel = UkControls.MakeButton("Bjorn cancels", session.PartnerCancels, UkButtonKind.Danger);
        accept.style.marginLeft = 8;
        cancel.style.marginLeft = 8;
        actions.Add(clear);
        actions.Add(UkControls.Spacer());
        actions.Add(accept);
        actions.Add(cancel);
        card.Add(actions);
        return card;
    }

    private static void SetOffer(TradeSession session, (string Prefab, int Amount)[] items)
    {
        var entries = new List<TradeEntry>();
        foreach (var (prefabName, amount) in items)
        {
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
                continue;
            var item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = Math.Min(amount, Math.Max(1, item.m_shared.m_maxStackSize));
            item.m_quality = 1;
            item.m_durability = item.GetMaxDurability();
            entries.Add(new TradeEntry(item, item.m_stack));
        }
        session.SetTheirs(entries);
    }
}

internal static class TradeDummyPiece
{
    public const string PieceName = "Drake_TradeDummy";

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

        var piece = new CustomPiece(PieceName, "ArmorStand", config);
        if (!piece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {PieceName}: clone 'ArmorStand' is not a piece.");
            return;
        }

        var prefab = piece.Piece.gameObject;
        // The stand's own use (equipment) is replaced by trading.
        if (prefab.GetComponent<ArmorStand>() is { } stand)
            UnityEngine.Object.DestroyImmediate(stand);
        if (prefab.GetComponent<TradeDummyBehaviour>() == null)
            prefab.AddComponent<TradeDummyBehaviour>();

        PieceManager.Instance.AddPiece(piece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {PieceName} (clone: ArmorStand)");
    }
}
