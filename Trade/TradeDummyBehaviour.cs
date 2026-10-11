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


/// <summary>Marks the trade test chest (E opens it, Shift+E trades; see TradeDummyPatches).</summary>
public class TradeDummyBehaviour : MonoBehaviour
{
}

/// <summary>Test controls and offers for the trade dummy (kept out of the MonoBehaviour so Unity only sees the bare interaction).</summary>
internal static class TradeDummyUi
{
    internal sealed class Offer
    {
        public string Label = "";
        public string[] Prefabs = Array.Empty<string>();
        public int[] Amounts = Array.Empty<int>();
    }

    private static readonly Offer[] Offers =
    {
        new Offer { Label = "Coins x120", Prefabs = new[] { "Coins" }, Amounts = new[] { 120 } },
        new Offer { Label = "Iron x10, Leather x5", Prefabs = new[] { "Iron", "LeatherScraps" }, Amounts = new[] { 10, 5 } },
        new Offer { Label = "Iron Sword, Honey x3", Prefabs = new[] { "SwordIron", "Honey" }, Amounts = new[] { 1, 3 } },
    };

    internal static readonly DummyPartner Partner = new DummyPartner();

    internal static void StartTrade(DummyPartner partner)
    {
        var session = new TradeSession(partner);
        TradeWindow.Open(session, BuildControls(session));
    }

    internal static VisualElement BuildControls(TradeSession session)
    {
        var card = UkControls.Card().Column().Pad(12);
        card.Add(UkControls.Text("TEST CONTROLS: Bjorn", 12, UkTheme.TextMuted, wrap: false));

        var offers = new VisualElement().Row();
        offers.style.flexWrap = Wrap.Wrap;
        offers.style.marginTop = 8;
        foreach (var offer in Offers)
        {
            var captured = offer;
            var label = offer.Label;
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

    private static void SetOffer(TradeSession session, Offer offer)
    {
        var entries = new List<TradeEntry>();
        for (var i = 0; i < offer.Prefabs.Length; i++)
        {
            var prefabName = offer.Prefabs[i];
            var amount = offer.Amounts[i];
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
