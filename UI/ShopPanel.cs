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

    private static RectTransform? _statusIconRoot;

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

        ClearStatusIcons();

    }



    public static bool IsOpen => _root != null && _root.activeSelf;



    public static bool TryShowStatus(string message)

    {

        if (!IsOpen || _status == null)

            return false;



        _status.text = message;

        ClearStatusIcons();

        return true;

    }



    public static bool TryShowTransactionResult(TransactionResult result, string itemId, bool buy)

    {

        if (!IsOpen || _status == null)

            return false;



        _status.text = ShopRpc.ResultMessage(result);

        ClearStatusIcons();



        if (result == TransactionResult.NotEnoughCurrency)

            PopulateCurrencyIcons(itemId, buy);



        return true;

    }



    private static void PopulateCurrencyIcons(string catalogItemId, bool buy)

    {

        if (_statusIconRoot == null || _session?.Catalog.Entries == null)

            return;



        var entry = _session.Catalog.Entries.Find(e =>

            string.Equals(e.Item, catalogItemId, System.StringComparison.OrdinalIgnoreCase));

        if (entry == null)

            return;



        var iconIds = new List<string>(2);



        if (buy)

        {

            if (entry.BuyPrice?.IsItemCurrency == true && !string.IsNullOrWhiteSpace(entry.BuyPrice.Item))

                iconIds.Add(entry.BuyPrice.Item!);

            else

                iconIds.Add(ItemResolver.CoinItemName);



            if (!string.IsNullOrWhiteSpace(entry.Item))

                iconIds.Add(entry.Item!);

        }

        else

        {

            if (!string.IsNullOrWhiteSpace(entry.Item))

                iconIds.Add(entry.Item!);



            if (entry.SellPrice?.IsItemCurrency == true && !string.IsNullOrWhiteSpace(entry.SellPrice.Item))

                iconIds.Add(entry.SellPrice.Item!);

            else if (iconIds.Count < 2)

                iconIds.Add(ItemResolver.CoinItemName);

        }



        int shown = 0;

        foreach (var id in iconIds)

        {

            if (shown >= 2)

                break;

            if (string.IsNullOrWhiteSpace(id))

                continue;

            if (shown > 0 && string.Equals(iconIds[0], id, System.StringComparison.OrdinalIgnoreCase))

                continue;



            AddStatusIcon(id);

            shown++;

        }

    }



    private static void ClearStatusIcons()

    {

        if (_statusIconRoot == null)

            return;



        for (int i = _statusIconRoot.childCount - 1; i >= 0; i--)

            Object.Destroy(_statusIconRoot.GetChild(i).gameObject);

    }



    private static void AddStatusIcon(string itemId)

    {

        if (_statusIconRoot == null)

            return;



        var sprite = ItemResolver.GetItemIcon(itemId);

        var iconGo = new GameObject("StatusIcon", typeof(RectTransform), typeof(Image));

        iconGo.transform.SetParent(_statusIconRoot, false);

        var img = iconGo.GetComponent<Image>();

        img.sprite = sprite;

        img.preserveAspect = true;

        img.color = Color.white;

        img.enabled = sprite != null;

        var le = iconGo.AddComponent<LayoutElement>();

        le.preferredWidth = 36;

        le.preferredHeight = 36;

        le.minWidth = 36;

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

            anchorMin: new Vector2(0.05f, 0.1f),

            anchorMax: new Vector2(0.95f, 0.16f),

            position: Vector2.zero,

            font: GUIManager.Instance.AveriaSerifBold,

            fontSize: 14,

            color: Color.white,

            outline: false,

            outlineColor: Color.black,

            width: 480f,

            height: 24f,

            addContentSizeFitter: false).GetComponent<Text>();



        var iconRowGo = new GameObject("StatusIcons");

        iconRowGo.transform.SetParent(_root.transform, false);

        _statusIconRoot = iconRowGo.AddComponent<RectTransform>();

        _statusIconRoot.anchorMin = new Vector2(0.05f, 0.02f);

        _statusIconRoot.anchorMax = new Vector2(0.95f, 0.1f);

        _statusIconRoot.offsetMin = Vector2.zero;

        _statusIconRoot.offsetMax = Vector2.zero;

        var iconLayout = iconRowGo.AddComponent<HorizontalLayoutGroup>();

        iconLayout.spacing = 8;

        iconLayout.childAlignment = TextAnchor.MiddleLeft;

        iconLayout.childForceExpandWidth = false;

        iconLayout.childForceExpandHeight = false;



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

        rt.anchorMin = new Vector2(0.05f, 0.18f);

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

        if (string.IsNullOrEmpty(_status.text) ||

            _status.text.StartsWith("Buy items", System.StringComparison.Ordinal) ||

            _status.text.StartsWith("Sell items", System.StringComparison.Ordinal))

        {

            _status.text = _sellMode

                ? "Sell items to this shop."

                : "Buy items with coins.";

            ClearStatusIcons();

        }



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


