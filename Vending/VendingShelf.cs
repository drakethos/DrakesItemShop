using System.Collections;
using System.Collections.Generic;
using System.Text;
using DrakesItemShop.Pieces;
using UnityEngine;

namespace DrakesItemShop.Vending;

/// <summary>
/// The shelves (5 x 4 slots): still copies of what the store sells you right now first (Trader.GetAvailableItems, so items
/// behind world keys stay hidden), then "Sold out" cards for the rest; and the vend: the slot's rune flashes, its pusher shoves the item off the shelf, it drops
/// into the chute tray, rests there a moment and is gone (the store already put it in your bag). Local only: other
/// players see the shelves, not your vend.
/// </summary>
internal sealed partial class VendingBehaviour
{
    // Piece space (Unity), from make_vending_machine.py's VENDINFO (Blender (x, y, z) -> Unity (x, z, y)).
    private static readonly float[] SlotX = { 0.399f, 0.257f, 0.115f, -0.027f, -0.169f };   // left to right, seen from the front
    private static readonly float[] ShelfTop = { 1.908f, 1.584f, 1.26f, 0.936f };           // row 0 = top
    private const int Slots = 20;
    private const float SlotWidth = 0.11f;
    private const float SlotDepth = 0.24f;
    private const float MaxHeight = 0.13f;
    private const float CardScale = 0.55f;         // the shop's tent card, shrunk to a slot
    private const float ItemZ = 0.0f;              // item centre, front to back
    private const float PusherFront = -0.30f;      // the pusher's front face at rest
    private const float PushTravel = 0.57f;
    private const float ShelfEdge = 0.29f;
    private const float TrayY = 0.36f;
    private const float TrayZ = 0.40f;

    private static Material? _flashMaterial;

    private Transform? _shelf;
    private readonly GameObject?[] _shown = new GameObject?[Slots];
    private readonly Trader.TradeItem?[] _slotItem = new Trader.TradeItem?[Slots];
    private readonly float[] _halfDepth = new float[Slots];   // half the shown item's front-to-back size
    private readonly Transform?[] _pushers = new Transform?[Slots];
    private readonly Renderer?[] _runes = new Renderer?[Slots];
    private readonly List<DisplayCard.DisplayCardFace> _cards = new();
    private string _shownStock = "";
    private bool _vending;

    private void OnDestroy()
    {
        foreach (var card in _cards)
            card.Dispose();
        _cards.Clear();
    }

    private void FindSlotParts()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            for (var i = 0; i < Slots; i++)
            {
                if (renderer.name.EndsWith("_Pusher" + i))
                    _pushers[i] = renderer.transform;
                else if (renderer.name.EndsWith("_Rune" + i))
                    _runes[i] = renderer;
            }
    }

    /// <summary>
    /// Fills the slots (only when something changed): everything you can buy now first, in store order, then a "Sold out"
    /// card for each item that isn't unlocked yet (or a one-off you already bought), so nothing is spoiled. Empty while out
    /// of order.
    /// </summary>
    private void RefreshShelves(bool inService)
    {
        if (_vending || Player.m_localPlayer == null)
            return;
        var store = inService ? Store() : null;
        var all = store != null ? store.m_items : new List<Trader.TradeItem>();
        var available = store != null ? new HashSet<Trader.TradeItem>(store.GetAvailableItems()) : new HashSet<Trader.TradeItem>();
        var order = new List<Trader.TradeItem>();
        foreach (var item in all)
            if (item.m_prefab != null && available.Contains(item))
                order.Add(item);
        var lockedCount = 0;
        foreach (var item in all)
            if (item.m_prefab != null && !available.Contains(item))
                lockedCount++;
        var key = new StringBuilder();
        foreach (var item in order)
            key.Append(item.m_prefab.name).Append(';');
        key.Append('|').Append(lockedCount);
        if (key.ToString() == _shownStock && _shelf != null)
            return;
        _shownStock = key.ToString();

        foreach (var card in _cards)
            card.Dispose();
        _cards.Clear();
        if (_shelf != null)
            Destroy(_shelf.gameObject);
        _shelf = new GameObject("drake_vending_items").transform;
        _shelf.SetParent(transform, false);
        for (var i = 0; i < Slots; i++)
        {
            _shown[i] = null;
            _slotItem[i] = null;
            if (i < order.Count)
            {
                _slotItem[i] = order[i];
                _shown[i] = ShowOnShelf(order[i].m_prefab.gameObject, i);
            }
            else if (i < order.Count + lockedCount)
            {
                SoldOutCard(i);
            }
        }
    }

    /// <summary>The shop's paper tent card in the slot, saying "Sold out".</summary>
    private void SoldOutCard(int slot)
    {
        var card = DisplayCard.Items.DisplayCard.BuildVisual(_shelf!, CardScale, out var visual);
        card.transform.localPosition = SlotPosition(slot);
        if (visual != null && DisplayCard.DisplayCardFace.Create(visual) is { } face)
        {
            face.ShowNotice(VendingSettings.SoldOutText);
            _cards.Add(face);
        }
    }

    private GameObject? ShowOnShelf(GameObject prefab, int slot)
    {
        var model = DisplayCaseBehaviour.StillCopy(prefab);
        if (model == null)
            return null;
        if (!DisplayCaseBehaviour.LocalBounds(model.transform, out var bounds))
        {
            Destroy(model);
            return null;
        }

        // Lay it down: longest side across the slot, middle front to back, shortest up; shrink to fit the slot.
        var axes = new List<(Vector3 Dir, float Size)> { (Vector3.right, bounds.size.x), (Vector3.up, bounds.size.y), (Vector3.forward, bounds.size.z) };
        axes.Sort((a, b) => b.Size.CompareTo(a.Size));
        var lay = Quaternion.Inverse(Quaternion.LookRotation(axes[1].Dir, axes[2].Dir));
        var scale = Mathf.Min(1f, SlotWidth / Mathf.Max(axes[0].Size, 0.001f), SlotDepth / Mathf.Max(axes[1].Size, 0.001f),
            MaxHeight / Mathf.Max(axes[2].Size, 0.001f));

        var t = model.transform;
        t.SetParent(_shelf, false);
        t.localRotation = lay;
        t.localScale = Vector3.one * scale;
        var centre = lay * (bounds.center * scale);
        t.localPosition = SlotPosition(slot) - centre + Vector3.up * (axes[2].Size * scale / 2f + 0.003f);
        // An item's own light (a glowing orb, a lantern) would flood the machine and its neighbours with colour.
        foreach (var light in model.GetComponentsInChildren<Light>(true))
            DestroyImmediate(light);
        model.SetActive(true);
        _halfDepth[slot] = axes[1].Size * scale / 2f;
        return model;
    }

    /// <summary>For tests: what this machine shows and does right now.</summary>
    internal string TestState()
    {
        var store = _shownOn == true ? Store() : null;
        var available = store != null ? new HashSet<Trader.TradeItem>(store.GetAvailableItems()) : new HashSet<Trader.TradeItem>();
        var items = 0;
        var lockedShown = 0;
        for (var i = 0; i < Slots; i++)
            if (_shown[i] != null)
            {
                items++;
                if (_slotItem[i] == null || !available.Contains(_slotItem[i]!))
                    lockedShown++;
            }
        var glow = _glow.Count > 0 && _litMaterial != null && _glow[0].sharedMaterial == _litMaterial ? "lit" : "dark";
        return $"service={(_shownOn == true ? "on" : "off")};glow={glow};light={(_light != null && _light.enabled ? "on" : "off")};" +
               $"items={items};cards={_cards.Count};lockedShown={lockedShown};available={available.Count};vending={(_vending ? "yes" : "no")}";
    }

    internal Trader? TestStore() => Store();

    private static Vector3 SlotPosition(int slot) =>
        new(SlotX[slot % SlotX.Length], ShelfTop[slot / SlotX.Length], ItemZ);

    /// <summary>
    /// Called when the local player bought <paramref name="item"/> here: play the vend for its slot. A purchase is always
    /// seen: an item that didn't fit on the shelves is put into the last slot first and vended from there.
    /// </summary>
    internal void Vend(Trader.TradeItem item)
    {
        if (_vending || _shelf == null || item.m_prefab == null)
            return;
        var slot = System.Array.IndexOf(_slotItem, item);
        if (slot < 0 || _shown[slot] == null)
        {
            slot = Slots - 1;
            var card = _shelf.childCount > 0 ? FindInSlot(slot) : null;
            if (card != null)
                Destroy(card);
            if (_shown[slot] != null)
                Destroy(_shown[slot]);
            _slotItem[slot] = item;
            _shown[slot] = ShowOnShelf(item.m_prefab.gameObject, slot);
            if (_shown[slot] == null)
                return;
        }
        StartCoroutine(VendRoutine(slot));
    }

    /// <summary>Whatever stands in <paramref name="slot"/> on the shelf (a card), by its spot.</summary>
    private GameObject? FindInSlot(int slot)
    {
        var at = SlotPosition(slot);
        foreach (Transform child in _shelf!)
            if (Mathf.Abs(child.localPosition.x - at.x) < 0.01f && Mathf.Abs(child.localPosition.y - at.y) < 0.01f &&
                child.gameObject != _shown[slot])
                return child.gameObject;
        return null;
    }

    private IEnumerator VendRoutine(int slot)
    {
        _vending = true;
        var model = _shown[slot]!;
        var pusher = _pushers[slot];
        var rune = _runes[slot];
        var runeMaterial = rune != null ? rune.sharedMaterial : null;
        if (rune != null)
            rune.sharedMaterial = FlashMaterial(runeMaterial);
        if (_light != null)
        {
            _light.enabled = true;     // by day too, for the flash
            _light.intensity = 4.5f;
        }

        var half = _halfDepth[slot];
        var itemStart = model.transform.localPosition;
        var reach = Mathf.Clamp(ItemZ - half - PusherFront, 0f, PushTravel);   // pusher travel before it touches the item

        yield return new WaitForSeconds(0.25f);
        // The pusher slides up to the item, then shoves it to the shelf's edge.
        yield return Animate(0.3f, k => SetPusher(pusher, reach * k));
        var shove = PushTravel - reach;
        yield return Animate(0.45f, k =>
        {
            SetPusher(pusher, reach + shove * k);
            model.transform.localPosition = itemStart + Vector3.forward * shove * k;
        });

        // Off the edge: it tips and falls behind the glass into the tray.
        var edge = model.transform.localPosition;
        var tray = new Vector3(edge.x, TrayY + half * 0.5f, TrayZ);
        var startRotation = model.transform.localRotation;
        var tipped = Quaternion.AngleAxis(70f, Vector3.right) * startRotation;
        yield return Animate(0.5f, k =>
        {
            var p = Vector3.Lerp(edge + Vector3.forward * 0.06f, tray, k);
            p.y = Mathf.Lerp(edge.y, tray.y, k * k);       // falling: slow, then fast
            model.transform.localPosition = p;
            model.transform.localRotation = Quaternion.Slerp(startRotation, tipped, Mathf.Min(1f, k * 1.6f));
        });
        if (rune != null)
            rune.sharedMaterial = runeMaterial;
        if (_light != null)
        {
            _light.intensity = 3f;
            _light.enabled = LightOn;
        }

        // The pusher goes home. In the tray: poof (the dust and knock of a piece being built), and the magic that shrank
        // it to fit the shelf lets go: it pops out of the chute to its full size, hangs a moment, then zips into your
        // pocket (it's already in your bag).
        StartCoroutine(Animate(0.4f, k => SetPusher(pusher, PushTravel * (1f - k))));
        yield return new WaitForSeconds(0.35f);
        var t = model.transform;
        if (GetComponent<Piece>() is { } piece)
            PlayEffect(piece.m_placeEffect, transform.TransformPoint(tray + Vector3.forward * 0.15f), transform.rotation);
        var small = t.localScale;
        var from = t.localPosition;
        var shown = new Vector3(from.x, 0.95f, TrayZ + 0.45f);   // out in front of the chute, at chest height
        yield return Animate(0.3f, k =>
        {
            t.localPosition = Vector3.Lerp(from, shown, k);
            t.localScale = Vector3.LerpUnclamped(small, Vector3.one, k * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)));   // a little overshoot
        });
        var spinFrom = t.localRotation;
        yield return Animate(0.6f, k => t.localRotation = spinFrom * Quaternion.AngleAxis(40f * k, Vector3.up));

        var player = Player.m_localPlayer;
        if (player != null)
        {
            // You reach out and grab it (the pickup animation only; the item was never dropped, the store already put it
            // in your bag, so nothing can be lost to desync).
            GrabAnimation(player);
            var start = t.position;
            yield return Animate(0.3f, k =>
            {
                t.position = Vector3.Lerp(start, player.transform.position + Vector3.up * 1.1f, k);
                t.localScale = Vector3.one * (1f - 0.9f * k);
            });
            PlayEffect(player.m_pickupEffects, player.transform.position, Quaternion.identity);
        }
        Destroy(model);

        _vending = false;
        _shownStock = "";   // restock the shelf
        RefreshShelves(InService);
    }

    private static readonly System.Reflection.FieldInfo? ZAnim = HarmonyLib.AccessTools.Field(typeof(Character), "m_zanim");

    /// <summary>The player's own pickup motion (Humanoid uses the "interact" trigger for picking up and dropping).</summary>
    private static void GrabAnimation(Player player)
    {
        if (ZAnim?.GetValue(player) is ZSyncAnimation anim)
            anim.SetTrigger("interact");
    }

    private static System.Reflection.MethodInfo? _effectCreate;

    /// <summary>
    /// EffectList.Create through reflection: live Valheim's has an extra ZDOID parameter our references lack, so a direct
    /// call compiles but throws in game. Fills every parameter after position and rotation with its default.
    /// </summary>
    private static void PlayEffect(EffectList effects, Vector3 position, Quaternion rotation)
    {
        _effectCreate ??= typeof(EffectList).GetMethod("Create");
        if (_effectCreate == null)
            return;
        var parameters = _effectCreate.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = position;
        args[1] = rotation;
        for (var i = 2; i < parameters.Length; i++)
            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue
                : parameters[i].ParameterType.IsValueType ? System.Activator.CreateInstance(parameters[i].ParameterType) : null;
        _effectCreate.Invoke(effects, args);
    }

    private static void SetPusher(Transform? pusher, float travel)
    {
        if (pusher != null)
            pusher.localPosition = Vector3.forward * travel;
    }

    private static IEnumerator Animate(float seconds, System.Action<float> step)
    {
        for (var t = 0f; t < seconds; t += Time.deltaTime)
        {
            step(Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        step(1f);
    }

    private static Material FlashMaterial(Material? lit)
    {
        if (_flashMaterial != null)
            return _flashMaterial;
        _flashMaterial = lit != null ? new Material(lit) : new Material(Shader.Find("Standard"));
        _flashMaterial.color = Color.white;
        if (_flashMaterial.HasProperty("_EmissionColor"))
        {
            _flashMaterial.EnableKeyword("_EMISSION");
            _flashMaterial.SetColor("_EmissionColor", new Color(0.6f, 0.95f, 1f) * 3f);
        }
        return _flashMaterial;
    }
}
