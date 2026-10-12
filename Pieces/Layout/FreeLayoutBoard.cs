using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Mats;
using DrakesItemShop.Pricing;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pieces.Layout;

/// <summary>
/// Top-down free layout board: a square for the mat surface, item icons you drag, and turn / lift / size for the selected one.
/// Pose is stored on the stack (<see cref="MatPose"/>). Edits update the world mat in place — they do not rebuild this panel
/// (rebuilding was resetting selection to the first item).
/// </summary>
internal static class FreeLayoutBoard
{
    private const float BoardPx = 340f;
    private const float Token = 44f;

    public static VisualElement Build(Container container, Action flushWorld)
    {
        var root = new VisualElement().Column();
        root.style.marginTop = 10;

        var items = MatBehaviour.OrderedItems(container).ToList();
        if (items.Count == 0)
        {
            root.Add(UkControls.Text("Nothing on the mat yet. Stock it first, then arrange here.", 14, UkTheme.TextMuted));
            return root;
        }

        var half = container.GetComponent<MatBehaviour>() is { } matBeh ? matBeh.Half : MatBehaviour.DefaultHalf;

        // Stable id per token: grid slot (ItemData references die when the chest reloads).
        static int KeyOf(Vector2i s) => s.x + s.y * 64;
        var slots = items.Select(i => i.m_gridPos).ToList();
        var selected = KeyOf(slots[0]);
        var tokens = new Dictionary<int, VisualElement>();

        var board = UkControls.Card().Column();
        board.style.width = BoardPx;
        board.style.height = BoardPx;
        board.style.alignSelf = Align.Center;
        board.style.position = Position.Relative;
        board.style.overflow = Overflow.Hidden;
        board.style.backgroundColor = new Color(0.28f, 0.22f, 0.14f, 1f);
        board.Border(new Color(0.12f, 0.09f, 0.06f, 1f), 3);

        var fringe = UkControls.Text("top · away from you", 11, UkTheme.TextMuted, wrap: false);
        fringe.style.position = Position.Absolute;
        fringe.style.top = 4;
        fringe.style.left = 8;
        fringe.pickingMode = PickingMode.Ignore;
        board.Add(fringe);

        Label? title = null;
        UkSlider? yaw = null, lift = null, scale = null;

        ItemDrop.ItemData? LiveAt(int key)
        {
            var x = key % 64;
            var y = key / 64;
            return container.GetInventory().GetItemAt(x, y);
        }

        int IndexOf(int key)
        {
            for (var i = 0; i < slots.Count; i++)
                if (KeyOf(slots[i]) == key)
                    return i;
            return 0;
        }

        MatPose PoseOf(ItemDrop.ItemData item, int key) =>
            MatPose.TryRead(item, out var stored) ? stored : MatPose.Default(IndexOf(key));

        void PlaceToken(int key, VisualElement token, MatPose pose)
        {
            var (px, py) = ToPanel(pose.X, pose.Z, half);
            token.style.left = px - Token / 2f;
            token.style.top = py - Token / 2f;
            token.style.rotate = new Rotate(Angle.Degrees(pose.Yaw));
            var selectedLook = key == selected;
            token.style.borderTopWidth = token.style.borderBottomWidth = token.style.borderLeftWidth = token.style.borderRightWidth = selectedLook ? 2 : 1;
            token.style.borderTopColor = token.style.borderBottomColor = token.style.borderLeftColor = token.style.borderRightColor =
                selectedLook ? UkTheme.Accent : UkTheme.Line;
        }

        void RefreshTokenBorders()
        {
            foreach (var pair in tokens)
            {
                var item = LiveAt(pair.Key);
                if (item == null)
                    continue;
                PlaceToken(pair.Key, pair.Value, PoseOf(item, pair.Key));
            }
        }

        void Persist(ItemDrop.ItemData item, MatPose pose)
        {
            pose.Write(item);
            ShopState.NotifyChanged(container);
            flushWorld();
        }

        void WriteSelected()
        {
            if (yaw == null || lift == null || scale == null)
                return;
            var live = LiveAt(selected);
            if (live == null)
                return;
            var pose = PoseOf(live, selected);
            pose.Yaw = yaw.Value;
            pose.Lift = lift.Value;
            pose.Scale = scale.Value;
            Persist(live, pose);
            if (tokens.TryGetValue(selected, out var token))
                PlaceToken(selected, token, pose);
        }

        void Select(int key)
        {
            selected = key;
            var live = LiveAt(key);
            if (live == null)
                return;
            var pose = PoseOf(live, key);
            if (title != null)
                title.text = ItemDisplayService.GetDisplayNameForUi(live, localize: true);
            // WithoutNotify so Select does not fire WriteSelected → Persist mid-select.
            yaw?.SetValueWithoutNotify(pose.Yaw);
            lift?.SetValueWithoutNotify(pose.Lift);
            scale?.SetValueWithoutNotify(pose.Scale);
            RefreshTokenBorders();
        }

        for (var i = 0; i < items.Count; i++)
        {
            var key = KeyOf(items[i].m_gridPos);
            var token = UkControls.IconTile(items[i].GetIcon(), Token);
            token.pickingMode = PickingMode.Position;
            token.style.position = Position.Absolute;
            token.style.borderTopLeftRadius = token.style.borderTopRightRadius =
                token.style.borderBottomLeftRadius = token.style.borderBottomRightRadius = 6;
            tokens[key] = token;
            PlaceToken(key, token, PoseOf(items[i], key));

            var captured = key;
            var dragging = false;
            token.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0)
                    return;
                Select(captured);
                dragging = true;
                token.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            token.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || !token.HasPointerCapture(e.pointerId))
                    return;
                var live = LiveAt(captured);
                if (live == null)
                    return;
                var local = board.WorldToLocal(e.position);
                var (x, z) = FromPanel(local.x, local.y, half);
                var pose = PoseOf(live, captured);
                pose.X = x;
                pose.Z = z;
                pose.Write(live);
                PlaceToken(captured, token, pose);
            });
            token.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!token.HasPointerCapture(e.pointerId))
                    return;
                token.ReleasePointer(e.pointerId);
                dragging = false;
                if (LiveAt(captured) == null)
                    return;
                // One save at the end of the drag (not on every move).
                ShopState.NotifyChanged(container);
                flushWorld();
            });
            board.Add(token);
        }

        root.Add(board);

        var controls = UkControls.Card().Column().Pad(12);
        controls.style.marginTop = 12;
        title = UkControls.Text("", 16, UkTheme.Text, heading: true, wrap: false);
        controls.Add(title);

        yaw = new UkSlider("Turn", 0f, 360f, 0f, v => $"{v:0}°");
        yaw.Changed = _ => WriteSelected();
        yaw.Root.style.marginTop = 8;
        controls.Add(yaw.Root);

        lift = new UkSlider("Lift", 0f, 0.3f, 0f, v => $"{v:0.00} m");
        lift.Changed = _ => WriteSelected();
        lift.Root.style.marginTop = 8;
        controls.Add(lift.Root);

        scale = new UkSlider("Size", 0.4f, 1.6f, 1f, v => $"{v:0.00}×");
        scale.Changed = _ => WriteSelected();
        scale.Root.style.marginTop = 8;
        controls.Add(scale.Root);

        var row = new VisualElement().Row();
        row.style.marginTop = 10;
        row.Add(UkControls.MakeButton("Reset spot", () =>
        {
            var live = LiveAt(selected);
            if (live == null)
                return;
            MatPose.Clear(live);
            ShopState.NotifyChanged(container);
            flushWorld();
            Select(selected);
        }, UkButtonKind.Secondary));
        controls.Add(row);
        root.Add(controls);

        Select(KeyOf(slots[0]));
        return root;
    }

    private static (float px, float py) ToPanel(float x, float z, float half)
    {
        var px = (x / half * 0.5f + 0.5f) * BoardPx;
        var py = (0.5f - z / half * 0.5f) * BoardPx;
        return (px, py);
    }

    private static (float x, float z) FromPanel(float px, float py, float half)
    {
        var x = Mathf.Clamp((px / BoardPx - 0.5f) * 2f * half, -half, half);
        var z = Mathf.Clamp((0.5f - py / BoardPx) * 2f * half, -half, half);
        return (x, z);
    }
}
