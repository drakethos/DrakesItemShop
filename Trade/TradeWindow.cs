using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Trade;

/// <summary>
/// The trade window (Uk toolkit UI): your offer on the left, theirs on the right, your inventory underneath. Click an
/// inventory item to put its stack on offer, click an offered stack to select it (then +/- its amount) and click it again to
/// take it back. Both sides press Accept. Built from <see cref="TradeSession"/>, which holds all the rules.
/// </summary>
internal sealed class TradeWindow
{
    private const int SlotSize = 64;
    private const int InventoryTile = 52;

    private static TradeWindow? _current;

    private readonly UkWindow _window;
    private readonly TradeSession _session;
    private readonly VisualElement _mineHost = new();
    private readonly VisualElement _theirsHost = new();
    private readonly VisualElement _inventoryHost = new();
    private readonly VisualElement _extraHost = new();
    private readonly Label _status;
    private readonly Button _acceptButton;
    private TradeEntry? _selected;

    public static bool IsOpen => _current != null && _current._window.IsOpen;

    /// <summary>Open the window for a session. <paramref name="extra"/> (the test dummy's controls) sits under your inventory.</summary>
    public static void Open(TradeSession session, VisualElement? extra = null)
    {
        _current?._window.CloseSilent();
        _current = new TradeWindow(session, extra);
        _current.Refresh();
        _current._window.Open();
    }

    private TradeWindow(TradeSession session, VisualElement? extra)
    {
        _session = session;
        _window = new UkWindow("drakes_trade", 800);
        _window.SetHeader($"Trading with {session.Partner.Name}", "Both of you press Accept to swap");

        var boxes = new VisualElement().Row(Align.Stretch);
        _mineHost.Grow();
        _theirsHost.Grow();
        boxes.AddGapped(14, _mineHost, _theirsHost);

        var inventoryTitle = UkControls.Text("YOUR INVENTORY", 12, UkTheme.TextMuted, wrap: false);
        inventoryTitle.style.marginTop = 16;
        inventoryTitle.style.marginBottom = 6;
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 230;
        scroll.Add(_inventoryHost);

        _window.Body.Add(boxes);
        _window.Body.Add(inventoryTitle);
        _window.Body.Add(UkControls.Card().Pad(10));
        var card = _window.Body[_window.Body.childCount - 1];
        card.Add(scroll);
        if (extra != null)
        {
            _extraHost.style.marginTop = 14;
            _extraHost.Add(extra);
            _window.Body.Add(_extraHost);
        }

        _status = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _acceptButton = UkControls.MakeButton("Accept", _session.Accept, UkButtonKind.Primary, 130);
        var cancel = UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary);
        _window.Footer.Add(cancel);
        _status.style.marginLeft = 16;
        _status.style.flexGrow = 1;
        _window.Footer.Add(_status);
        _window.Footer.Add(_acceptButton);
        _window.ShowFooter(true);

        _session.Changed += Refresh;
        _window.Closed = () =>
        {
            _session.Changed -= Refresh;
            if (!_session.Locked)
                _session.Cancel();
        };
    }

    private void Refresh()
    {
        if (_selected != null && !_session.Mine.Contains(_selected))
            _selected = null;

        Fill(_mineHost, "YOU OFFER", _session.Mine, mine: true, _session.MineAccepted);
        Fill(_theirsHost, $"{_session.Partner.Name.ToUpperInvariant()} OFFERS", _session.Theirs, mine: false, _session.TheirsAccepted);
        FillInventory();

        _status.text = _session.Message;
        UkControls.SetButtonEnabled(_acceptButton, _session.CanAccept && !_session.MineAccepted);
        _acceptButton.text = _session.Done ? "Done" : _session.MineAccepted ? "Accepted" : "Accept";

        if (_session.Done)
            _window.Body.schedule.Execute(() => _window.Close()).StartingIn(1400);
    }

    private void Fill(VisualElement host, string title, List<TradeEntry> entries, bool mine, bool accepted)
    {
        host.Clear();
        var card = UkControls.Card().Column().Pad(12);
        var head = new VisualElement().Row();
        head.Add(UkControls.Text(title, 12, UkTheme.TextMuted, wrap: false));
        head.Add(UkControls.Spacer());
        if (accepted)
            head.Add(UkControls.Text("Accepted", 13, UkTheme.Ok, wrap: false));
        card.Add(head);

        var slots = new VisualElement().Row(Align.FlexStart);
        slots.style.flexWrap = Wrap.Wrap;
        slots.style.marginTop = 8;
        for (var i = 0; i < TradeSession.MaxSlots; i++)
        {
            var entry = i < entries.Count ? entries[i] : null;
            var slot = Slot(entry, entry != null && entry == _selected);
            slot.style.marginRight = 8;
            slot.style.marginBottom = 8;
            if (entry != null && mine)
            {
                var captured = entry;
                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_selected == captured)
                        _session.RemoveMine(captured);
                    else
                    {
                        _selected = captured;
                        Refresh();
                    }
                });
            }
            slots.Add(slot);
        }
        card.Add(slots);

        // Amount of the selected stack.
        if (mine && _selected != null)
        {
            var line = new VisualElement().Row();
            line.Add(UkControls.Text($"{Name(_selected.Item)}  x{_selected.Amount} of {_selected.Item.m_stack}", 14, UkTheme.TextBody, wrap: false));
            line.Add(UkControls.Spacer());
            var entry = _selected;
            var minus = UkControls.MakeIconButton("−", () => _session.ChangeAmount(entry, Step(-1)), "Fewer (Shift: 10)");
            var plus = UkControls.MakeIconButton("+", () => _session.ChangeAmount(entry, Step(1)), "More (Shift: 10)");
            plus.style.marginLeft = 6;
            line.Add(minus);
            line.Add(plus);
            card.Add(line);
        }
        else
        {
            card.Add(UkControls.Text(entries.Count == 0 ? "Nothing yet" : $"{entries.Count} of {TradeSession.MaxSlots} slots", 13, UkTheme.TextMuted, wrap: false));
        }

        host.Add(card);
    }

    private static int Step(int direction) =>
        direction * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1);

    private VisualElement Slot(TradeEntry? entry, bool selected)
    {
        var slot = new VisualElement().Size(SlotSize, SlotSize).Fixed().Fill(UkTheme.Field).Round(UkTheme.Radius)
            .Border(selected ? UkTheme.Accent : entry == null ? UkTheme.LineSoft : UkTheme.Line, selected ? 2f : 1f);
        slot.style.alignItems = Align.Center;
        slot.style.justifyContent = Justify.Center;
        if (entry == null)
            return slot;

        slot.Add(UkControls.IconTile(entry.Item.GetIcon(), SlotSize - 12));
        if (entry.Amount > 1)
            slot.Add(Corner(entry.Amount.ToString(), right: true));
        if (entry.Item.m_quality > 1)
            slot.Add(Corner("Q" + entry.Item.m_quality, right: false));
        _window.Tips.Attach(slot, () => Name(entry.Item));
        return slot;
    }

    private static Label Corner(string text, bool right)
    {
        var label = UkControls.Text(text, 12, right ? UkTheme.Gold : UkTheme.TextMuted, wrap: false);
        label.style.position = Position.Absolute;
        label.style.bottom = 2;
        if (right)
            label.style.right = 4;
        else
            label.style.left = 4;
        return label;
    }

    private void FillInventory()
    {
        _inventoryHost.Clear();
        _inventoryHost.style.flexDirection = FlexDirection.Row;
        _inventoryHost.style.flexWrap = Wrap.Wrap;
        var inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
        if (inventory == null)
            return;

        // Equipped gear stays put: only what you can hand over.
        foreach (var item in inventory.GetAllItems().Where(i => !i.m_equipped))
        {
            var onOffer = _session.Mine.Any(e => e.Item == item);
            var tile = new VisualElement().Size(InventoryTile + 8, InventoryTile + 8).Fixed().Fill(onOffer ? UkTheme.Active : UkTheme.Field)
                .Round(UkTheme.RadiusSmall).Border(onOffer ? UkTheme.Accent : UkTheme.LineSoft);
            tile.style.marginRight = 6;
            tile.style.marginBottom = 6;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.opacity = onOffer ? 0.55f : 1f;
            tile.Add(UkControls.IconTile(item.GetIcon(), InventoryTile));
            if (item.m_stack > 1)
                tile.Add(Corner(item.m_stack.ToString(), right: true));
            var captured = item;
            tile.RegisterCallback<ClickEvent>(_ => _session.ToggleMine(captured));
            _window.Tips.Attach(tile, () => Name(captured));
            _inventoryHost.Add(tile);
        }
    }

    private static string Name(ItemDrop.ItemData item) =>
        ItemDisplayService.GetDisplayNameForUi(item, localize: true);
}
