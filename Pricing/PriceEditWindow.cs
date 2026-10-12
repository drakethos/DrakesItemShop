using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Pricing;

/// <summary>
/// Set price: buyer gets N of this stack per deal, and may pay by any of several OR options
/// (e.g. 200 coins OR 10 Iron + 10 coins).
/// </summary>
internal static class PriceEditWindow
{
    private static UkWindow? _window;

    internal static void Open(Container container, ItemDrop.ItemData item, System.Action? onSaved = null)
    {
        _window?.CloseSilent();
        var live = ShopState.Live(container, item);
        var goodsName = ItemDisplayService.GetDisplayNameForUi(live, localize: true);
        var window = new UkWindow("drakes_shop_price_edit", 560);
        _window = window;
        window.SetHeader(goodsName, "Set price");

        var options = PayOptions.All(live).Select(CloneOption).ToList();
        if (options.Count == 0)
            options.Add(new PayOption());

        var lotText = ItemLot.Of(live).ToString();
        var body = new VisualElement().Column();
        Label? dealPreview = null;
        VisualElement? optionsHost = null;

        TextField StyleField(TextField field, float width)
        {
            field.style.width = width;
            field.style.marginLeft = field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            if (field.Q<VisualElement>("unity-text-input") is { } input)
            {
                input.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 10);
                input.style.color = UkTheme.Text;
                input.style.fontSize = 15;
                input.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            return field;
        }

        var lotField = StyleField(new TextField { value = lotText, maxLength = 4 }, 64);

        void UpdatePreview()
        {
            if (dealPreview == null)
                return;
            if (!int.TryParse(lotField.value.Trim(), out var lot) || lot <= 0)
                lot = 1;
            var get = lot == 1 ? $"1 {goodsName}" : $"{lot} {goodsName}";
            var valid = options.Where(o => !o.IsEmpty).ToList();
            dealPreview.text = valid.Count == 0
                ? $"Deal: (add a payment) → {get}"
                : $"Deal: {string.Join("  OR  ", valid.Select(o => o.Label))} → {get}";
        }

        // Buyer gets
        var getCard = UkControls.Card().Column().Pad(10);
        getCard.style.marginBottom = 12;
        getCard.Add(UkControls.Text("BUYER GETS (per deal)", 12, UkTheme.TextMuted, wrap: false));
        var getRow = new VisualElement().Row();
        getRow.style.marginTop = 6;
        getRow.Add(UkControls.Text("Amount", 14, UkTheme.TextMuted, wrap: false));
        lotField.style.marginLeft = 10;
        getRow.Add(lotField);
        var getName = UkControls.Text(goodsName, 15, UkTheme.Text, wrap: false);
        getName.style.marginLeft = 10;
        getName.style.flexGrow = 1;
        getRow.Add(getName);
        getCard.Add(getRow);
        body.Add(getCard);
        lotField.RegisterValueChangedCallback(_ => UpdatePreview());

        body.Add(UkControls.Text("PAYMENT OPTIONS (pick any one)", 12, UkTheme.TextMuted, wrap: false));
        body.Add(UkControls.Text(
            "Each option is one way to pay. Add another for OR — e.g. 200 coins OR 10 Iron + 10 coins.",
            13, UkTheme.TextMuted, wrap: true));

        optionsHost = new VisualElement().Column();
        optionsHost.style.marginTop = 8;
        body.Add(optionsHost);

        void RedrawOptions()
        {
            optionsHost!.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                var idx = i;
                var opt = options[i];
                if (i > 0)
                {
                    var orLabel = UkControls.Text("— OR —", 13, UkTheme.Gold, wrap: false);
                    orLabel.style.marginTop = 8;
                    orLabel.style.marginBottom = 4;
                    orLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    optionsHost.Add(orLabel);
                }

                var card = UkControls.Card().Column().Pad(10);
                var head = new VisualElement().Row();
                head.Add(UkControls.Text($"Option {idx + 1}", 14, UkTheme.Text, heading: true, wrap: false));
                head.Add(UkControls.Spacer());
                if (options.Count > 1)
                {
                    head.Add(UkControls.MakeButton("Remove", () =>
                    {
                        options.RemoveAt(idx);
                        RedrawOptions();
                    }, UkButtonKind.Ghost));
                }
                card.Add(head);

                var coinRow = new VisualElement().Row();
                coinRow.style.marginTop = 8;
                coinRow.Add(UkControls.Text("Coins", 14, UkTheme.TextMuted, wrap: false));
                var coinField = StyleField(new TextField
                {
                    value = opt.Coins > 0 ? opt.Coins.ToString() : "",
                    maxLength = 8,
                }, 90);
                coinField.style.marginLeft = 8;
                coinField.RegisterValueChangedCallback(e =>
                {
                    opt.Coins = int.TryParse(e.newValue.Trim(), out var c) && c > 0 ? c : 0;
                    UpdatePreview();
                });
                coinRow.Add(coinField);
                card.Add(coinRow);

                var wantList = new VisualElement().Column();
                wantList.style.marginTop = 8;
                void RedrawWants()
                {
                    wantList.Clear();
                    if (opt.Items.Count == 0)
                        wantList.Add(UkControls.Text("No items in this option (coins only is fine).", 12, UkTheme.TextMuted));
                    for (var w = 0; w < opt.Items.Count; w++)
                    {
                        var wi = w;
                        var want = opt.Items[w];
                        var row = new VisualElement().Row();
                        row.style.marginBottom = 4;
                        row.Add(UkControls.IconTile(
                            ObjectDB.instance?.GetItemPrefab(want.Prefab)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon(), 28));
                        var amt = StyleField(new TextField { value = want.Count.ToString(), maxLength = 4 }, 52);
                        amt.style.marginLeft = 6;
                        amt.RegisterValueChangedCallback(e =>
                        {
                            if (int.TryParse(e.newValue.Trim(), out var n) && n > 0)
                                opt.Items[wi] = new TradeWant(want.Prefab, n);
                            UpdatePreview();
                        });
                        row.Add(amt);
                        var lab = UkControls.Text(TradeWant.DisplayName(want.Prefab), 14, UkTheme.Text, wrap: false);
                        lab.style.marginLeft = 6;
                        lab.style.flexGrow = 1;
                        row.Add(lab);
                        row.Add(UkControls.MakeButton("✕", () =>
                        {
                            opt.Items.RemoveAt(wi);
                            RedrawWants();
                            UpdatePreview();
                        }, UkButtonKind.Ghost));
                        wantList.Add(row);
                    }
                }
                RedrawWants();
                card.Add(wantList);

                // Search to add items to this option
                var searchRow = new VisualElement().Row();
                searchRow.style.marginTop = 8;
                var search = new TextField { value = "", maxLength = 40 };
                search.style.flexGrow = 1;
                if (search.Q<VisualElement>("unity-text-input") is { } sin)
                {
                    sin.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 8);
                    sin.style.color = UkTheme.Text;
                    sin.style.fontSize = 14;
                }
                var qtyField = StyleField(new TextField { value = "1", maxLength = 4 }, 48);
                qtyField.style.marginLeft = 6;
                searchRow.Add(search);
                searchRow.Add(qtyField);
                card.Add(searchRow);

                var results = new VisualElement().Column();
                results.style.marginTop = 4;
                card.Add(results);
                IconCatalogEntry? picked = null;

                void FillResults(string q)
                {
                    results.Clear();
                    picked = null;
                    foreach (var hit in TradePrice.Search(q, 6))
                    {
                        var entry = hit;
                        var row = new VisualElement().Row();
                        row.style.marginBottom = 2;
                        row.pickingMode = PickingMode.Position;
                        row.Add(UkControls.IconTile(entry.Sprite, 24));
                        var local = global::Localization.instance != null
                            ? global::Localization.instance.Localize(entry.NameToken)
                            : entry.PrefabName;
                        var t = UkControls.Text(local, 13, UkTheme.Text, wrap: false);
                        t.style.marginLeft = 6;
                        t.style.flexGrow = 1;
                        row.Add(t);
                        row.RegisterCallback<ClickEvent>(_ =>
                        {
                            picked = entry;
                            if (!int.TryParse(qtyField.value.Trim(), out var qty) || qty <= 0)
                                qty = 1;
                            var existing = opt.Items.FindIndex(x =>
                                string.Equals(x.Prefab, entry.PrefabName, System.StringComparison.OrdinalIgnoreCase));
                            if (existing >= 0)
                                opt.Items[existing] = new TradeWant(entry.PrefabName, opt.Items[existing].Count + qty);
                            else
                                opt.Items.Add(new TradeWant(entry.PrefabName, qty));
                            RedrawWants();
                            UpdatePreview();
                            search.value = "";
                            FillResults("");
                        });
                        results.Add(row);
                    }
                }
                search.RegisterValueChangedCallback(e => FillResults(e.newValue));
                optionsHost.Add(card);
            }
            UpdatePreview();
        }

        RedrawOptions();

        var addOpt = UkControls.MakeButton("Add another payment (OR)", () =>
        {
            options.Add(new PayOption());
            RedrawOptions();
        }, UkButtonKind.Secondary);
        addOpt.style.marginTop = 10;
        body.Add(addOpt);

        dealPreview = UkControls.Text("", 15, UkTheme.Gold, wrap: true);
        dealPreview.style.marginTop = 14;
        body.Add(dealPreview);
        UpdatePreview();

        window.Body.Add(body);

        window.Footer.Add(UkControls.MakeButton("Clear price", () =>
        {
            var target = ShopState.Live(container, live);
            PayOptions.Clear(target);
            ItemLot.Clear(target);
            ShopState.NotifyChanged(container);
            onSaved?.Invoke();
            window.Close();
        }, UkButtonKind.Ghost));
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Cancel", () => window.Close(), UkButtonKind.Secondary));
        window.Footer.Add(UkControls.MakeButton("Save", () =>
        {
            var target = ShopState.Live(container, live);
            if (!int.TryParse(lotField.value.Trim(), out var lot) || lot <= 0)
            {
                Trade.TradeNet.Say("Buyer Gets amount must be at least 1.");
                return;
            }
            var valid = options.Where(o => !o.IsEmpty).ToList();
            if (valid.Count == 0)
            {
                Trade.TradeNet.Say("Add at least one payment (coins and/or items).");
                return;
            }
            if (ShopState.IsCoinsStack(target) && valid.All(o => o.Coins > 0 && o.Items.Count == 0))
            {
                Trade.TradeNet.Say("Coins can't be sold for coins only — add an item payment option.");
                return;
            }
            PayOptions.Set(target, valid);
            ItemLot.Set(target, lot);
            ShopState.NotifyChanged(container);
            onSaved?.Invoke();
            window.Close();
            Trade.TradeNet.Say("Price saved.");
        }, UkButtonKind.Primary));
        window.ShowFooter(true);
        window.Open();
    }

    private static PayOption CloneOption(PayOption o) =>
        new()
        {
            Coins = o.Coins,
            Items = o.Items.Select(w => new TradeWant(w.Prefab, w.Count)).ToList(),
        };
}
