using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>Where the apron's bonuses apply.</summary>
internal enum ApronCondition
{
    /// <summary>Everywhere.</summary>
    Always,
    /// <summary>Only under a roof (the same check as the Rested shelter), dungeons included.</summary>
    UnderRoof,
    /// <summary>Only under a roof, and not inside a dungeon: houses, sheds, shops.</summary>
    UnderRoofNotDungeon,
}

/// <summary>
/// "Open for business": carry weight and stamina savings from the apron's config, on while the condition holds (default: under a
/// roof, not in a dungeon). Re-checked about once a second, so the bonus follows you through the door.
/// </summary>
internal sealed class SE_ShopkeeperApron : SE_Stats
{
    private static readonly MethodInfo? InShelter = AccessTools.Method(typeof(Player), "InShelter");
    private static readonly MethodInfo? InInterior = AccessTools.Method(typeof(Player), "InInterior");

    private float _next;
    private bool _on;

    public override void Setup(Character character)
    {
        base.Setup(character);
        Apply(true);
    }

    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        _next -= dt;
        if (_next > 0f)
            return;
        _next = 1f;
        Apply(false);
    }

    public override string GetTooltipString()
    {
        var carry = ApronSettings.CarryBonus;
        var cut = ApronSettings.StaminaCut;
        var text = "";
        if (carry > 0f)
            text += $"Carry weight: +{carry:0}\n";
        if (cut > 0)
            text += $"Stamina use: -{cut}%\n";
        text += ApronSettings.Condition switch
        {
            ApronCondition.UnderRoof => "While under a roof.",
            ApronCondition.UnderRoofNotDungeon => "While under a roof (not in a dungeon).",
            _ => "Always.",
        };
        return text;
    }

    private void Apply(bool force)
    {
        var on = Covered(m_character as Player, ApronSettings.Condition);
        if (!force && on == _on)
            return;
        _on = on;
        var cut = on ? -ApronSettings.StaminaCut / 100f : 0f;
        m_addMaxCarryWeight = on ? ApronSettings.CarryBonus : 0f;
        m_runStaminaDrainModifier = cut;
        m_jumpStaminaUseModifier = cut;
        m_attackStaminaUseModifier = cut;
        m_blockStaminaUseModifier = cut;
        m_dodgeStaminaUseModifier = cut;
        m_swimStaminaUseModifier = cut;
        m_sneakStaminaUseModifier = cut;
    }

    /// <summary>Is the player where the apron's bonuses apply?</summary>
    internal static bool Covered(Player? player, ApronCondition condition)
    {
        if (player == null)
            return false;
        if (condition == ApronCondition.Always)
            return true;
        return Decide(condition, InShelter != null && (bool)InShelter.Invoke(player, null), condition == ApronCondition.UnderRoofNotDungeon && Dungeon(player));
    }

    /// <summary>The rule on its own: does the bonus apply for this condition, shelter and dungeon state?</summary>
    internal static bool Decide(ApronCondition condition, bool sheltered, bool inDungeon) => condition switch
    {
        ApronCondition.Always => true,
        ApronCondition.UnderRoof => sheltered,
        _ => sheltered && !inDungeon,
    };

    internal static bool Dungeon(Player player)
    {
        if (InInterior != null)
            return (bool)InInterior.Invoke(player, null);
        return player.transform.position.y > 3000f;   // dungeons are built high above the world
    }
}
