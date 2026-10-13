using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Pricing;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.DisplayCard;

/// <summary>Player sells to the shop via a buy card: give items, get coins from the stand's buy budget.</summary>
internal sealed class SellWindow
{
    private const float CloseDistance = 7f;

    private static SellWindow? _current;

    private readonly UkWindow _window;
    private readonly Container _shop;
    private readonly int _offerIndex;
    private readonly Label _qtyLabel;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly VisualElement _statusCard;
    private readonly Button _sell;
    private int _deals = 1;

    internal static bool IsOpen => _current != null && _current._window.IsOpen;

    internal static void Open(Container shop, int offerIndex)
    {
        _current?._window.CloseSilent();
        _current = new SellWindow(shop, offerIndex);
        _current.Refresh();
        _current._window.Open();
    }

    private SellWindow(Container shop, int offerIndex)
    {
        _shop = shop;
        _offerIndex = offerIndex;
        var offer = CurrentOffer();
        var name = offer.IsValid ? BuyOffers.ItemLabel(offer.WantPrefab) : "item";

        _window = new UkWindow("drakes_shop_sell", 480);
        _window.SetHeader($"Sell {name}", global::Localization.instance.Localize(shop.m_name));

        var top = new VisualElement().Row();
        top.Add(UkControls.IconTile(offer.IsValid ? BuyOffers.ItemIcon(offer.WantPrefab) : null, 64));
        var info = new VisualElement().Column();
        info.style.marginLeft = 14;
        info.Add(UkControls.Text(name, 18, UkTheme.Text, heading: true, wrap: false));
        _summary = UkControls.Text("", 14, UkTheme.TextMuted, wrap: true);
        info.Add(_summary);
        top.Add(info);
        _window.Body.Add(top);

        var stepper = new VisualElement().Row();
        stepper.style.marginTop = 16;
        stepper.Add(UkControls.MakeIconButton("−", () => Change(-Step()), "Fewer (Shift: 10)"));
        _qtyLabel = UkControls.Text("1", 20, UkTheme.Text, heading: true, wrap: false);
        _qtyLabel.style.flexGrow = 1;
        _qtyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        stepper.Add(_qtyLabel);
        stepper.Add(UkControls.MakeIconButton("+", () => Change(Step()), "More (Shift: 10)"));
        _window.Body.Add(stepper);

        _statusCard = UkControls.Card(UkTheme.DangerBg).Column().Pad(10);
        _statusCard.style.marginTop = 14;
        _statusCard.style.display = DisplayStyle.None;
        _status = UkControls.Text("", 15, UkTheme.DangerText, wrap: true);
        _statusCard.Add(_status);
        _window.Body.Add(_statusCard);

        _sell = UkControls.MakeButton("Sell", Confirm, UkButtonKind.Primary, 130);
        _window.Footer.Add(UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary));
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(_sell);
        _window.ShowFooter(true);

        _window.Body.schedule.Execute(() =>
        {
            var player = Player.m_localPlayer;
            if (_window.IsOpen && (player == null || Vector3.Distance(player.transform.position, _shop.transform.position) > CloseDistance))
                _window.Close();
        }).Every(500);
    }

    private static int Step() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1;

    private BuyOfferEntry CurrentOffer()
    {
        var nview = _shop.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return default;
        var list = BuyOffers.All(nview.GetZDO());
        return _offerIndex >= 0 && _offerIndex < list.Count ? list[_offerIndex] : default;
    }

    private void Change(int delta)
    {
        var offer = CurrentOffer();
        if (!offer.IsValid)
            return;
        var player = Player.m_localPlayer;
        var have = player != null ? CountPrefab(player.GetInventory(), offer.WantPrefab) : 0;
        var maxByItems = Math.Max(1, have / Math.Max(1, offer.WantAmount));
        var maxByCap = offer.Cap > 0
            ? Math.Max(1, offer.Remaining / Math.Max(1, offer.WantAmount))
            : maxByItems;
        var zdo = _shop.GetComponent<ZNetView>()?.GetZDO();
        var budget = zdo != null ? BuyOffers.Spendable(_shop, zdo, offer) : 0;
        var maxByBudget = offer.PayCoins > 0 ? Math.Max(1, budget / offer.PayCoins) : maxByItems;
        var max = Math.Max(1, Math.Min(maxByItems, Math.Min(maxByCap, maxByBudget)));
        _deals = Mathf.Clamp(_deals + delta, 1, max);
        Refresh();
    }

    private void SetStatus(string? text)
    {
        var show = !string.IsNullOrEmpty(text);
        _statusCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        _status.text = text ?? "";
        _sell.SetEnabled(!show);
    }

    private void Refresh()
    {
        var offer = CurrentOffer();
        if (!offer.IsValid)
        {
            SetStatus("This buy offer is gone.");
            return;
        }

        var units = offer.WantAmount * _deals;
        var pay = offer.PayCoins * _deals;
        var name = BuyOffers.ItemLabel(offer.WantPrefab);
        var capLine = offer.Cap > 0 ? $"\nShop still wants {offer.Remaining} of {offer.Cap}." : "";
        _qtyLabel.text = _deals.ToString();
        _summary.text = $"Give {units} {name}  →  get {pay} coins{capLine}";

        var refuse = ShopSell.RefuseReason(_shop, _offerIndex, _deals, Player.m_localPlayer);
        SetStatus(refuse);
        if (refuse == null)
            _sell.SetEnabled(true);
    }

    private void Confirm()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return;
        var msg = ShopSell.Sell(_shop, _offerIndex, _deals, player);
        if (msg != null)
        {
            SetStatus(msg);
            Trade.TradeNet.Say(msg);
            Refresh();
            return;
        }
        Trade.TradeNet.Say("Sold.");
        _shop.GetComponent<BuyCardShelf>()?.Dirty();
        _window.Close();
    }

    private static int CountPrefab(Inventory inv, string prefab) =>
        inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).Sum(i => i.m_stack);
}

/// <summary>
/// Sell into the shop: take items, pay from buy budget, stash in the stand's own inventory for now.
/// Stand-linked buy crates are tabled — same meshes later, different prefab/behaviour.
/// </summary>
internal static class ShopSell
{
    /// <summary>Null when the deal can go through; otherwise a player-facing refusal.</summary>
    public static string? RefuseReason(Container shop, int offerIndex, int deals, Player? player)
    {
        if (player == null)
            return "No player.";
        if (!ShopState.IsOpen(shop))
            return "Sorry — the shop isn't open for sales right now.";
        var nview = shop.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return "Shop isn't ready.";
        var zdo = nview.GetZDO();
        var list = BuyOffers.All(zdo);
        if (offerIndex < 0 || offerIndex >= list.Count)
            return "That buy offer is gone.";
        var offer = list[offerIndex];
        if (!offer.IsValid)
            return "That buy offer is gone.";
        if (!offer.Accepting)
            return $"Sorry — the shop isn't taking sales of {BuyOffers.ItemLabel(offer.WantPrefab)} right now.";

        deals = Math.Max(1, deals);
        var units = offer.WantAmount * deals;
        var pay = offer.PayCoins * deals;
        if (offer.Cap > 0 && units > offer.Remaining)
            return $"Sorry — the shop only wants {offer.Remaining} more {BuyOffers.ItemLabel(offer.WantPrefab)}.";

        if (!BuyOffers.Enabled(zdo))
            return "Sorry — this stand isn't buying right now.";
        if (BuyOffers.Spendable(shop, zdo, offer) < pay)
            return OutOfMoney(offer);

        var have = CountPrefab(player.GetInventory(), offer.WantPrefab);
        if (have < units)
            return $"You need {units} {BuyOffers.ItemLabel(offer.WantPrefab)} (you have {have}).";

        if (!CanFitCoins(player.GetInventory(), pay))
            return "No room for the coins — make space and try again.";

        if (!CanStash(shop, zdo, offer.WantPrefab, units))
            return BuyCrates.HasCrateFor(zdo, offer.WantPrefab)
                ? $"The {BuyOffers.ItemLabel(offer.WantPrefab)} crates are full — the owner needs to empty them."
                : "The shop has no room for that — empty the stand or make space.";

        return null;
    }

    /// <summary>Only offers that may dip into the till suggest buying something: that's what would refill it.</summary>
    public static string OutOfMoney(BuyOfferEntry offer) => offer.TillTopUp
        ? "Sorry — the shop is out of coins. Maybe buy something?"
        : "Sorry — the shop is out of coins for buying right now.";

    /// <summary>Owner takes bought <paramref name="prefab"/> out of the stand's buy crates. Returns units moved.</summary>
    public static int Collect(Container shop, string prefab, Player player)
    {
        var nview = shop.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return 0;
        nview.ClaimOwnership();
        var zdo = nview.GetZDO();
        var left = BuyOffers.HeldOf(zdo, prefab);
        var moved = 0;
        while (left > 0)
        {
            var entry = Trade.TradeItemCodec.Make(prefab, left);
            if (entry == null)
                break;
            var take = entry.Item.m_stack;
            if (!player.GetInventory().AddItem(entry.Item))
                break;
            left -= take;
            moved += take;
        }
        if (moved > 0)
        {
            BuyOffers.AddHeld(zdo, prefab, -moved);
            shop.GetComponent<BuyCardShelf>()?.Dirty();
        }
        return moved;
    }

    public static string? Sell(Container shop, int offerIndex, int deals, Player player)
    {
        var refuse = RefuseReason(shop, offerIndex, deals, player);
        if (refuse != null)
            return refuse;

        var nview = shop.GetComponent<ZNetView>()!;
        nview.ClaimOwnership();
        var zdo = nview.GetZDO();
        var offer = BuyOffers.All(zdo)[offerIndex];
        deals = Math.Max(1, deals);
        var units = offer.WantAmount * deals;
        var pay = offer.PayCoins * deals;

        if (!TakePrefab(player.GetInventory(), offer.WantPrefab, units))
            return $"You need {units} {BuyOffers.ItemLabel(offer.WantPrefab)}.";

        if (!BuyOffers.TrySpend(shop, zdo, offer, pay, out var fromBudget, out var fromTill))
        {
            GivePrefab(player.GetInventory(), offer.WantPrefab, units);
            return OutOfMoney(offer);
        }

        if (!ShopState.GiveCoins(player.GetInventory(), pay))
        {
            BuyOffers.Refund(shop, zdo, fromBudget, fromTill);
            GivePrefab(player.GetInventory(), offer.WantPrefab, units);
            return "No room for the coins — make space and try again.";
        }

        if (!TryStash(shop, zdo, offer.WantPrefab, units))
        {
            ShopState.TakeCoins(player.GetInventory(), pay);
            BuyOffers.Refund(shop, zdo, fromBudget, fromTill);
            GivePrefab(player.GetInventory(), offer.WantPrefab, units);
            return "The shop has no room for that — empty the stand or make space.";
        }

        BuyOffers.AddFilled(zdo, offerIndex, units);
        shop.GetComponent<BuyCardShelf>()?.Dirty();
        return null;
    }

    private static int CountPrefab(Inventory inv, string prefab) =>
        inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).Sum(i => i.m_stack);

    private static bool CanFitCoins(Inventory inv, int coins)
    {
        if (coins <= 0)
            return true;
        if (inv.GetAllItems().Any(ShopState.IsCoinsStack))
            return true;
        return inv.GetEmptySlots() > 0;
    }

    /// <summary>Goods go into the item's buy crates when the layout has any for it, else into the stand's own inventory.</summary>
    private static bool CanStash(Container shop, ZDO zdo, string prefab, int units)
    {
        if (BuyCrates.HasCrateFor(zdo, prefab))
            return BuyCrates.Capacity(zdo, prefab) - BuyOffers.HeldOf(zdo, prefab) >= units;
        var inv = shop.GetInventory();
        if (inv == null)
            return false;
        var drop = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();
        if (drop == null)
            return false;
        var maxStack = Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
        var left = units;
        foreach (var stack in inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab))
            left -= Math.Max(0, maxStack - stack.m_stack);
        left -= inv.GetEmptySlots() * maxStack;
        return left <= 0;
    }

    private static bool TryStash(Container shop, ZDO zdo, string prefab, int units)
    {
        if (BuyCrates.HasCrateFor(zdo, prefab))
        {
            if (!CanStash(shop, zdo, prefab, units))
                return false;
            BuyOffers.AddHeld(zdo, prefab, units);
            return true;
        }
        var inv = shop.GetInventory();
        if (inv == null)
            return false;
        var left = units;
        while (left > 0)
        {
            var entry = Trade.TradeItemCodec.Make(prefab, left);
            if (entry == null)
                return false;
            var take = entry.Item.m_stack;
            if (!inv.AddItem(entry.Item))
                return false;
            left -= take;
        }
        ShopState.NotifyChanged(shop);
        return true;
    }

    private static bool TakePrefab(Inventory inv, string prefab, int amount)
    {
        if (CountPrefab(inv, prefab) < amount)
            return false;
        var left = amount;
        foreach (var stack in inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).ToList())
        {
            var take = Math.Min(stack.m_stack, left);
            inv.RemoveItem(stack, take);
            left -= take;
            if (left == 0)
                return true;
        }
        return left == 0;
    }

    private static bool GivePrefab(Inventory inv, string prefab, int amount)
    {
        var left = amount;
        while (left > 0)
        {
            var entry = Trade.TradeItemCodec.Make(prefab, left);
            if (entry == null)
                return false;
            var take = entry.Item.m_stack;
            if (!inv.AddItem(entry.Item))
                return false;
            left -= take;
        }
        return true;
    }
}
