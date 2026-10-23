using System;
using System.Collections.Generic;
using System.Linq;
using DrakesItemShop.Pieces;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>
/// The shopkeeper aprons: cape-slot cosmetics (so they layer over any armor) that carry a little extra, "Open for business".
/// Each item is a clone of the linen cape with its cape mesh removed; what you see on the body is built by <see cref="ApronBody"/>
/// from the numbers in <see cref="ApronTuning"/> (which <see cref="ApronTuner"/> lets you change live) and the item's
/// <see cref="ApronStyle"/> (colours, bib or waist apron, pocket, studs).
/// </summary>
internal static class ApronItem
{
    internal const string EffectName = "SE_Drake_Shopkeeper";
    private const string CloneSource = "CapeLinen";

    /// <summary>A lit piece material borrowed for the apron's cloth and straps (its texture and colour are replaced).</summary>
    internal static Material? ClothMaterial;

    private static StatusEffect? _effect;

    internal static void Register()
    {
        ApronStyles.Index();
        try
        {
            // An ordinary lit piece material (plain texture and colour). The cape's own creature-shader material has hue / style
            // controls that turned cream cloth black and brown leather red.
            var chest = PrefabManager.Instance.GetPrefab("piece_chest_wood");
            ClothMaterial = chest == null ? null
                : chest.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Custom/Piece");
            foreach (var style in ApronStyles.All)
                RegisterOne(style);
        }
        catch (Exception ex)
        {
            ItemShopPlugin.LogSource?.LogError($"[Apron] couldn't register the aprons: {ex}");
        }
    }

    private static void RegisterOne(ApronStyle style)
    {
        var token = "$" + style.PrefabName.ToLowerInvariant();
        var config = new ItemConfig
        {
            Name = token + "_name",
            Description = token + "_desc",
            CraftingStation = CraftingStations.Workbench,
            Icon = ApronArt.Icon(style),
            Requirements = DisplayCasePiece.ParseCost(style.Cost, style.PrefabName)
                .Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
        };
        var apron = new CustomItem(style.PrefabName, CloneSource, config);
        if (apron.ItemDrop == null)
        {
            ItemShopPlugin.LogSource?.LogError($"[Apron] clone '{CloneSource}' has no ItemDrop.");
            return;
        }

        // The cape mesh goes: what you wear is built at runtime. The dropped model (a folded cloth) stays.
        if (apron.ItemPrefab.transform.Find("attach_skin") is { } cape)
            UnityEngine.Object.DestroyImmediate(cape.gameObject);

        var shared = apron.ItemDrop.m_itemData.m_shared;
        shared.m_armor = 0f;
        shared.m_armorPerLevel = 0f;
        shared.m_movementModifier = 0f;
        shared.m_maxQuality = 1;
        shared.m_useDurability = false;
        shared.m_weight = 0.5f;
        shared.m_equipStatusEffect = Effect();

        ItemManager.Instance.AddItem(apron);
        apron.ItemDrop.m_itemData.m_dropPrefab = apron.ItemPrefab;
        ItemShopPlugin.LogSource?.LogInfo($"[Apron] Registered {style.PrefabName}");
    }

    private static StatusEffect Effect()
    {
        if (_effect != null)
            return _effect;
        var effect = ScriptableObject.CreateInstance<SE_Stats>();
        effect.name = EffectName;
        effect.m_name = "$drake_shopapron_effect";
        effect.m_tooltip = "$drake_shopapron_effect_tip";
        effect.m_addMaxCarryWeight = 50f;
        ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
        return _effect = effect;
    }
}

/// <summary>While the fitting is being worked out: one of each apron in your inventory on entering the world (config 09 Apron).</summary>
[HarmonyPatch(typeof(Player), "OnSpawned")]
internal static class ApronGivePatch
{
    private static bool _given;

    [HarmonyPostfix]
    private static void Postfix(Player __instance)
    {
        if (_given || __instance != Player.m_localPlayer || !ApronSettings.GiveApron)
            return;
        _given = true;
        __instance.StartCoroutine(Give(__instance));
    }

    private static System.Collections.IEnumerator Give(Player player)
    {
        yield return new WaitForSeconds(3f);
        if (player == null || ObjectDB.instance == null)
            yield break;
        var inventory = player.GetInventory();
        foreach (var style in ApronStyles.All)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(style.PrefabName);
            if (prefab == null)
                continue;
            var have = false;
            foreach (var item in inventory.GetAllItems())
                if (item.m_dropPrefab != null && item.m_dropPrefab.name == style.PrefabName)
                    have = true;
            if (!have)
                inventory.AddItem(prefab, 1);
        }
        ItemShopPlugin.LogSource?.LogInfo("[Apron] gave you one of each apron you didn't have (turn off 09 Apron > Give apron to stop this).");
    }
}

internal static class ApronSettings
{
    private static BepInEx.Configuration.ConfigEntry<bool>? _give;
    private static BepInEx.Configuration.ConfigEntry<KeyCode>? _key;

    internal static bool GiveApron => _give?.Value ?? false;

    internal static KeyCode TunerKey => _key?.Value ?? KeyCode.F8;

    internal static void Bind(BepInEx.Configuration.ConfigFile config)
    {
        _give = config.Bind("09 Apron", "Give apron", true,
            "While the aprons are being tuned: put one of each apron in your inventory when you enter the world (once per game start, only the ones you don't have).");
        _key = config.Bind("09 Apron", "Tuner key", KeyCode.F8,
            "Opens the apron fitting room (see the panel for the controls; Save writes BepInEx/config/DrakesItemShop.apron.json).");
    }
}
