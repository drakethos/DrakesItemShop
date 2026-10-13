using System.Collections.Generic;
using DrakesItemShop.Config;
using UnityEngine;

namespace DrakesItemShop.DisplayCard;

/// <summary>
/// Stands a tent card on a display case for each of its <see cref="BuyOffers"/> — not hammer-placed. Cards use the same
/// slots as the stock below (one per slot, so a 7-slot case shows up to 7) but stand on the lid (<see
/// cref="DisplayCaseBehaviour.m_cardY"/>), grouped per <see cref="ItemShopConfig.BuyCardAlign"/>. Hovering one sells.
/// </summary>
public class BuyCardShelf : MonoBehaviour
{
    private const float RefreshSeconds = 0.45f;
    /// <summary>How much of its slot a card may fill along the case.</summary>
    private const float SlotFill = 0.85f;

    private DisplayCaseBehaviour? _case;
    private ZNetView? _view;
    private Container? _container;
    private Transform? _root;
    private string _shown = "\0";
    private float _next;
    private readonly List<DisplayCardFace> _faces = new();

    private void Awake()
    {
        _case = GetComponent<DisplayCaseBehaviour>();
        _view = GetComponent<ZNetView>();
        _container = GetComponent<Container>();
    }

    private void Update()
    {
        if (Time.time < _next)
            return;
        _next = Time.time + RefreshSeconds;
        if (!Ready(out var zdo))
            return;
        var fp = Fingerprint(zdo);
        if (fp == _shown)
            return;
        _shown = fp;
        Rebuild(zdo);
    }

    private void OnDestroy() => Clear();

    /// <summary>Force a rebuild after the Buy tab or a sale edits offers.</summary>
    internal void Dirty()
    {
        _next = 0f;
        if (!Ready(out var zdo))
        {
            _shown = "\0";
            return;
        }
        _shown = Fingerprint(zdo);
        Rebuild(zdo);
    }

    private bool Ready(out ZDO zdo)
    {
        zdo = null!;
        if (_view == null)
            _view = GetComponent<ZNetView>();
        if (_case == null)
            _case = GetComponent<DisplayCaseBehaviour>();
        if (_container == null)
            _container = GetComponent<Container>();
        if (_case == null || _view == null || !_view.IsValid() || ZNet.instance == null || ZNet.instance.IsDedicated())
            return false;
        zdo = _view.GetZDO();
        return zdo != null;
    }

    private static string Fingerprint(ZDO zdo) =>
        BuyOffers.Fingerprint(zdo) + "#" + BuyCrates.Fingerprint(zdo) + "#" + string.Join(",", BuyOffers.Held(zdo))
        + "#" + ItemShopConfig.BuyCardScale + "#" + ItemShopConfig.BuyCardAlign;

    private int Slots()
    {
        var width = _container != null ? _container.GetInventory()?.GetWidth() ?? _container.m_width : 0;
        return Mathf.Max(1, width);
    }

    private void Rebuild(ZDO zdo)
    {
        Clear();
        if (_case == null || !BuyOffers.Enabled(zdo))
            return;

        _root = new GameObject("drake_buy_cards").transform;
        _root.SetParent(transform, false);
        try
        {
            BuyCrateYard.Build(_root, _case, zdo);
        }
        catch (System.Exception ex)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[BuyCrates] {name}: crates failed to build: {ex.Message}");
        }

        var offers = BuyOffers.All(zdo);
        if (offers.Count == 0)
            return;

        var slots = Slots();
        var count = Mathf.Min(offers.Count, slots);
        var pitch = (_case.m_floorZ1 - _case.m_floorZ0) / slots;
        var scale = Mathf.Min(ItemShopConfig.BuyCardScale, Mathf.Abs(pitch) * SlotFill / Items.DisplayCard.Width);
        // First slot used: Left = the stocker's left (slot 0, the +Z end, same as the stock), Center splits the spare slots.
        var first = ItemShopConfig.BuyCardAlign switch
        {
            ItemShopConfig.CardAlign.Left => 0f,
            ItemShopConfig.CardAlign.Right => slots - count,
            _ => (slots - count) / 2f,
        };

        for (var i = 0; i < count; i++)
        {
            var offer = offers[i];
            var card = Items.DisplayCard.BuildVisual(_root, scale, out var visual);
            var t = card.transform;
            // Cases: right up against the customers' edge of the lid. Mats have no sides: down the middle.
            var x = _case.m_anySide ? _case.m_floorX : _case.m_cardEdgeX - Items.DisplayCard.Depth * scale / 2f;
            t.localPosition = new Vector3(x, _case.m_cardY, _case.m_floorZ1 - (first + i + 0.5f) * pitch);
            // Model faces ±Z; turn them across the case (±X) so one face meets customers and one the stocker.
            t.localRotation = Quaternion.Euler(0f, 90f, 0f);

            if (visual != null && DisplayCardFace.Create(visual) is { } face)
            {
                face.Refresh(offer);
                _faces.Add(face);
            }

            var tag = card.AddComponent<BuyCardTag>();
            tag.OfferIndex = i;
            tag.Shop = _container;
        }
    }

    private void Clear()
    {
        foreach (var face in _faces)
            face.Dispose();
        _faces.Clear();
        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
        }
    }
}
