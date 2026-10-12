using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pricing;

/// <summary>Where the Price Ledger opens (default Shift+L).</summary>
internal static class PriceLedgerSettings
{
    private static ConfigEntry<KeyboardShortcut> _shortcut = default!;

    internal static KeyboardShortcut Shortcut => _shortcut.Value;

    internal static void Bind(ConfigFile config) =>
        _shortcut = config.Bind("07 Shop", "Price ledger shortcut", new KeyboardShortcut(KeyCode.L, KeyCode.LeftShift),
            "Opens the Price Ledger: every item in the shop pieces near you (within 15 m) that you manage, with their prices, for pricing in bulk.");
}

/// <summary>
/// The Price Ledger: a list of what is in the shop pieces around you (display cases and racks you manage), with a price field per item
/// and a few bulk buttons. For bulk work only: buying never needs it.
/// </summary>
internal sealed class PriceLedger
{
    private const float Range = 15f;

    private static PriceLedger? _current;

    private readonly UkWindow _window;
    private readonly VisualElement _list = new();
    private readonly Label _status;
    private List<(Container Piece, ItemDrop.ItemData Item)> _rows = new();

    internal static void Poll()
    {
        if (!PriceLedgerSettings.Shortcut.IsDown() || Player.m_localPlayer == null || UkWindow.AnyOpen)
            return;
        if (Chat.instance != null && Chat.instance.HasFocus())
            return;
        if (Console.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || TextInput.IsVisible())
            return;
        _current = new PriceLedger();
        _current.Fill();
        _current._window.Open();
    }

    private PriceLedger()
    {
        _window = new UkWindow("drakes_price_ledger", 700);
        _window.SetHeader("Price Ledger", "Shops within 15 m that you manage");
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 420;
        scroll.Add(_list);
        var card = UkControls.Card().Pad(10);
        card.Add(scroll);
        _window.Body.Add(card);

        _status = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _status.style.flexGrow = 1;
        _status.style.marginLeft = 12;
        _window.Footer.Add(UkControls.MakeButton("Unpriced from catalog", FromCatalog, UkButtonKind.Secondary));
        _window.Footer.Add(Gap(UkControls.MakeButton("+10%", () => Adjust(1.1f), UkButtonKind.Secondary)));
        _window.Footer.Add(Gap(UkControls.MakeButton("Round to 5", () => Adjust(1f, round: true), UkButtonKind.Secondary)));
        _window.Footer.Add(_status);
        _window.Footer.Add(UkControls.MakeButton("Save to catalog", ToCatalog, UkButtonKind.Primary));
        _window.ShowFooter(true);
    }

    private static Button Gap(Button button)
    {
        button.style.marginLeft = 8;
        return button;
    }

    private void Fill()
    {
        var player = Player.m_localPlayer;
        _rows = new List<(Container, ItemDrop.ItemData)>();
        foreach (var piece in UnityEngine.Object.FindObjectsOfType<Container>())
        {
            if (!ShopState.IsPiece(piece) || Vector3.Distance(piece.transform.position, player.transform.position) > Range
                || !ShopState.CanManage(piece, player))
                continue;
            foreach (var item in piece.GetInventory().GetAllItems().OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x))
                _rows.Add((piece, item));
        }
        _rows = _rows.OrderBy(r => Vector3.Distance(r.Piece.transform.position, player.transform.position)).ToList();

        _list.Clear();
        if (_rows.Count == 0)
            _list.Add(UkControls.Text("No shop pieces with items near you that you manage.", 14, UkTheme.TextMuted));
        foreach (var (piece, item) in _rows)
        {
            var row = new VisualElement().Row();
            row.style.marginBottom = 6;
            row.Add(UkControls.IconTile(item.GetIcon(), 34));
            var name = UkControls.Text(ItemDisplayService.GetDisplayNameForUi(item, localize: true), 15, UkTheme.Text, wrap: false);
            name.style.marginLeft = 10;
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            row.Add(name);
            var where = UkControls.Text(global::Localization.instance.Localize(piece.m_name), 13, UkTheme.TextMuted, wrap: false);
            where.style.width = 150;
            row.Add(where);
            var stock = UkControls.Text("x" + item.m_stack, 14, UkTheme.TextMuted, wrap: false);
            stock.style.width = 50;
            row.Add(stock);
            var field = new TextField { value = ItemPrice.Raw(item), maxLength = 24 };
            field.style.width = 120;
            field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            if (field.Q<VisualElement>("unity-text-input") is { } input)
            {
                input.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 10);
                input.style.color = UkTheme.Text;
                input.style.fontSize = 15;
            }
            var captured = (piece, item);
            field.RegisterValueChangedCallback(e => SetPrice(captured.piece, captured.item, e.newValue));
            row.Add(field);
            _list.Add(row);
        }
    }

    private static void SetPrice(Container piece, ItemDrop.ItemData item, string text) =>
        ShopState.SetPrice(piece, item, text);

    private void FromCatalog()
    {
        var count = 0;
        foreach (var (piece, item) in _rows)
        {
            if (ItemPrice.Raw(item).Length > 0 || Catalog.Get(item) is not { Length: > 0 } saved)
                continue;
            SetPrice(piece, item, saved);
            count++;
        }
        _status.text = count + " priced from the catalog.";
        Fill();
    }

    private void Adjust(float factor, bool round = false)
    {
        var count = 0;
        foreach (var (piece, item) in _rows)
        {
            var coins = ItemPrice.Coins(item);
            if (coins <= 0)
                continue;
            var changed = Mathf.Max(1, Mathf.RoundToInt(coins * factor));
            if (round)
                changed = Mathf.Max(5, Mathf.RoundToInt(changed / 5f) * 5);
            SetPrice(piece, item, changed.ToString());
            count++;
        }
        _status.text = count + " prices changed.";
        Fill();
    }

    private void ToCatalog()
    {
        var count = 0;
        foreach (var (_, item) in _rows)
        {
            var price = ItemPrice.Raw(item);
            if (price.Length == 0)
                continue;
            Catalog.Set(item, price);
            count++;
        }
        _status.text = count + " saved to the catalog.";
    }
}
