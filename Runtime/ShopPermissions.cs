using DrakesItemShop.Schema;
using Jotunn.Managers;

namespace DrakesItemShop.Runtime;

internal static class ShopPermissions
{
    public static bool CanOpen(Player player, ShopDefinitionDto shop, long ownerUid)
    {
        var rule = shop.Permissions?.Open?.Trim().ToLowerInvariant() ?? "permitted";
        return rule switch
        {
            "anyone" => true,
            "admin" => IsAdmin(player),
            "owner" => IsOwner(player, ownerUid) || IsAdmin(player),
            "permitted" => true,
            _ => true,
        };
    }

    public static bool CanManage(Player player, ShopDefinitionDto shop, long ownerUid)
    {
        var rule = shop.Permissions?.Manage?.Trim().ToLowerInvariant() ?? "ownerOrAdmin";
        return rule switch
        {
            "admin" => IsAdmin(player),
            "owner" => IsOwner(player, ownerUid),
            "ownerOrAdmin" => IsOwner(player, ownerUid) || IsAdmin(player),
            "anyone" => true,
            _ => IsOwner(player, ownerUid) || IsAdmin(player),
        };
    }

    private static bool IsAdmin(Player player)
    {
        if (player == null)
            return false;

        if (ZNet.instance != null && ZNet.instance.IsServer() && player == Player.m_localPlayer)
            return true;

        return SynchronizationManager.Instance?.PlayerIsAdmin == true &&
               player == Player.m_localPlayer;
    }

    private static bool IsOwner(Player player, long ownerUid)
    {
        if (ownerUid == 0)
            return false;
        return player.GetPlayerID() == ownerUid;
    }
}
