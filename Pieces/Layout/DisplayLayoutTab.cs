using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Mats;
using DrakesItemShop.Pricing;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pieces.Layout;

/// <summary>
/// Shift+E "Layout" tab for shop display pieces. The jute mat gets a top-down free board (drag, turn, lift, size).
/// Cases and racks get a slot strip you can reorder — same menu, so the layout habit works across the shop suite.
/// </summary>
internal static class DisplayLayoutTab
{
    internal static void Register() =>
        PieceMenuTabs.Register(new PieceMenuTab("drakes.layout", "Layout", ShopState.IsPiece, Build));

    internal static void BuildInto(PieceMenuTabContext ctx) => Build(ctx);

    private static void Build(PieceMenuTabContext ctx)
    {
        var container = ctx.Piece;
        var body = ctx.Body;
        var player = Player.m_localPlayer;
        var manage = player != null && ShopState.CanManage(container, player);

        if (!manage)
        {
            body.Add(UkControls.Text("Only the owner (or someone the ward lets in) can change the layout.", 14, UkTheme.TextMuted));
            return;
        }

        if (container.GetComponent<MatBehaviour>() is { } mat)
        {
            body.Add(UkControls.Text("Drag goods on the mat. Select one to turn, lift or size it.", 14, UkTheme.TextMuted));
            // Only refresh the world mat — do not Rebuild this tab (that reset selection to the first item).
            body.Add(FreeLayoutBoard.Build(container, () => mat.Invalidate()));
            return;
        }

        body.Add(UkControls.Text(
            container.GetComponent<DisplayCaseBehaviour>() != null
                ? "Drag an item onto another slot to swap them. Display row only (top row)."
                : "Drag an item onto another hook to swap them.",
            14, UkTheme.TextMuted));
        body.Add(SlotLayoutBoard.Build(container, ctx.Rebuild));
    }
}
