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
        var effect = ScriptableObject.CreateInstance<SE_ShopkeeperApron>();
        effect.name = EffectName;
        effect.m_name = "$drake_shopapron_effect";
        effect.m_tooltip = "$drake_shopapron_effect_tip";
        ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
        return _effect = effect;
    }
}

#if DEBUG
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

#endif

internal static class ApronSettings
{
    private static BepInEx.Configuration.ConfigEntry<float>? _carry;
    private static BepInEx.Configuration.ConfigEntry<int>? _stamina;
    private static BepInEx.Configuration.ConfigEntry<ApronCondition>? _condition;

    /// <summary>Extra carry weight while the apron's condition holds.</summary>
    internal static float CarryBonus => _carry?.Value ?? 50f;

    /// <summary>Percent less stamina used (running, jumping, swimming, sneaking, dodging, attacking, blocking).</summary>
    internal static int StaminaCut => _stamina?.Value ?? 10;

    internal static ApronCondition Condition => _condition?.Value ?? ApronCondition.UnderRoofNotDungeon;

#if DEBUG
    private static BepInEx.Configuration.ConfigEntry<bool>? _give;
    private static BepInEx.Configuration.ConfigEntry<KeyCode>? _key;

    internal static bool GiveApron => _give?.Value ?? false;

    internal static KeyCode TunerKey => _key?.Value ?? KeyCode.F8;
#endif

    internal static void Bind(BepInEx.Configuration.ConfigFile config)
    {
        const string section = "09 Apron";
        _carry = Config.ItemShopConfig.Synced(config, section, "Carry weight bonus", 50f,
            "Extra carry weight the shopkeeper aprons give while their condition holds. 0 = none.",
            new BepInEx.Configuration.AcceptableValueRange<float>(0f, 500f));
        _stamina = Config.ItemShopConfig.Synced(config, section, "Stamina use reduction", 10,
            "Percent less stamina used for running, jumping, swimming, sneaking, dodging, attacking and blocking while the apron's condition holds. 0 = none.",
            new BepInEx.Configuration.AcceptableValueRange<int>(0, 90));
        _condition = Config.ItemShopConfig.Synced(config, section, "Bonus applies", ApronCondition.UnderRoofNotDungeon,
            "Always, UnderRoof (any roof, dungeons too) or UnderRoofNotDungeon (houses, sheds and shops, but not dungeons).");
#if DEBUG
        _give = config.Bind(section, "Give apron", false,
            "Debug builds only: put one of each apron in your inventory when you enter the world (once per game start).");
        _key = config.Bind(section, "Tuner key", KeyCode.F8,
            "Debug builds only: opens the apron fitting room (Save writes BepInEx/config/DrakesItemShop.apron.json).");
#endif
    }
}
