using System;
using System.Linq;
using BepInEx.Bootstrap;
using DrakeModsLibs.Display;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pricing;

/// <summary>
/// The "Shop" tab in the shared Shift+E menu of display cases and racks: the open switch, a price for each item in the piece, the till
/// and the last sales. A price is a number of coins (buyable) or a short note (shown on the tag, dealt in person).
/// </summary>
internal static class ShopMenuTab
{
    internal static void Register() =>
        PieceMenuTabs.Register(new PieceMenuTab("drakes.shop", "Shop", ShopState.IsPiece, Build));

    internal static void BuildInto(PieceMenuTabContext ctx) => Build(ctx);

    private static void Build(PieceMenuTabContext ctx)
    {
        var container = ctx.Piece;
        var body = ctx.Body;
        // Shop manage rights are ours (builder, or ward-permitted), not LockSmith's guest-list flag — that flag was dimming price
        // fields for owners when LockSmith and the shop disagreed about the ward.
        var player = Player.m_localPlayer;
        var manage = player != null && ShopState.CanManage(container, player);
        if (!ShopState.InWard(container))
        {
            var warn = UkControls.Card(UkTheme.DangerBg).Column().Pad(12);
            warn.style.marginBottom = 14;
            warn.Add(UkControls.Text("Not protected", 16, UkTheme.DangerText, heading: true, wrap: false));
            warn.Add(UkControls.Text("This shop is not inside a ward, so anyone can open the chest and take the goods. Build it inside a ward, or lock it with LockSmith.", 14, UkTheme.TextBody));
            body.Add(warn);
        }

        var open = new UkSwitch("Open for business", "When on, priced items can be bought by anyone, without opening the chest. Unpriced items stay on display.");
        open.SetValue(ShopState.IsOpen(container), notify: false);
        open.Changed = on => ShopState.SetOpen(container, on);
        body.Add(open.Root);
        if (!manage)
            open.Root.SetEnabled(false);

        // The till
        var till = ShopState.Till(container);
        var tillCard = UkControls.Card().Column().Pad(12);
        tillCard.style.marginTop = 14;
        tillCard.Add(UkControls.Text("TILL", 12, UkTheme.TextMuted, wrap: false));
        var tillRow = new VisualElement().Row();
        tillRow.Add(UkControls.Text($"{till} coins", 20, UkTheme.Gold, heading: true, wrap: false));
        tillRow.Add(UkControls.Spacer());
        if (manage && till > 0)
        {
            tillRow.Add(UkControls.MakeButton("Collect", () =>
            {
                if (Player.m_localPlayer == null)
                    return;
                var coins = ShopState.TakeTill(container);
                if (coins > 0 && !ShopState.GiveCoins(Player.m_localPlayer.GetInventory(), coins))
                    Trade.TradeNet.Say("No room for the coins. Make space and collect again.");
                ctx.Rebuild();
            }, UkButtonKind.Primary));
        }
        tillCard.Add(tillRow);
        tillCard.Add(UkControls.Text("Coins from sales collect here. Take them whenever you like.", 13, UkTheme.TextMuted));
        body.Add(tillCard);

        // Prices
        var title = UkControls.Text("PRICES", 12, UkTheme.TextMuted, wrap: false);
        title.style.marginTop = 16;
        title.style.marginBottom = 6;
        body.Add(title);
        var items = container.GetInventory().GetAllItems().OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x).ToList();
        var card = UkControls.Card().Column().Pad(10);
        if (items.Count == 0)
            card.Add(UkControls.Text("Nothing in here yet. Stock the piece, then set prices.", 14, UkTheme.TextMuted));
        foreach (var item in items)
        {
            var row = new VisualElement().Row();
            row.style.marginBottom = 6;
            row.Add(UkControls.IconTile(item.GetIcon(), 36));
            var name = UkControls.Text(ItemDisplayService.GetDisplayNameForUi(item, localize: true), 15, UkTheme.Text, wrap: false);
            name.style.marginLeft = 10;
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            row.Add(name);
            row.Add(UkControls.Text("x" + item.m_stack, 14, UkTheme.TextMuted, wrap: false));

            var priceLabel = PriceLabel(item);
            priceLabel.style.marginLeft = 10;
            priceLabel.style.maxWidth = 160;
            row.Add(priceLabel);

            if (manage)
            {
                var captured = item;
                var set = UkControls.MakeButton("Set", () =>
                    PriceEditWindow.Open(container, captured, () => ctx.Rebuild()), UkButtonKind.Secondary);
                set.style.marginLeft = 8;
                row.Add(set);
            }
            card.Add(row);
        }
        body.Add(card);
        body.Add(UkControls.Text(
            "Set: buyer gets N of this stack per deal. Add payment options with OR — e.g. 200 coins OR 10 Iron + 10 coins. Item payments go into the chest; coins into the till.",
            13, UkTheme.TextMuted));
        if (manage && items.Count > 0)
        {
            var catalogTitle = UkControls.Text("YOUR CATALOG (optional)", 12, UkTheme.TextMuted, wrap: false);
            catalogTitle.style.marginTop = 14;
            catalogTitle.style.marginBottom = 4;
            body.Add(catalogTitle);
            body.Add(UkControls.Text(
                "A private price list on this computer only. Copy prices into it for later, or fill empty prices from it. Shoppers never see or need this file.",
                13, UkTheme.TextMuted));
            var catalog = new VisualElement().Row();
            catalog.style.marginTop = 8;
            catalog.Add(UkControls.MakeButton("Remember in catalog", () =>
            {
                var n = 0;
                foreach (var entry in items)
                {
                    var live = ShopState.Live(container, entry);
                    var encoded = CatalogEncode(live);
                    if (encoded.Length == 0)
                        continue;
                    Catalog.Set(live, encoded);
                    n++;
                }
                Trade.TradeNet.Say(n == 0 ? "No priced items to remember." : $"Remembered {n} price(s) in your catalog.");
            }, UkButtonKind.Secondary));
            var fill = UkControls.MakeButton("Fill blanks from catalog", () =>
            {
                var n = 0;
                foreach (var entry in items)
                {
                    var live = ShopState.Live(container, entry);
                    if (HasAnyPrice(live) || Catalog.Get(live) is not { Length: > 0 } saved)
                        continue;
                    ApplyCatalog(container, live, saved);
                    n++;
                }
                Trade.TradeNet.Say(n == 0 ? "Nothing blank to fill (or catalog has no match)." : $"Filled {n} blank price(s) from your catalog.");
                ctx.Rebuild();
            }, UkButtonKind.Ghost);
            fill.style.marginLeft = 8;
            catalog.Add(fill);
            body.Add(catalog);
        }

        // Last sales
        var sales = ShopState.Sales(container);
        if (sales.Count > 0)
        {
            var salesTitle = UkControls.Text("RECENT SALES", 12, UkTheme.TextMuted, wrap: false);
            salesTitle.style.marginTop = 16;
            salesTitle.style.marginBottom = 6;
            body.Add(salesTitle);
            var salesCard = UkControls.Card().Column().Pad(12);
            foreach (var (buyer, item, quantity, coins, ago) in sales)
            {
                var line = new VisualElement().Row();
                line.Add(UkControls.Text($"{buyer}: {quantity} {item}", 15, UkTheme.Text, wrap: false));
                line.Add(UkControls.Spacer());
                var paid = coins > 0 ? $"{coins} coins, {Ago(ago)}" : $"trade, {Ago(ago)}";
                line.Add(UkControls.Text(paid, 14, UkTheme.TextMuted, wrap: false));
                salesCard.Add(line);
            }
            body.Add(salesCard);
        }
    }

    private static Label PriceLabel(ItemDrop.ItemData item)
    {
        if (PayOptions.Has(item))
            return UkControls.Text(PayOptions.Summary(item), 14, UkTheme.Gold, wrap: false);
        if (ItemPrice.Raw(item).Length > 0)
            return UkControls.Text(ItemPrice.Raw(item), 14, UkTheme.TextMuted, wrap: false);
        return UkControls.Text("no price", 14, UkTheme.TextMuted, wrap: false);
    }

    private static bool HasAnyPrice(ItemDrop.ItemData item) => PayOptions.Has(item) || ItemPrice.Raw(item).Length > 0;

    private static string CatalogEncode(ItemDrop.ItemData item)
    {
        var raw = PayOptions.Raw(item);
        return raw.Length > 0 ? "pay|" + raw : ItemPrice.Raw(item);
    }

    private static void ApplyCatalog(Container container, ItemDrop.ItemData item, string saved)
    {
        if (saved.StartsWith("pay|", StringComparison.OrdinalIgnoreCase))
        {
            var chunks = saved.Substring("pay|".Length).Split('|');
            var opts = chunks.Select(PayOption.Decode).Where(o => o != null).Cast<PayOption>().ToList();
            PayOptions.Set(item, opts);
            ShopState.NotifyChanged(container);
            return;
        }
        if (saved.StartsWith("trade|", StringComparison.OrdinalIgnoreCase))
        {
            if (PayOption.Decode(saved.Substring("trade|".Length)) is { } opt)
                PayOptions.Set(item, new[] { opt });
            ShopState.NotifyChanged(container);
            return;
        }
        PayOptions.Clear(item);
        ShopState.SetPrice(container, item, saved);
    }

    private static string Ago(TimeSpan span) =>
        span.TotalMinutes < 1 ? "just now"
        : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
        : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
        : $"{(int)span.TotalDays} d ago";
}

/// <summary>Buying: E on a priced item in an open shop opens the buy window for anyone who can't open the chest themselves.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class ShopInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (alt || !ShopState.IsPiece(__instance) || character is not Player player || !ShopState.IsOpen(__instance))
            return true;
        if (DisplayedItemTag.UnderCrosshair(__instance.transform) is not { } shown)
            return true;
        var item = ShopState.Live(__instance, shown);
        if (!PayOptions.Has(item))
            return true;
        // Coins-for-coins only: skip (need an item payment option on coin stacks).
        if (ShopState.IsCoinsStack(item) && PayOptions.All(item).All(o => o.Coins > 0 && o.Items.Count == 0))
            return true;
        // The people who run the shop (the builder, the ward) keep opening the chest; everyone else buys.
        if (ShopState.CanManage(__instance, player))
            return true;

        __result = true;
        if (!hold && !BuyWindow.IsOpen)
            BuyWindow.Open(__instance, item);
        return false;
    }
}

/// <summary>Without LockSmith (or when it has nothing for this player) Shift+E on a shop piece opens the Shop settings by themselves.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class ShopSettingsInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (!alt || hold || !ShopState.IsPiece(__instance) || character is not Player player
            || Mail.MailboxSettingsWindow.LockSmithShowsMenu(__instance))
            return true;
        __result = true;
        if (ShopState.CanManage(__instance, player))
            ShopSettingsWindow.Open(__instance, player);
        else
            Trade.TradeNet.Say("Only the owner can change this shop.");
        return false;
    }
}

internal static class ShopSettingsWindow
{
    private static UkWindow? _window;
    private static bool _layoutTab;

    internal static void Open(Container container, Player player)
    {
        _window?.CloseSilent();
        _layoutTab = false;
        var window = new UkWindow("drakes_shop_settings", 640);
        _window = window;
        window.SetHeader(global::Localization.instance.Localize(container.m_name), "Shop");
        Draw(window, container, player);
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Close", () => window.Close(), UkButtonKind.Secondary));
        window.ShowFooter(true);
        window.Open();
    }

    private static void Draw(UkWindow window, Container container, Player player)
    {
        window.Body.Clear();
        var manage = ShopState.CanManage(container, player);
        var tabs = new VisualElement().Row();
        tabs.style.marginBottom = 12;
        tabs.Add(UkControls.MakeButton("Shop", () => { _layoutTab = false; Draw(window, container, player); },
            _layoutTab ? UkButtonKind.Ghost : UkButtonKind.Selected));
        var layout = UkControls.MakeButton("Layout", () => { _layoutTab = true; Draw(window, container, player); },
            _layoutTab ? UkButtonKind.Selected : UkButtonKind.Ghost);
        layout.style.marginLeft = 8;
        tabs.Add(layout);
        window.Body.Add(tabs);

        var body = new VisualElement().Column();
        var ctx = new PieceMenuTabContext(container, body, manage, () => Draw(window, container, player));
        if (_layoutTab)
            Pieces.Layout.DisplayLayoutTab.BuildInto(ctx);
        else
            ShopMenuTab.BuildInto(ctx);
        window.Body.Add(body);
    }
}
