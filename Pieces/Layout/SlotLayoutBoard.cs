using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Pricing;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pieces.Layout;

/// <summary>
/// Slot strip for display cases and racks: drag an item onto another slot to swap. Same Shift+E Layout tab as the free mat board.
/// </summary>
internal static class SlotLayoutBoard
{
    private const float Token = 48f;

    public static VisualElement Build(Container container, Action onChanged)
    {
        var root = new VisualElement().Column();
        root.style.marginTop = 10;

        var inventory = container.GetInventory();
        var width = Mathf.Max(1, inventory.GetWidth());
        // Display row is y=0 for cases; racks use the full grid in order.
        var rack = container.GetComponent<ItemRackBehaviour>() != null;
        var slots = new ItemDrop.ItemData?[width];
        foreach (var item in inventory.GetAllItems())
        {
            if (!rack && item.m_gridPos.y != 0)
                continue;
            if (item.m_gridPos.x < 0 || item.m_gridPos.x >= width)
                continue;
            slots[item.m_gridPos.x] = item;
        }

        if (slots.All(s => s == null))
        {
            root.Add(UkControls.Text("Nothing on display yet. Stock the piece, then arrange here.", 14, UkTheme.TextMuted));
            return root;
        }

        var strip = new VisualElement().Row();
        strip.style.flexWrap = Wrap.Wrap;
        strip.style.justifyContent = Justify.Center;

        ItemDrop.ItemData? dragging = null;

        for (var x = 0; x < width; x++)
        {
            var slotIndex = x;
            var cell = UkControls.Card().Column();
            cell.style.width = Token + 16;
            cell.style.height = Token + 28;
            cell.style.marginRight = 6;
            cell.style.marginBottom = 6;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.pickingMode = PickingMode.Position;

            var item = slots[x];
            if (item != null)
            {
                var token = UkControls.IconTile(item.GetIcon(), Token);
                token.pickingMode = PickingMode.Ignore;
                cell.Add(token);
                var name = UkControls.Text(ShortName(item), 11, UkTheme.TextMuted, wrap: false);
                name.style.marginTop = 2;
                cell.Add(name);
            }
            else
            {
                cell.Add(UkControls.Text("empty", 12, UkTheme.TextMuted, wrap: false));
            }

            cell.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || slots[slotIndex] == null)
                    return;
                dragging = slots[slotIndex];
                cell.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            cell.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!cell.HasPointerCapture(e.pointerId))
                    return;
                cell.ReleasePointer(e.pointerId);
                if (dragging == null)
                    return;
                var from = dragging;
                dragging = null;
                var toX = slotIndex;
                if (from.m_gridPos.x == toX)
                    return;
                Swap(container, from, toX, rack ? from.m_gridPos.y : 0);
                onChanged();
            });

            strip.Add(cell);
        }

        root.Add(strip);
        root.Add(UkControls.Text("Drop onto another slot to swap. Backstock (lower rows) stays put.", 13, UkTheme.TextMuted));
        return root;
    }

    private static void Swap(Container container, ItemDrop.ItemData from, int toX, int toY)
    {
        var inventory = container.GetInventory();
        var live = ShopState.Live(container, from);
        var other = inventory.GetItemAt(toX, toY);
        var fromPos = live.m_gridPos;
        live.m_gridPos = new Vector2i(toX, toY);
        if (other != null)
            other.m_gridPos = fromPos;
        ShopState.NotifyChanged(container);
    }

    private static string ShortName(ItemDrop.ItemData item)
    {
        var name = ItemDisplayService.GetDisplayNameForUi(item, localize: true) ?? "";
        return name.Length <= 10 ? name : name.Substring(0, 9) + "…";
    }
}
