using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.DisplayCard;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pieces.Layout;

/// <summary>
/// Layout tab → Buy crates: a top-down 1 m grid around the stand. Pick a crate type, click the floor to set it down (up to one
/// per display slot), click a crate to choose which buy offer it stores. Crates appear around the stand right away.
/// </summary>
internal static class BuyCrateBoard
{
    private const float MaxBoardWidth = 560f;

    // Tool and selection survive redraws of the same stand.
    private static Container? _for;
    private static CrateKind? _tool;
    private static bool _erase;
    private static int _selected = -1;

    public static VisualElement Build(Container container, DisplayCaseBehaviour stand)
    {
        if (_for != container)
        {
            _for = container;
            _tool = CrateKind.All[0];
            _erase = false;
            _selected = -1;
        }
        var root = new VisualElement().Column();
        Draw(root, container, stand);
        return root;
    }

    private static void Draw(VisualElement root, Container container, DisplayCaseBehaviour stand)
    {
        root.Clear();
        var nview = container.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        var zdo = nview.GetZDO();
        var offers = BuyOffers.All(zdo);
        var crates = BuyCrates.All(zdo);
        var grid = new BuyCrates.Grid(stand);
        var max = BuyCrates.Max(container);
        if (_selected >= crates.Count)
            _selected = -1;
        void Redraw() => Draw(root, container, stand);

        void Save(List<CratePlan> next)
        {
            nview.ClaimOwnership();
            BuyCrates.SetAll(zdo, next);
            container.GetComponent<BuyCardShelf>()?.Dirty();
            Redraw();
        }

        var head = new VisualElement().Row();
        head.Add(UkControls.Text("BUY CRATES", 12, UkTheme.TextMuted, wrap: false));
        head.Add(UkControls.Spacer());
        head.Add(UkControls.Text($"{crates.Count} / {max}", 13, crates.Count >= max ? UkTheme.Gold : UkTheme.TextMuted, wrap: false));
        root.Add(head);
        root.Add(UkControls.Text(
            "Pick a crate, then click the floor around the stand. Click a crate to choose which offer it stores. Bought goods go into their crate.",
            13, UkTheme.TextMuted));

        // Palette: the four crate types, then Remove.
        var palette = new VisualElement().Row();
        palette.style.flexWrap = Wrap.Wrap;
        palette.style.marginTop = 8;
        foreach (var kind in CrateKind.All)
        {
            var k = kind;
            var on = !_erase && _tool == k;
            var tile = UkControls.Card(on ? UkTheme.Active : null).Row().Pad(4, 8);
            tile.Border(on ? UkTheme.Accent : UkTheme.LineSoft);
            tile.style.marginRight = 6;
            tile.style.marginBottom = 6;
            tile.style.alignItems = Align.Center;
            tile.pickingMode = PickingMode.Position;
            tile.Add(UkControls.IconTile(k.Icon, 30));
            var label = UkControls.Text(k.Label, 14, on ? UkTheme.Accent : UkTheme.Text, wrap: false);
            label.style.marginLeft = 6;
            tile.Add(label);
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                _tool = k;
                _erase = false;
                Redraw();
            });
            palette.Add(tile);
        }
        var erase = UkControls.MakeButton("Remove", () =>
        {
            _erase = !_erase;
            Redraw();
        }, _erase ? UkButtonKind.Selected : UkButtonKind.Ghost);
        palette.Add(erase);
        root.Add(palette);

        // The floor grid: column 0 = stocker's left, top row = store side.
        var px = Mathf.Floor(Mathf.Clamp(MaxBoardWidth / grid.Cols, 28f, 52f));
        var board = new VisualElement().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius).Border(UkTheme.LineSoft);
        board.style.position = Position.Relative;
        board.style.width = grid.Cols * px;
        board.style.height = grid.Rows * px;
        board.style.alignSelf = Align.Center;
        board.style.marginTop = 4;
        board.pickingMode = PickingMode.Position;

        for (var c = 0; c < grid.Cols; c++)
            for (var r = 0; r < grid.Rows; r++)
                if (!grid.IsStand(c, r))
                    board.Add(Box(c, r, 1, px, Color.clear, UkTheme.LineSoft));

        var standBox = Box(BuyCrates.Ring, BuyCrates.Ring, grid.StandCols, px, UkTheme.Raised, UkTheme.Line, grid.StandRows);
        standBox.style.alignItems = Align.Center;
        standBox.style.justifyContent = Justify.Center;
        standBox.Add(UkControls.Text(global::Localization.instance.Localize(container.m_name), 13, UkTheme.TextMuted, wrap: false));
        board.Add(standBox);

        for (var i = 0; i < crates.Count; i++)
        {
            var plan = crates[i];
            var picked = i == _selected;
            var box = Box(plan.Col, plan.Row, plan.Kind.Cells, px, picked ? UkTheme.Active : UkTheme.Raised, picked ? UkTheme.Accent : UkTheme.Line);
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            var icon = plan.Holds.Length > 0 ? BuyOffers.ItemIcon(plan.Holds) : plan.Kind.Icon;
            var tile = UkControls.IconTile(icon, px - 8);
            if (plan.Holds.Length == 0)
                tile.style.opacity = 0.5f;
            box.Add(tile);
            board.Add(box);
        }

        board.RegisterCallback<ClickEvent>(e =>
        {
            var col = Mathf.FloorToInt(e.localPosition.x / px);
            var row = Mathf.FloorToInt(e.localPosition.y / px);
            if (!grid.Inside(col, row) || grid.IsStand(col, row))
                return;
            var hit = BuyCrates.IndexAt(crates, col, row);
            if (hit >= 0)
            {
                if (_erase)
                {
                    var next = crates.ToList();
                    next.RemoveAt(hit);
                    _selected = -1;
                    Save(next);
                    return;
                }
                _selected = hit;
                Redraw();
                return;
            }
            if (_erase || _tool == null)
                return;
            if (crates.Count >= max)
            {
                Trade.TradeNet.Say($"This stand can lay out {max} crate(s) — one per display slot.");
                return;
            }
            if (!BuyCrates.Fits(grid, crates, _tool, col, row))
            {
                Trade.TradeNet.Say($"No room for the {_tool.Label.ToLowerInvariant()} there.");
                return;
            }
            // New crates take the first offer that has no crate yet.
            var holds = offers.Select(o => o.WantPrefab).FirstOrDefault(p => crates.All(c => c.Holds != p))
                        ?? offers.FirstOrDefault().WantPrefab ?? "";
            var added = crates.ToList();
            added.Add(new CratePlan(_tool, col, row, holds));
            _selected = added.Count - 1;
            Save(added);
        });

        var sides = new VisualElement().Column();
        sides.style.alignSelf = Align.Center;
        sides.style.width = grid.Cols * px;
        if (!stand.m_anySide)
            sides.Add(Caption("▲ store side (door)      ▼ customers"));
        sides.Add(board);
        root.Add(sides);

        // The selected crate: what it stores.
        if (_selected >= 0)
        {
            var plan = crates[_selected];
            var card = UkControls.Card().Column().Pad(12);
            card.style.marginTop = 10;
            var title = new VisualElement().Row();
            title.Add(UkControls.IconTile(plan.Kind.Icon, 32));
            var name = UkControls.Text($"{plan.Kind.Label} — holds", 15, UkTheme.Text, heading: true, wrap: false);
            name.style.marginLeft = 10;
            name.style.flexGrow = 1;
            title.Add(name);
            title.Add(UkControls.MakeButton("Remove crate", () =>
            {
                var next = crates.ToList();
                next.RemoveAt(_selected);
                _selected = -1;
                Save(next);
            }, UkButtonKind.Danger));
            card.Add(title);

            var choices = new VisualElement().Row();
            choices.style.flexWrap = Wrap.Wrap;
            choices.style.marginTop = 8;
            foreach (var prefab in offers.Select(o => o.WantPrefab).Distinct())
            {
                var p = prefab;
                var on = plan.Holds == p;
                var chip = UkControls.Card(on ? UkTheme.Active : null).Row().Pad(4, 8);
                chip.Border(on ? UkTheme.Accent : UkTheme.LineSoft);
                chip.style.marginRight = 6;
                chip.style.marginBottom = 6;
                chip.style.alignItems = Align.Center;
                chip.pickingMode = PickingMode.Position;
                chip.Add(UkControls.IconTile(BuyOffers.ItemIcon(p), 28));
                var label = UkControls.Text(BuyOffers.ItemLabel(p), 14, on ? UkTheme.Accent : UkTheme.Text, wrap: false);
                label.style.marginLeft = 6;
                chip.Add(label);
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    var next = crates.ToList();
                    next[_selected] = plan.WithHolds(p);
                    Save(next);
                });
                choices.Add(chip);
            }
            card.Add(choices);
            var heldNow = plan.Holds.Length > 0 ? BuyOffers.HeldOf(zdo, plan.Holds) : 0;
            card.Add(UkControls.Text(heldNow > 0
                ? $"{heldNow} {BuyOffers.ItemLabel(plan.Holds)} waiting — collect from the Buy tab or press E on the crate."
                : $"Stores up to {plan.Kind.Stacks} stacks.", 13, UkTheme.TextMuted));
            root.Add(card);
        }
    }

    private static VisualElement Box(int col, int row, int cells, float px, Color fill, Color line, int rows = 1)
    {
        var box = new VisualElement { pickingMode = PickingMode.Ignore }.Fill(fill).Border(line).Round(UkTheme.RadiusSmall);
        box.style.position = Position.Absolute;
        box.style.left = col * px + 1;
        box.style.top = row * px + 1;
        box.style.width = cells * px - 2;
        box.style.height = rows * px - 2;
        return box;
    }

    private static Label Caption(string text)
    {
        var label = UkControls.Text(text, 12, UkTheme.TextMuted, wrap: false);
        label.style.marginTop = 6;
        label.style.marginBottom = 4;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        return label;
    }
}
