using DrakeModsLibs.API;
using System.Linq;
using DrakesItemShop.Runtime;
using DrakesItemShop.Schema;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace DrakesItemShop.UI;

internal static class ShopManagePanel
{
    private static GameObject? _root;
    private static GameObject? _listRoot;
    private static Text? _title;
    private static Text? _status;
    private static ShopManageAnchor? _anchor;

    public static void Open(ShopManageAnchor anchor)
    {
        _anchor = anchor;
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
        _anchor = null;
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
            width: 480,
            height: 460,
            draggable: true);

        _title = GUIManager.Instance.CreateText(
            text: "Shop setup",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.9f),
            anchorMax: new Vector2(0.95f, 0.98f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 20,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 440f,
            height: 32f,
            addContentSizeFitter: false).GetComponent<Text>();

        _status = GUIManager.Instance.CreateText(
            text: "",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.82f),
            anchorMax: new Vector2(0.95f, 0.88f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: Color.white,
            outline: false,
            outlineColor: Color.black,
            width: 440f,
            height: 24f,
            addContentSizeFitter: false).GetComponent<Text>();

        var closeBtn = GUIManager.Instance.CreateButton(
            text: "Close",
            parent: _root.transform,
            anchorMin: new Vector2(0.72f, 0.9f),
            anchorMax: new Vector2(0.95f, 0.97f),
            position: Vector2.zero,
            width: 90f,
            height: 26f).GetComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        _listRoot = new GameObject("ShopManageList");
        _listRoot.transform.SetParent(_root.transform, false);
        var rt = _listRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.34f);
        rt.anchorMax = new Vector2(0.95f, 0.8f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _listRoot.AddComponent<VerticalLayoutGroup>().spacing = 4;
        _listRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateRestockButton("Stock chest", 0.24f, 0.32f, () => OpenContainer(ShopContainerAccess.Stock));
        CreateRestockButton("Treasury", 0.52f, 0.32f, () => OpenContainer(ShopContainerAccess.Treasury));
        CreateRestockButton("Sell hold", 0.05f, 0.16f, () => OpenContainer(ShopContainerAccess.SellHold));
        CreateRestockButton("Dispense", 0.52f, 0.16f, () => OpenContainer(ShopContainerAccess.Dispense));

        var linkBtn = GUIManager.Instance.CreateButton(
            text: "Link ledger in inventory",
            parent: _root.transform,
            anchorMin: new Vector2(0.05f, 0.05f),
            anchorMax: new Vector2(0.95f, 0.12f),
            position: Vector2.zero,
            width: 420f,
            height: 28f).GetComponent<Button>();
        linkBtn.onClick.AddListener(OnLinkLedger);

        _root.SetActive(false);
    }

    private static void CreateRestockButton(string label, float xMin, float xMax, UnityEngine.Events.UnityAction onClick)
    {
        if (_root == null)
            return;

        var btn = GUIManager.Instance.CreateButton(
            text: label,
            parent: _root.transform,
            anchorMin: new Vector2(xMin, xMax - 0.08f),
            anchorMax: new Vector2(xMin + 0.43f, xMax),
            position: Vector2.zero,
            width: 180f,
            height: 28f).GetComponent<Button>();
        btn.onClick.AddListener(onClick);
    }

    private static void Refresh()
    {
        if (_anchor == null || _listRoot == null || _title == null || _status == null)
            return;

        string current = _anchor.GetShopId();
        _title.text = $"{_anchor.Label} — shop id";
        _status.text = string.IsNullOrWhiteSpace(current)
            ? "No shop id set. Pick one below."
            : $"Current: {current}";

        foreach (Transform child in _listRoot.transform)
            Object.Destroy(child.gameObject);

        var shops = ShopRegistry.Instance.Shops.Values
            .OrderBy(s => s.DisplayName ?? s.Id)
            .ToList();

        int row = 0;
        foreach (var shop in shops)
        {
            if (string.IsNullOrWhiteSpace(shop.Id))
                continue;

            string id = shop.Id.Trim();
            string label = string.IsNullOrWhiteSpace(shop.DisplayName) ? id : $"{shop.DisplayName} ({id})";
            bool isCurrent = id.Equals(current, System.StringComparison.OrdinalIgnoreCase);

            var rowGo = GUIManager.Instance.CreateButton(
                text: isCurrent ? $"> {label}" : label,
                parent: _listRoot.transform,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                position: Vector2.zero,
                width: 420f,
                height: 30f).gameObject;

            rowGo.AddComponent<LayoutElement>().minHeight = 30;
            string captured = id;
            rowGo.GetComponent<Button>().onClick.AddListener(() => ApplyShopId(captured));

            row++;
            if (row >= 8)
                break;
        }
    }

    private static void ApplyShopId(string shopId)
    {
        if (_anchor == null)
            return;

        if (!ShopRegistry.Instance.TryGetShop(shopId, out _))
        {
            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, $"Unknown shop: {shopId}");
            return;
        }

        _anchor.SetShopId(shopId);
        _anchor.AfterShopIdChanged();
        CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, $"Shop id set to '{shopId}'.");
        Refresh();
    }

    private static void OnLinkLedger()
    {
        var player = Player.m_localPlayer;
        if (player == null || _anchor?.Box == null)
        {
            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, "Link ledger from a Merchant Box only.");
            return;
        }

        if (ShopManageService.TryLinkLedger(player, _anchor.Box))
            Close();
    }

    private static void OpenContainer(ShopContainerAccess access)
    {
        var player = Player.m_localPlayer;
        var box = _anchor?.Box;
        if (player == null || box == null)
            return;

        Close();
        switch (access)
        {
            case ShopContainerAccess.Stock:
                box.OpenStock(player);
                break;
            case ShopContainerAccess.Treasury:
                box.OpenTreasury(player);
                break;
            case ShopContainerAccess.SellHold:
                box.OpenSellHold(player);
                break;
            case ShopContainerAccess.Dispense:
                box.OpenDispense(player);
                break;
        }
    }

    private enum ShopContainerAccess
    {
        Stock,
        Treasury,
        SellHold,
        Dispense,
    }
}
