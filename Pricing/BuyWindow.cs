using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pricing;

/// <summary>
/// Buyer window: quantity of deals, pick one payment option when several ORs exist, then buy/trade.
/// </summary>
internal sealed class BuyWindow
{
    private const float CloseDistance = 7f;

    private static BuyWindow? _current;

    private readonly UkWindow _window;
    private readonly Container _container;
    private readonly ItemDrop.ItemData _hint;
    private readonly Label _quantityLabel;
    private readonly Label _total;
    private readonly Label _have;
    private readonly Label _status;
    private readonly VisualElement _statusCard;
    private readonly VisualElement _optionHost;
    private readonly Button _buy;
    private int _quantity = 1;
    private int _optionIndex;

    internal static bool IsOpen => _current != null && _current._window.IsOpen;

    internal static void Open(Container container, ItemDrop.ItemData item)
    {
        _current?._window.CloseSilent();
        _current = new BuyWindow(container, item);
        _current.Refresh();
        _current._window.Open();
    }

    private BuyWindow(Container container, ItemDrop.ItemData item)
    {
        _container = container;
        _hint = item;
        _window = new UkWindow("drakes_shop_buy", 520);
        _window.SetHeader(Name(item), global::Localization.instance.Localize(container.m_name));

        var top = new VisualElement().Row();
        top.Add(UkControls.IconTile(item.GetIcon(), 64));
        var info = new VisualElement().Column();
        info.style.marginLeft = 14;
        info.Add(UkControls.Text(Name(item), 18, UkTheme.Text, heading: true, wrap: false));
        _total = UkControls.Text("", 14, UkTheme.TextMuted, wrap: true);
        info.Add(_total);
        top.Add(info);
        _window.Body.Add(top);

        _optionHost = new VisualElement().Column();
        _optionHost.style.marginTop = 12;
        _window.Body.Add(_optionHost);

        var stepper = new VisualElement().Row();
        stepper.style.marginTop = 16;
        var minus = UkControls.MakeIconButton("−", () => Change(-Step()), "Fewer (Shift: 10)");
        var plus = UkControls.MakeIconButton("+", () => Change(Step()), "More (Shift: 10)");
        _quantityLabel = UkControls.Text("1", 20, UkTheme.Text, heading: true, wrap: false);
        _quantityLabel.style.flexGrow = 1;
        _quantityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        stepper.Add(minus);
        stepper.Add(_quantityLabel);
        stepper.Add(plus);
        _window.Body.Add(stepper);

        _have = UkControls.Text("", 14, UkTheme.TextMuted, wrap: true);
        _have.style.marginTop = 12;
        _window.Body.Add(_have);

        _statusCard = UkControls.Card(UkTheme.DangerBg).Column().Pad(10);
        _statusCard.style.marginTop = 14;
        _statusCard.style.display = DisplayStyle.None;
        _status = UkControls.Text("", 15, UkTheme.DangerText, wrap: true);
        _statusCard.Add(_status);
        _window.Body.Add(_statusCard);

        _buy = UkControls.MakeButton("Buy", Confirm, UkButtonKind.Primary, 130);
        _window.Footer.Add(UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary));
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(_buy);
        _window.ShowFooter(true);

        _window.Body.schedule.Execute(() =>
        {
            var player = Player.m_localPlayer;
            if (_window.IsOpen && (player == null || Vector3.Distance(player.transform.position, _container.transform.position) > CloseDistance))
                _window.Close();
        }).Every(500);
    }

    private static string Name(ItemDrop.ItemData item) => ItemDisplayService.GetDisplayNameForUi(item, localize: true);

    private static int Step() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1;

    private ItemDrop.ItemData? Live => ShopPurchase.FindStockPay(_container, _hint, PayOptions.Raw(_hint));

    private void Change(int delta)
    {
        var live = Live ?? _hint;
        var lot = ItemLot.Of(live);
        var stock = live.m_stack;
        var maxDeals = Math.Max(1, stock / Math.Max(1, lot));
        _quantity = Mathf.Clamp(_quantity + delta, 1, maxDeals);
        Refresh();
    }

    private void SetStatus(string? text)
    {
        var show = !string.IsNullOrEmpty(text);
        _statusCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        _status.text = text ?? "";
    }

    private void Refresh()
    {
        var player = Player.m_localPlayer;
        var mine = player?.GetInventory();
        var live = Live ?? _hint;
        var opts = PayOptions.All(live);
        var lot = ItemLot.Of(live);
        var stock = Live?.m_stack ?? 0;
        var maxDeals = Math.Max(1, stock / Math.Max(1, lot));
        _quantity = Mathf.Clamp(_quantity, 1, maxDeals);
        if (_optionIndex >= opts.Count)
            _optionIndex = 0;

        _optionHost.Clear();
        if (opts.Count > 1)
        {
            _optionHost.Add(UkControls.Text("Pay with", 12, UkTheme.TextMuted, wrap: false));
            for (var i = 0; i < opts.Count; i++)
            {
                var idx = i;
                var btn = UkControls.MakeButton(opts[i].Label, () =>
                {
                    _optionIndex = idx;
                    Refresh();
                }, idx == _optionIndex ? UkButtonKind.Selected : UkButtonKind.Ghost);
                btn.style.marginTop = 4;
                _optionHost.Add(btn);
            }
        }
        else if (opts.Count == 1)
            _optionHost.Add(UkControls.Text("Pay: " + opts[0].Label, 14, UkTheme.Gold, wrap: true));

        _quantityLabel.text = _quantity.ToString();
        var get = _quantity * lot;
        if (stock <= 0 || opts.Count == 0)
        {
            _total.text = "That item is no longer for sale.";
            _have.text = "";
            UkControls.SetButtonEnabled(_buy, false);
            return;
        }

        var opt = opts[_optionIndex];
        _total.text = $"{opt.Label} each deal → {lot} {Name(live)}. " +
                      $"{stock} in stock. Buying {_quantity} deal(s) = {get} item(s).";

        if (mine == null)
        {
            _have.text = "";
            UkControls.SetButtonEnabled(_buy, false);
            return;
        }

        var lines = new List<string>();
        if (opt.Coins > 0)
            lines.Add($"coins {ShopState.CountCoins(mine)}/{(long)opt.Coins * _quantity}");
        foreach (var w in opt.Items)
            lines.Add($"{TradeWant.DisplayName(w.Prefab)} {TradePrice.CountIn(mine, w.Prefab)}/{(long)w.Count * _quantity}");
        _have.text = "You have: " + string.Join(", ", lines);
        UkControls.SetButtonEnabled(_buy, opt.BuyerCanPay(mine, _quantity));
    }

    private void Confirm()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return;
        UkControls.SetButtonEnabled(_buy, false);
        SetStatus(Trade.TradeLedger.ServerMode ? "Checking with the server..." : null);
        var deals = _quantity;
        var optionIndex = _optionIndex;
        var hint = Live ?? _hint;
        var payRaw = PayOptions.Raw(hint);
        ShopServer.ValidatePay(_container, hint, deals, payRaw, refusal =>
        {
            if (refusal == null)
                refusal = ShopPurchase.Purchase(_container, hint, deals, optionIndex, payRaw, player);
            if (refusal != null)
            {
                if (_window.IsOpen)
                {
                    SetStatus(refusal);
                    Refresh();
                }
                return;
            }
            if (_window.IsOpen)
                _window.Close();
        });
    }
}

/// <summary>The sale itself: checked first, then done in one step on the buyer's client.</summary>
internal static class ShopPurchase
{
    private static bool PrefabMatches(ItemDrop.ItemData item, string prefab) =>
        item.m_dropPrefab != null && (
            string.Equals(item.m_dropPrefab.name, prefab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.m_dropPrefab.name.Replace("(Clone)", ""), prefab.Replace("(Clone)", ""), StringComparison.OrdinalIgnoreCase));

    public static ItemDrop.ItemData? FindStockPay(Container container, ItemDrop.ItemData hint, string payRaw)
    {
        var inventory = container.GetInventory();
        var prefab = hint.m_dropPrefab != null ? hint.m_dropPrefab.name : "";
        if (prefab.Length == 0)
            return null;

        var at = inventory.GetItemAt(hint.m_gridPos.x, hint.m_gridPos.y);
        if (at != null && PrefabMatches(at, prefab))
        {
            if (string.IsNullOrWhiteSpace(payRaw) || PayOptions.Same(PayOptions.Raw(at), payRaw) || !PayOptions.Has(at))
            {
                if (!string.IsNullOrWhiteSpace(payRaw) && !PayOptions.Has(at))
                {
                    at.m_customData ??= new Dictionary<string, string>();
                    at.m_customData[PayOptions.Key] = payRaw.Trim();
                }
                return at;
            }
        }

        var priced = inventory.GetAllItems()
            .FirstOrDefault(i => PrefabMatches(i, prefab) && PayOptions.Same(PayOptions.Raw(i), payRaw));
        if (priced != null)
            return priced;

        return inventory.GetAllItems().FirstOrDefault(i => PrefabMatches(i, prefab) && PayOptions.Has(i))
               ?? inventory.GetAllItems().FirstOrDefault(i => PrefabMatches(i, prefab));
    }

    /// <summary>Null when the sale went through. <paramref name="deals"/> is number of deals; each gives <see cref="ItemLot"/> items.</summary>
    public static string? Purchase(Container container, ItemDrop.ItemData hint, int deals, int optionIndex, string expectedPay, Player player)
    {
        var shopInventory = container.GetInventory();
        var mine = player.GetInventory();
        if (!ShopState.IsOpen(container))
            return "The shop is closed.";
        if (deals <= 0)
            return "That isn't a valid purchase.";

        var item = FindStockPay(container, hint, expectedPay);
        if (item == null)
            return "Sold out or changed. Look again.";

        var opts = PayOptions.All(item);
        if (opts.Count == 0 || !PayOptions.Same(PayOptions.Raw(item), expectedPay))
            return "The price changed. Look again.";
        if (optionIndex < 0 || optionIndex >= opts.Count)
            return "The price changed. Look again.";

        var opt = opts[optionIndex];
        var lot = ItemLot.Of(item);
        var take = deals * lot;
        if (item.m_stack < take)
            return "Sold out or changed. Look again.";

        if (ShopState.IsCoinsStack(item) && opt.Coins > 0 && opt.Items.Count == 0)
            return "Coins aren't sold for coins only.";

        if (!opt.BuyerCanPay(mine, deals))
            return "You can't afford that payment.";

        var bought = item.Clone();
        bought.m_stack = take;
        PayOptions.Clear(bought);
        ItemLot.Clear(bought);
        if (!mine.CanAddItem(bought, take))
            return "No room in your inventory.";

        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        var spot = item.m_gridPos;
        var prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
        var payKey = expectedPay.Trim();

        var coinTotal = (long)opt.Coins * deals;
        if (coinTotal > int.MaxValue / 2)
            return "That is too many.";

        var taken = new List<(string Prefab, int Count)>();
        foreach (var w in opt.Items)
        {
            var need = w.Count * deals;
            if (!TradePrice.TakeFrom(mine, w.Prefab, need))
            {
                foreach (var t in taken)
                    TradePrice.GiveTo(mine, t.Prefab, t.Count);
                return $"Not enough {TradeWant.DisplayName(w.Prefab)}.";
            }
            taken.Add((w.Prefab, need));
        }

        if (coinTotal > 0 && !ShopState.TakeCoins(mine, (int)coinTotal))
        {
            foreach (var t in taken)
                TradePrice.GiveTo(mine, t.Prefab, t.Count);
            return "Not enough coins.";
        }

        var deposited = new List<(string Prefab, int Count)>();
        foreach (var t in taken)
        {
            if (!TradePrice.GiveTo(shopInventory, t.Prefab, t.Count))
            {
                foreach (var d in deposited)
                    TradePrice.TakeFrom(shopInventory, d.Prefab, d.Count);
                foreach (var refund in taken)
                    TradePrice.GiveTo(mine, refund.Prefab, refund.Count);
                if (coinTotal > 0)
                    ShopState.GiveCoins(mine, (int)coinTotal);
                return "The shop has no room for your payment.";
            }
            deposited.Add(t);
        }

        shopInventory.RemoveItem(item, take);
        mine.AddItem(bought);
        if (container.GetComponent<DisplayCaseBehaviour>() != null
            && shopInventory.GetItemAt(spot.x, spot.y) == null)
        {
            var next = shopInventory.GetAllItems().FirstOrDefault(o => o.m_gridPos.y > 0 && o.m_dropPrefab != null
                && PrefabMatches(o, prefabName) && PayOptions.Same(PayOptions.Raw(o), payKey));
            if (next != null)
                next.m_gridPos = spot;
        }

        ShopState.NotifyChanged(container);
        ShopState.AddSale(container, player.GetPlayerName(), name, take, (int)coinTotal);
        Trade.TradeNet.Say(coinTotal > 0 && taken.Count == 0
            ? $"Bought {take} {name} for {coinTotal} coins."
            : $"Bought {take} {name} ({opt.Label}).");
        return null;
    }

    // --- Legacy helpers kept for older call sites / server coin checks ---

    public static ItemDrop.ItemData? FindStock(Container container, ItemDrop.ItemData hint, int price) =>
        FindStockPay(container, hint, price > 0 ? $"c:{price}" : PayOptions.Raw(hint));

    public static ItemDrop.ItemData? FindStockTrade(Container container, ItemDrop.ItemData hint, string tradeKey) =>
        FindStockPay(container, hint, string.IsNullOrWhiteSpace(tradeKey) ? "" : tradeKey);

    public static string? Buy(Container container, ItemDrop.ItemData hint, int quantity, int expectedPrice, Player player) =>
        Purchase(container, hint, quantity, 0, $"c:{expectedPrice}", player);

    public static string? Barter(Container container, ItemDrop.ItemData hint, int quantity, string expectedTrade, Player player) =>
        Purchase(container, hint, quantity, 0, expectedTrade, player);
}
