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
        PieceMenuTabs.Register(new PieceMenuTab("drakes.layout", "Layout", ShopState.IsPiece, Build, group: "shop", isRoot: false));

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

        var stand = container.GetComponent<DisplayCaseBehaviour>();
        body.Add(UkControls.Text("DISPLAY SLOTS", 12, UkTheme.TextMuted, wrap: false));
        body.Add(UkControls.Text(
            stand != null
                ? "Drag an item onto another slot to swap them. Display row only (top row)."
                : "Drag an item onto another hook to swap them.",
            14, UkTheme.TextMuted));
        body.Add(SlotLayoutBoard.Build(container, ctx.Rebuild));

        // Buy crates: only once the stand is buying and has something to buy.
        if (stand == null || container.GetComponent<DisplayCard.BuyCardShelf>() == null
            || container.GetComponent<ZNetView>() is not { } nview || !nview.IsValid())
            return;
        var zdo = nview.GetZDO();
        if (!DisplayCard.BuyOffers.Enabled(zdo))
            return;
        var divider = UkControls.Divider();
        divider.style.marginTop = 14;
        divider.style.marginBottom = 12;
        body.Add(divider);
        if (DisplayCard.BuyOffers.All(zdo).Count == 0)
        {
            body.Add(UkControls.Text("BUY CRATES", 12, UkTheme.TextMuted, wrap: false));
            body.Add(UkControls.Text("Add a buy offer in the Buy tab first, then lay out crates for it here.", 14, UkTheme.TextMuted));
            return;
        }
        body.Add(BuyCrateBoard.Build(container, stand));
    }
}
