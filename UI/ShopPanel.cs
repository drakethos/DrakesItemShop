using System.Collections.Generic;
using DrakesItemShop.Networking;
using DrakesItemShop.Runtime;
using DrakesItemShop.Schema;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace DrakesItemShop.UI;

internal static class ShopPanel
{
    private static GameObject? _root;
    private static GameObject? _listRoot;
    private static Text? _title;
    private static Text? _status;
    private static ShopSession? _session;
    private static bool _sellMode;

    public static void Open(ShopSession session)
    {
        _session = session;
        _sellMode = false;
        EnsureBuilt();
        Refresh();
        _root!.SetActive(true);
        GUIManager.BlockInput(true);
    }

    public static void Close()
    {
        if (_root != null)
            _root.SetActive(false);
        GUIManager.BlockInput(false);
        ShopSessionState.Current = null;
    }

    private static void EnsureBuilt()
    {
        if (_root != null)
            return;

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _root = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            width: 520,
            height: 420,
            draggable: true);

        _title = GUIManager.Instance.CreateText(
            text: "Shop",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.88f),
            anchorMax: new Vector2(0.95f, 0.98f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 480f,
            height: 36f,
            addContentSizeFitter: false).GetComponent<Text>();

        _status = GUIManager.Instance.CreateText(
            text: "",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.06f),
            anchorMax: new Vector2(0.95f, 0.12f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: false,
            outlineColor: Color.black,
            width: 480f,
            height: 24f,
            addContentSizeFitter: false).GetComponent<Text>();

        var buyTab = GUIManager.Instance.CreateButton(
            text: "Buy",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.78f),
            anchorMax: new Vector2(0.25f, 0.86f),
            position: Vector2.zero,
            width: 100f,
            height: 28f).GetComponent<Button>();
        buyTab.onClick.AddListener(() => { _sellMode = false; Refresh(); });

        var sellTab = GUIManager.Instance.CreateButton(
            text: "Sell",
            parent: _root.transform,
            anchorMin: new Vector2(0.27f, 0.78f),
            anchorMax: new Vector2(0.47f, 0.86f),
            position: Vector2.zero,
            width: 100f,
            height: 28f).GetComponent<Button>();
        sellTab.onClick.AddListener(() => { _sellMode = true; Refresh(); });

        var closeBtn = GUIManager.Instance.CreateButton(
            text: "Close",
            parent: _root.transform,
            anchorMin: new Vector2(0.75f, 0.78f),
            anchorMax: new Vector2(0.95f, 0.86f),
            position: Vector2.zero,
            width: 100f,
            height: 28f).GetComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        _listRoot = new GameObject("ShopEntryList");
        _listRoot.transform.SetParent(_root.transform, false);
        var rt = _listRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.14f);
        rt.anchorMax = new Vector2(0.95f, 0.76f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _listRoot.AddComponent<VerticalLayoutGroup>().spacing = 4;
        _listRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _root.SetActive(false);
    }

    private static void Refresh()
    {
        if (_session == null || _listRoot == null || _title == null || _status == null)
            return;

        _title.text = _session.Shop.DisplayName ?? _session.Shop.Id ?? "Shop";
        _status.text = _sellMode ? "Sell items to this shop." : "Buy items with coins.";

        foreach (Transform child in _listRoot.transform)
            Object.Destroy(child.gameObject);

        var entries = _session.Catalog.Entries ?? new List<CatalogEntryDto>();
        int row = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Item))
                continue;

            int price = _sellMode
                ? entry.SellPrice?.ResolveCoinPrice() ?? 0
                : entry.BuyPrice?.ResolveCoinPrice() ?? 0;

            if (_sellMode && price <= 0 && !(entry.SellPrice?.IsItemCurrency ?? false))
                continue;

            if (!_sellMode && price <= 0 && !(entry.BuyPrice?.IsItemCurrency ?? false))
                continue;

            string label = ItemResolver.GetDisplayName(entry.Item!);
            string priceText = _sellMode
                ? $"Sell {entry.SellPrice?.ResolveCoinPrice() ?? 0}g"
                : $"Buy {price}g";

            var rowGo = GUIManager.Instance.CreateButton(
                text: $"{label} — {priceText}",
                parent: _listRoot.transform,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                position: Vector2.zero,
                width: 460f,
                height: 32f).gameObject;

            var le = rowGo.AddComponent<LayoutElement>();
            le.minHeight = 32;

            string itemId = entry.Item!;
            rowGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_sellMode)
                    ShopRpc.RequestSell(itemId, 1);
                else
                    ShopRpc.RequestBuy(itemId, 1);
                Refresh();
            });

            row++;
            if (row >= 12)
                break;
        }
    }
}
