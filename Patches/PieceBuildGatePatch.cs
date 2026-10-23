using DrakeModsLibs.API;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using DrakesItemShop.Config;
using DrakesItemShop.Pieces;
using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>When AdminOnlyBuildPieces is enabled, only admins may place DrakeShop hammer pieces.</summary>
internal static class PieceBuildGatePatch
{
    public static void TryApply(Harmony harmony, ManualLogSource log)
    {
        // Live Valheim: PlacePiece(Piece, Vector3, Quaternion, bool, ...) returns void; older builds differ. Any overload works:
        // the prefix reads the piece from the first argument (or the ghost) and skips the call to refuse.
        var target = FindPlaceMethod(typeof(Player));

        if (target == null)
        {
            log.LogWarning("Piece build gate: PlacePiece not found — AdminOnlyBuildPieces will not block placement until a matching patch is added.");
            return;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(typeof(PieceBuildGatePatch), nameof(Prefix)));
        log.LogInfo($"Piece build gate patched: {target.DeclaringType?.Name}.{target.Name}");
    }

    private static bool IsAdmin(Player player)
    {
        if (ZNet.instance != null && ZNet.instance.IsServer() && player == Player.m_localPlayer)
            return true;
        return Jotunn.Managers.SynchronizationManager.Instance?.PlayerIsAdmin == true && player == Player.m_localPlayer;
    }

    public static bool Prefix(Player __instance, object[] __args)
    {
        if (!ItemShopConfig.AdminOnlyBuildPieces || __instance == null)
            return true;

        var pieceName = __args.Length > 0 && __args[0] is Piece piece && piece != null ? piece.name : "";
        if (pieceName.Length == 0 && !TryGetGhostPieceName(__instance, out pieceName))
            return true;

        if (!ShopPieceRegistrar.IsDrakeShopPiece(pieceName))
            return true;

        if (IsAdmin(__instance))
            return true;

        CustomizeLibsAPI.ShowHudMessage(__instance, MessageHud.MessageType.Center, "Only admins can build Drake Shop pieces.");
        return false;
    }

    private static bool TryGetGhostPieceName(Player player, out string pieceName)
    {
        pieceName = "";
        var ghost = AccessTools.Field(typeof(Player), "m_placementGhost")?.GetValue(player) as GameObject;
        if (ghost == null)
            return false;

        pieceName = ghost.name;
        return true;
    }

    private static MethodInfo? FindPlaceMethod(System.Type type)
    {
        foreach (var method in AccessTools.GetDeclaredMethods(type))
        {
            if (method.Name == "PlacePiece")
                return method;
        }

        return null;
    }
}
