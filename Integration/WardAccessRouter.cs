using DrakesItemShop.Config;
using DrakesItemShop.Schema;
using UnityEngine;

namespace DrakesItemShop.Integration;

internal enum WardMode
{
    None,
    Vanilla,
    BetterWards,
    WardIsLove,
}

internal static class WardAccessRouter
{
    public static WardMode ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return ParseMode(ItemShopConfig.DefaultWardMode);

        return mode.Trim().ToLowerInvariant() switch
        {
            "none" => WardMode.None,
            "vanilla" => WardMode.Vanilla,
            "betterwards" => WardMode.BetterWards,
            "wardislove" => WardMode.WardIsLove,
            _ => WardMode.Vanilla,
        };
    }

    public static bool CanAccess(Player player, Vector3 worldPos, WardDto? ward, ShopAction action)
    {
        if (player == null)
            return false;

        var mode = ParseMode(ward?.Mode);
        if (mode == WardMode.None)
            return true;

        if (mode == WardMode.BetterWards && BetterWardsAdapter.TryIsPermitted(player, worldPos))
            return true;

        if (mode == WardMode.WardIsLove && WardIsLoveAdapter.TryIsPermitted(player, worldPos))
            return true;

        return VanillaWardAdapter.IsPermitted(player, worldPos, ward?.RequirePermitted ?? true);
    }
}

internal enum ShopAction
{
    Open,
    Buy,
    Sell,
    Manage,
}
