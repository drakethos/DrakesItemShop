using System.Collections.Generic;
using DrakeModsLibs.API;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Vending;

/// <summary>
/// One placed vending machine. In service (its unlock keys are set): runes and lamp glow, a little light, and E opens the
/// game's own store with the vanilla trader's goods (bought coins leave the world as with the trader). Out of service:
/// dark, and E only says the configured "out of order" message. Hover and E arrive through the chest clone's Container
/// (<see cref="Storefront.IStorefrontUse"/>): Valheim's Hoverable can't be implemented against our references.
/// </summary>
internal sealed partial class VendingBehaviour : MonoBehaviour, Storefront.IStorefrontUse
{
    private static readonly Color Lit = new(0.35f, 0.85f, 1f);
    private static readonly Color Unlit = new(0.08f, 0.09f, 0.1f);
    private static Material? _litMaterial;
    private static Material? _darkMaterial;

    private ZNetView? _view;
    private VendingPiece.Variant? _variant;
    private readonly List<Renderer> _glow = new();
    private Light? _light;
    private Trader? _store;
    private bool? _shownOn;
    private float _nextCheck;

    /// <summary>The Glow material on the prefab: plain, untextured, emissive (used by <see cref="ForgeLook"/>).</summary>
    internal static void MakeGlow(Material material)
    {
        foreach (var texture in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" })
            if (material.HasProperty(texture))
                material.SetTexture(texture, null);
        material.color = Lit;
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Lit * 0.6f);   // a soft glow; more blooms into glare by day
        }
    }

    private void Awake() => TrySetUp();

    /// <summary>
    /// Finds its parts and adds the bay light, once its network object is valid. When a world loads, that can happen after
    /// Awake, so Update keeps trying; a placement ghost or the registered prefab never sets up.
    /// </summary>
    private void TrySetUp()
    {
        _view ??= GetComponent<ZNetView>();
        if (_variant != null || _view == null || !_view.IsValid())
            return;
        _variant = VendingPiece.Find(Utils.GetPrefabName(gameObject));
        if (_variant == null)
            return;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            if (renderer.name.Contains("_Rune") || renderer.name.EndsWith("_Lamp"))
                _glow.Add(renderer);
        FindSlotParts();

        var lightObject = new GameObject("VendingLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0.115f, 1.4f, 0.1f);   // inside the bay, on the goods
        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Lit;
        _light.range = 2.2f;
        _light.intensity = 3f;
        _light.enabled = false;   // the first check (within a second) decides
    }

    private void Update()
    {
        if (_variant == null)
            TrySetUp();
        if (_variant == null || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + 1f;
        var inService = InService;
        Show(inService);
        if (_light != null && !_vending)
            _light.enabled = LightOn;
        RefreshShelves(inService);
    }

    private bool InService => _variant != null && VendingSettings.KeysMet(_variant.RequiredKeys());

    private void Show(bool on)
    {
        if (_shownOn == on || _glow.Count == 0)
            return;
        _shownOn = on;
        if (_litMaterial == null)
        {
            _litMaterial = _glow[0].sharedMaterial;
            _darkMaterial = new Material(_litMaterial) { color = Unlit };
            if (_darkMaterial.HasProperty("_EmissionColor"))
            {
                _darkMaterial.SetColor("_EmissionColor", Color.black);
                _darkMaterial.DisableKeyword("_EMISSION");
            }
        }
        foreach (var renderer in _glow)
            renderer.sharedMaterial = on ? _litMaterial : _darkMaterial;
        if (_light != null)
            _light.enabled = LightOn;
    }

    /// <summary>The bay light: in service, and at night unless the server says always or off (by day it only adds glare).</summary>
    private bool LightOn => _shownOn == true && VendingSettings.BayLight switch
    {
        VendingSettings.LightMode.Always => true,
        VendingSettings.LightMode.Off => false,
        _ => EnvMan.instance == null || EnvMan.IsNight(),
    };

    /// <summary>A clear pane (the borrowed crystal is frosted and streaky): plain transparent glass on the Standard shader.</summary>
    internal static void MakeClearGlass(Material material)
    {
        foreach (var texture in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" })
            if (material.HasProperty(texture))
                material.SetTexture(texture, null);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_EMISSION");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.color = new Color(0.8f, 0.92f, 1f, 0.06f);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.92f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

    public string GetHoverName() =>
        global::Localization.instance.Localize(_variant != null ? $"${_variant.Token}_name" : "$drake_vending_haldor_name");

    public string GetHoverText()
    {
        if (!InService)
            return GetHoverName() + "\n<color=#9a9a9a>" + VendingSettings.OutOfOrderMessage + "</color>";
        return global::Localization.instance.Localize(GetHoverName() + "\n[<color=yellow><b>$KEY_Use</b></color>] $drake_vending_browse");
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user != Player.m_localPlayer || _variant == null)
            return false;
        if (!InService)
        {
            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, VendingSettings.OutOfOrderMessage);
            return false;
        }
        var store = Store();
        if (store == null)
        {
            CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, VendingSettings.OutOfOrderMessage);
            return false;
        }
        StoreGui.instance.Show(store);
        return true;
    }

    /// <summary>
    /// An inactive Trader holding the vanilla trader's goods, for <see cref="StoreGui"/>. Inactive so it never runs the
    /// NPC's Start/Update (animator, greetings); <see cref="TraderVoicePatch"/> skips its buy/sell lines.
    /// </summary>
    private Trader? Store()
    {
        if (_store != null)
            return _store;
        var source = ZNetScene.instance?.GetPrefab(_variant!.Trader)?.GetComponent<Trader>();
        if (source == null)
        {
            ItemShopPlugin.LogSource?.LogWarning($"Vending machine {_variant!.Name}: trader prefab '{_variant.Trader}' not found.");
            return null;
        }
        var holder = new GameObject("VendingStore");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);
        _store = holder.AddComponent<Trader>();
        _store.m_name = GetHoverName();
        _store.m_items = source.m_items;
        return _store;
    }

    internal static bool IsVendingStore(Trader trader) =>
        trader != null && trader.transform.parent != null && trader.transform.parent.GetComponent<VendingBehaviour>() != null;
}

/// <summary>A vending machine has no NPC to talk or animate: skip the trader's buy/sell voice lines for it; a buy plays the vend instead.</summary>
[HarmonyPatch]
internal static class TraderVoicePatch
{
    [HarmonyPatch(typeof(Trader), nameof(Trader.OnBought))]
    [HarmonyPrefix]
    private static bool OnBought(Trader __instance, Trader.TradeItem item)
    {
        if (!VendingBehaviour.IsVendingStore(__instance))
            return true;
        __instance.transform.parent.GetComponent<VendingBehaviour>().Vend(item);
        return false;
    }

    [HarmonyPatch(typeof(Trader), nameof(Trader.OnSold))]
    [HarmonyPrefix]
    private static bool OnSold(Trader __instance) => !VendingBehaviour.IsVendingStore(__instance);
}
