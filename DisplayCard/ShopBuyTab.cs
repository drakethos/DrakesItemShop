using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using DrakesItemShop.Pricing;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.DisplayCard;

/// <summary>
/// Shift+E → Buy tab on display cases: configure buy offers + budget.
/// Cards appear on the case automatically from those offers.
/// </summary>
internal static class ShopBuyTab
{
    private static UkWindow? _edit;

    internal static void Register() =>
        PieceMenuTabs.Register(new PieceMenuTab("drakes.buy", "Buy", AppliesTo, Build, group: "shop", isRoot: false));

    /// <summary>Stands with Buying switched on (Shop tab) — off, the tab stays out of the way.</summary>
    internal static bool AppliesTo(Container c) =>
        c.GetComponent<BuyCardShelf>() != null && c.GetComponent<ZNetView>() is { } nview && nview.IsValid()
        && BuyOffers.Enabled(nview.GetZDO());

    internal static void BuildInto(PieceMenuTabContext ctx) => Build(ctx);

    private static void Build(PieceMenuTabContext ctx)
    {
        var container = ctx.Piece;
        var nview = container.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        var zdo = nview.GetZDO();
        var body = ctx.Body;
        var manage = ctx.CanManage;
        var offers = BuyOffers.All(zdo);

        body.Add(UkControls.Text("BUYING", 12, UkTheme.TextMuted, wrap: false));
        body.Add(UkControls.Text(
            "What this stand will buy. Cards appear on the case — you don't place them.",
            13, UkTheme.TextMuted, wrap: true));

        if (offers.Count == 0)
            body.Add(UkControls.Text("No buy offers yet.", 15, UkTheme.TextMuted, wrap: false));

        for (var i = 0; i < offers.Count; i++)
        {
            var idx = i;
            var offer = offers[i];
            var row = UkControls.Card().Row().Pad(8);
            row.style.marginTop = 8;
            row.Add(UkControls.IconTile(BuyOffers.ItemIcon(offer.WantPrefab), 40));
            var label = UkControls.Text(offer.Summary, 15, UkTheme.Text, wrap: true);
            label.style.marginLeft = 10;
            label.style.flexGrow = 1;
            row.Add(label);
            if (manage)
            {
                row.Add(UkControls.MakeButton("Edit", () => OpenEdit(container, idx, offer, ctx.Rebuild), UkButtonKind.Secondary));
                row.Add(UkControls.MakeButton("×", () =>
                {
                    if (!Claim(nview))
                        return;
                    BuyOffers.RemoveAt(zdo, idx);
                    DirtyShelf(container);
                    ctx.Rebuild();
                }, UkButtonKind.Ghost));
            }
            body.Add(row);
        }

        if (manage)
        {
            var add = UkControls.MakeButton("Add buy offer…", () => OpenEdit(container, -1, default, ctx.Rebuild), UkButtonKind.Primary);
            add.style.marginTop = 12;
            body.Add(add);
        }

        var budget = BuyOffers.Budget(zdo);
        var tillCard = UkControls.Card().Column().Pad(12);
        tillCard.style.marginTop = 16;
        tillCard.Add(UkControls.Text("BUY BUDGET", 12, UkTheme.TextMuted, wrap: false));
        var tillRow = new VisualElement().Row();
        tillRow.Add(UkControls.Text($"{budget} coins", 20, UkTheme.Gold, heading: true, wrap: false));
        tillRow.Add(UkControls.Spacer());
        if (manage)
        {
            tillRow.Add(UkControls.MakeButton("Add 50", () =>
            {
                var player = Player.m_localPlayer;
                if (player == null || !Claim(nview))
                    return;
                if (!ShopState.TakeCoins(player.GetInventory(), 50))
                {
                    Trade.TradeNet.Say("Need 50 coins in your inventory.");
                    return;
                }
                BuyOffers.AddBudget(zdo, 50);
                DirtyShelf(container);
                ctx.Rebuild();
            }, UkButtonKind.Secondary));
            if (budget > 0)
            {
                tillRow.Add(UkControls.MakeButton("Collect", () =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null || !Claim(nview))
                        return;
                    var n = BuyOffers.TakeBudget(zdo);
                    if (n > 0 && !ShopState.GiveCoins(player.GetInventory(), n))
                        Trade.TradeNet.Say("No room for the coins. Make space and collect again.");
                    DirtyShelf(container);
                    ctx.Rebuild();
                }, UkButtonKind.Secondary));
            }
        }
        tillCard.Add(tillRow);
        tillCard.Add(UkControls.Text(
            "Coins set aside to pay sellers. Offers with \"Till tops up\" also spend sales coins once this runs out.",
            13, UkTheme.TextMuted));
        body.Add(tillCard);

        // Goods bought into the layout's crates wait here until collected.
        var held = BuyOffers.Held(zdo);
        if (held.Count > 0)
        {
            var heldCard = UkControls.Card().Column().Pad(12);
            heldCard.style.marginTop = 16;
            heldCard.Add(UkControls.Text("IN BUY CRATES", 12, UkTheme.TextMuted, wrap: false));
            foreach (var kv in held)
            {
                var prefab = kv.Key;
                var row = new VisualElement().Row();
                row.style.marginTop = 6;
                row.Add(UkControls.IconTile(BuyOffers.ItemIcon(prefab), 32));
                var label = UkControls.Text($"{kv.Value} {BuyOffers.ItemLabel(prefab)}", 15, UkTheme.Text, wrap: false);
                label.style.marginLeft = 10;
                label.style.flexGrow = 1;
                row.Add(label);
                if (manage)
                {
                    row.Add(UkControls.MakeButton("Collect", () =>
                    {
                        if (Player.m_localPlayer is not { } player)
                            return;
                        var moved = ShopSell.Collect(container, prefab, player);
                        if (moved == 0)
                            Trade.TradeNet.Say("No room in your inventory.");
                        ctx.Rebuild();
                    }, UkButtonKind.Secondary));
                }
                heldCard.Add(row);
            }
            body.Add(heldCard);
        }

        if (manage && offers.Count > 0 && !BuyCrates.All(zdo).Any())
            body.Add(UkControls.Text("Tip: Layout tab → Buy crates sets out crates for what you buy, instead of filling the stand.", 13, UkTheme.TextMuted));
    }

    private static bool Claim(ZNetView nview)
    {
        if (!nview.IsValid())
            return false;
        nview.ClaimOwnership();
        return true;
    }

    private static void OpenEdit(Container container, int index, BuyOfferEntry existing, System.Action rebuild)
    {
        var nview = container.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid() || !Claim(nview))
        {
            Trade.TradeNet.Say("Can't edit buy offers right now.");
            return;
        }
        var zdo = nview.GetZDO();

        _edit?.CloseSilent();
        var window = new UkWindow("drakes_shop_buy_edit", 520);
        _edit = window;
        window.SetHeader(index < 0 ? "Add buy offer" : "Edit buy offer", "Player sells → you pay coins");

        IconCatalogEntry? picked = null;
        if (existing.IsValid)
        {
            picked = TradePrice.Search(existing.WantPrefab, 1).FirstOrDefault(e =>
                string.Equals(e.PrefabName, existing.WantPrefab, System.StringComparison.OrdinalIgnoreCase));
        }

        TextField StyleField(TextField field, float width)
        {
            field.style.width = width;
            if (field.Q<VisualElement>("unity-text-input") is { } input)
            {
                input.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 10);
                input.style.color = UkTheme.Text;
                input.style.fontSize = 15;
                input.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            return field;
        }

        string LocalName(IconCatalogEntry e) =>
            global::Localization.instance != null
                ? global::Localization.instance.Localize(e.NameToken)
                : e.PrefabName;

        var body = new VisualElement().Column();
        var pickedRow = new VisualElement().Row();
        pickedRow.style.marginBottom = 8;
        var amtField = StyleField(new TextField
        {
            value = existing.IsValid ? existing.WantAmount.ToString() : "1",
            maxLength = 4,
        }, 64);
        var payField = StyleField(new TextField
        {
            value = existing.PayCoins > 0 ? existing.PayCoins.ToString() : "",
            maxLength = 6,
        }, 80);
        var capField = StyleField(new TextField
        {
            value = existing.IsValid && existing.Cap > 0 ? existing.Cap.ToString() : "",
            maxLength = 6,
        }, 80);
        var preview = UkControls.Text("", 15, UkTheme.Gold, wrap: true);

        void RedrawPicked()
        {
            pickedRow.Clear();
            if (picked == null)
            {
                pickedRow.Add(UkControls.Text("No item selected", 14, UkTheme.TextMuted, wrap: false));
                return;
            }
            pickedRow.Add(UkControls.IconTile(picked.Sprite, 40));
            var t = UkControls.Text(LocalName(picked), 16, UkTheme.Text, wrap: false);
            t.style.marginLeft = 10;
            pickedRow.Add(t);
        }

        void UpdatePreview()
        {
            if (!int.TryParse(amtField.value.Trim(), out var amt) || amt <= 0)
                amt = 1;
            int.TryParse(payField.value.Trim(), out var pay);
            int.TryParse(capField.value.Trim(), out var cap);
            if (picked == null || pay <= 0)
            {
                preview.text = "Deal: (pick an item and coin pay)";
                return;
            }
            var get = amt == 1 ? LocalName(picked) : $"{amt} {LocalName(picked)}";
            var capLine = cap > 0 ? $"  ·  stop after {cap} total" : "  ·  no total cap";
            preview.text = $"Deal: player gives {get}  →  you pay {pay} coins{capLine}";
        }

        body.Add(UkControls.Text("WE WANT", 12, UkTheme.TextMuted, wrap: false));
        body.Add(pickedRow);
        RedrawPicked();

        var amtRow = new VisualElement().Row();
        amtRow.style.marginTop = 6;
        amtRow.Add(UkControls.Text("Amount per deal", 14, UkTheme.TextMuted, wrap: false));
        amtField.style.marginLeft = 10;
        amtRow.Add(amtField);
        body.Add(amtRow);
        amtField.RegisterValueChangedCallback(_ => UpdatePreview());

        var search = new TextField { value = "", maxLength = 40 };
        search.style.marginTop = 10;
        if (search.Q<VisualElement>("unity-text-input") is { } sin)
        {
            sin.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 8);
            sin.style.color = UkTheme.Text;
            sin.style.fontSize = 14;
        }
        body.Add(UkControls.Text("Search items", 12, UkTheme.TextMuted, wrap: false));
        body.Add(search);
        var results = new VisualElement().Column();
        results.style.marginTop = 4;
        body.Add(results);

        void FillResults(string q)
        {
            results.Clear();
            foreach (var hit in TradePrice.Search(q, 8))
            {
                var entry = hit;
                var row = new VisualElement().Row();
                row.style.marginBottom = 2;
                row.pickingMode = PickingMode.Position;
                row.Add(UkControls.IconTile(entry.Sprite, 24));
                var t = UkControls.Text(LocalName(entry), 13, UkTheme.Text, wrap: false);
                t.style.marginLeft = 6;
                t.style.flexGrow = 1;
                row.Add(t);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    picked = entry;
                    RedrawPicked();
                    UpdatePreview();
                });
                results.Add(row);
            }
        }
        search.RegisterValueChangedCallback(e => FillResults(e.newValue));
        FillResults("");

        body.Add(UkControls.Text("WE PAY (coins)", 12, UkTheme.TextMuted, wrap: false));
        var payRow = new VisualElement().Row();
        payRow.style.marginTop = 6;
        payRow.Add(UkControls.Text("Coins", 14, UkTheme.TextMuted, wrap: false));
        payField.style.marginLeft = 10;
        payRow.Add(payField);
        body.Add(payRow);
        payField.RegisterValueChangedCallback(_ => UpdatePreview());

        body.Add(UkControls.Text("BUY CAP (total units)", 12, UkTheme.TextMuted, wrap: false));
        var capRow = new VisualElement().Row();
        capRow.style.marginTop = 6;
        capRow.Add(UkControls.Text("Max (0 = no limit)", 14, UkTheme.TextMuted, wrap: false));
        capField.style.marginLeft = 10;
        capRow.Add(capField);
        body.Add(capRow);
        body.Add(UkControls.Text(
            "Example: amount 1, cap 200 → buy up to 200 Wood, then the card says the shop isn't taking sales.",
            13, UkTheme.TextMuted, wrap: true));
        capField.RegisterValueChangedCallback(_ => UpdatePreview());

        var topUp = new UkSwitch("Till tops up the budget",
            "Off: only the buy budget pays, you keep it filled. On: when the budget runs out, coins from sales (the till) pay too.");
        var topUpOn = existing.IsValid && existing.TillTopUp;
        topUp.SetValue(topUpOn, notify: false);
        topUp.Changed = on => topUpOn = on;
        topUp.Root.style.marginTop = 12;
        body.Add(topUp.Root);

        preview.style.marginTop = 14;
        body.Add(preview);
        UpdatePreview();

        window.Body.Add(body);
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Cancel", () => window.Close(), UkButtonKind.Secondary));
        window.Footer.Add(UkControls.MakeButton("Save", () =>
        {
            if (!Claim(nview))
                return;
            if (picked == null)
            {
                Trade.TradeNet.Say("Pick an item this stand will buy.");
                return;
            }
            if (!int.TryParse(amtField.value.Trim(), out var amt) || amt <= 0)
            {
                Trade.TradeNet.Say("Amount must be at least 1.");
                return;
            }
            if (!int.TryParse(payField.value.Trim(), out var pay) || pay <= 0)
            {
                Trade.TradeNet.Say("Coin pay must be at least 1.");
                return;
            }
            int.TryParse(capField.value.Trim(), out var cap);
            if (cap < 0)
                cap = 0;
            // Keep progress when editing an existing offer (same item); reset if the item changed.
            var filled = 0;
            if (index >= 0 && existing.IsValid
                && string.Equals(existing.WantPrefab, picked.PrefabName, System.StringComparison.OrdinalIgnoreCase))
                filled = existing.Filled;
            var entry = new BuyOfferEntry(picked.PrefabName, amt, pay, cap, filled, topUpOn);
            if (index < 0)
                BuyOffers.Add(zdo, entry);
            else
                BuyOffers.ReplaceAt(zdo, index, entry);
            DirtyShelf(container);
            rebuild();
            window.Close();
            Trade.TradeNet.Say("Buy offer saved — card on the case.");
        }, UkButtonKind.Primary));
        window.ShowFooter(true);
        window.Open();
    }

    private static void DirtyShelf(Container container)
    {
        var shelf = container.GetComponent<BuyCardShelf>()
                     ?? container.GetComponentInChildren<BuyCardShelf>(true)
                     ?? container.GetComponentInParent<BuyCardShelf>();
        if (shelf == null)
        {
            ItemShopPlugin.LogSource?.LogWarning(
                $"[DisplayCard] No BuyCardShelf on {container.name} — buy cards won't spawn.");
            return;
        }
        shelf.Dirty();
    }
}
