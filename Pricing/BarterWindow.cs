using System;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pricing;

/// <summary>
/// Buyer's window for an item-trade price: how many units to take, what you must hand over, and Trade.
/// </summary>
internal sealed class BarterWindow
{
    private const float CloseDistance = 7f;

    private static BarterWindow? _current;

    private readonly UkWindow _window;
    private readonly Container _container;
    private readonly ItemDrop.ItemData _hint;
    private readonly Label _quantityLabel;
    private readonly Label _cost;
    private readonly Label _have;
    private readonly Label _status;
    private readonly VisualElement _statusCard;
    private readonly Button _trade;
    private int _quantity = 1;

    internal static bool IsOpen => _current != null && _current._window.IsOpen;

    internal static void Open(Container container, ItemDrop.ItemData item)
    {
        _current?._window.CloseSilent();
        _current = new BarterWindow(container, item);
        _current.Refresh();
        _current._window.Open();
    }

    private BarterWindow(Container container, ItemDrop.ItemData item)
    {
        _container = container;
        _hint = item;
        _window = new UkWindow("drakes_shop_barter", 500);
        _window.SetHeader(Name(item), global::Localization.instance.Localize(container.m_name));

        var top = new VisualElement().Row();
        top.Add(UkControls.IconTile(item.GetIcon(), 64));
        var info = new VisualElement().Column();
        info.style.marginLeft = 14;
        info.Add(UkControls.Text(Name(item), 18, UkTheme.Text, heading: true, wrap: false));
        info.Add(UkControls.Text("Trade for: " + TradePrice.Summary(item), 14, UkTheme.Gold, wrap: true));
        top.Add(info);
        _window.Body.Add(top);

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

        _cost = UkControls.Text("", 14, UkTheme.TextMuted, wrap: true);
        _cost.style.marginTop = 12;
        _window.Body.Add(_cost);

        _have = UkControls.Text("", 14, UkTheme.TextMuted, wrap: true);
        _have.style.marginTop = 6;
        _window.Body.Add(_have);

        _statusCard = UkControls.Card(UkTheme.DangerBg).Column().Pad(10);
        _statusCard.style.marginTop = 14;
        _statusCard.style.display = DisplayStyle.None;
        _status = UkControls.Text("", 15, UkTheme.DangerText, wrap: true);
        _statusCard.Add(_status);
        _window.Body.Add(_statusCard);

        _trade = UkControls.MakeButton("Trade", Confirm, UkButtonKind.Primary, 130);
        _window.Footer.Add(UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary));
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(_trade);
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

    private ItemDrop.ItemData? Live => ShopPurchase.FindStockTrade(_container, _hint, TradePrice.Raw(_hint));

    private void Change(int delta)
    {
        var stock = Live?.m_stack ?? _hint.m_stack;
        _quantity = Mathf.Clamp(_quantity + delta, 1, Math.Max(1, stock));
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
        var stock = Live?.m_stack ?? 0;
        var live = Live ?? _hint;
        var wants = TradePrice.Wants(live);
        _quantity = Mathf.Clamp(_quantity, 1, Math.Max(1, stock));
        _quantityLabel.text = _quantity.ToString();

        if (stock <= 0 || wants.Count == 0)
        {
            _cost.text = "That item is no longer for trade.";
            _have.text = "";
            UkControls.SetButtonEnabled(_trade, false);
            return;
        }

        var costParts = wants.Select(w =>
        {
            var need = (long)w.Count * _quantity;
            return need == 1 ? TradeWant.DisplayName(w.Prefab) : $"{need}× {TradeWant.DisplayName(w.Prefab)}";
        });
        _cost.text = stock > 0
            ? $"{TradePrice.Summary(live)} each, {stock} in stock. You give: {string.Join(", ", costParts)}."
            : "That item is no longer in the shop.";

        var haveParts = wants.Select(w =>
        {
            var have = mine != null ? TradePrice.CountIn(mine, w.Prefab) : 0;
            var need = (long)w.Count * _quantity;
            var color = have >= need ? UkTheme.TextMuted : UkTheme.DangerText;
            return $"{have}/{need} {TradeWant.DisplayName(w.Prefab)}";
        });
        _have.text = "You have: " + string.Join(", ", haveParts);

        var canPay = mine != null && wants.All(w => TradePrice.CountIn(mine, w.Prefab) >= (long)w.Count * _quantity);
        UkControls.SetButtonEnabled(_trade, stock > 0 && canPay);
    }

    private void Confirm()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return;
        UkControls.SetButtonEnabled(_trade, false);
        SetStatus(global::DrakesItemShop.Trade.TradeLedger.ServerMode ? "Checking with the server..." : null);
        var quantity = _quantity;
        var hint = Live ?? _hint;
        var tradeKey = TradePrice.Raw(hint);
        ShopServer.ValidateTrade(_container, hint, quantity, tradeKey, refusal =>
        {
            if (refusal == null)
                refusal = ShopPurchase.Barter(_container, hint, quantity, tradeKey, player);
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
