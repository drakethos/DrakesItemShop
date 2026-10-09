using System.IO;
using BepInEx.Logging;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Runtime;

/// <summary>
/// Resolves vanilla prefab names via Jotunn's PrefabManager cache.
/// Must be called from OnVanillaPrefabsAvailable — Jotunn populates its cache there
/// even though ZNetScene.instance and ObjectDB.instance are not yet assigned at that moment.
/// </summary>
internal static class ShopVanillaPrefabResolver
{
    private static bool _resolved;
    private static string _merchantBox = "piece_chest";
    private static string _shopSign = "sign";
    private static string _npcSpawner = "piece_bed02";
    private static string _ledger = "LeatherScraps";

    public static string MerchantBox => _merchantBox;
    public static string ShopSign => _shopSign;
    public static string NpcSpawner => _npcSpawner;
    public static string Ledger => _ledger;

    public static void ResolveAll(ManualLogSource log)
    {
        if (_resolved)
            return;

        // #region agent log
        DbgLog("FIX", "ResolveAll using Jotunn PrefabManager cache", "\"source\":\"PrefabManager\"");
        // #endregion

        _merchantBox = ResolvePiecePrefab(log, "MerchantBox",
            "piece_chest", "piece_chest_wood", "piece_chest_blackmetal");

        _shopSign = ResolvePiecePrefab(log, "ShopSign",
            "sign", "sign_notext");

        _npcSpawner = ResolvePiecePrefab(log, "NpcSpawner",
            "piece_bed02", "piece_bed");

        _ledger = ResolveItemPrefab(log, "Ledger",
            "LeatherScraps", "Coins", "Ruby");

        _resolved = true;
    }

    private static string ResolvePiecePrefab(ManualLogSource log, string label, params string[] candidates)
    {
        foreach (var name in candidates)
        {
            if (!TryGetPiecePrefab(name, out _))
                continue;

            log.LogInfo($"ItemShop clone [{label}]: using piece prefab '{name}'");
            // #region agent log
            DbgLog("FIX", $"resolved piece [{label}]={name}", $"\"label\":\"{label}\",\"name\":\"{name}\"");
            // #endregion
            return name;
        }

        log.LogError($"ItemShop clone [{label}]: no valid piece prefab found. Tried: {string.Join(", ", candidates)}");
        // #region agent log
        DbgLog("FIX", $"FAILED piece [{label}]", $"\"label\":\"{label}\",\"tried\":\"{string.Join(",", candidates)}\"");
        // #endregion
        return candidates[0];
    }

    private static string ResolveItemPrefab(ManualLogSource log, string label, params string[] candidates)
    {
        foreach (var name in candidates)
        {
            if (!TryGetItemPrefab(name, out _))
                continue;

            log.LogInfo($"ItemShop clone [{label}]: using item prefab '{name}'");
            // #region agent log
            DbgLog("FIX", $"resolved item [{label}]={name}", $"\"label\":\"{label}\",\"name\":\"{name}\"");
            // #endregion
            return name;
        }

        log.LogError($"ItemShop clone [{label}]: no valid item prefab found. Tried: {string.Join(", ", candidates)}");
        // #region agent log
        DbgLog("FIX", $"FAILED item [{label}]", $"\"label\":\"{label}\",\"tried\":\"{string.Join(",", candidates)}\"");
        // #endregion
        return candidates[0];
    }

    private static bool TryGetPiecePrefab(string name, out GameObject prefab)
    {
        prefab = null!;
        try
        {
            prefab = PrefabManager.Instance.GetPrefab(name);
        }
        catch (System.Exception ex)
        {
            // #region agent log
            DbgLog("FIX", $"GetPrefab({name}) threw", $"\"name\":\"{name}\",\"ex\":\"{ex.Message}\"");
            // #endregion
            return false;
        }

        bool hasPiece = prefab != null && prefab.GetComponent<Piece>() != null;
        // #region agent log
        DbgLog("FIX", $"piece check {name}: found={prefab != null} hasPiece={hasPiece}",
            $"\"name\":\"{name}\",\"found\":{(prefab != null).ToString().ToLower()},\"hasPiece\":{hasPiece.ToString().ToLower()}");
        // #endregion
        return hasPiece;
    }

    private static bool TryGetItemPrefab(string name, out GameObject prefab)
    {
        prefab = null!;
        try
        {
            prefab = PrefabManager.Instance.GetPrefab(name);
        }
        catch (System.Exception ex)
        {
            // #region agent log
            DbgLog("FIX", $"GetPrefab({name}) threw", $"\"name\":\"{name}\",\"ex\":\"{ex.Message}\"");
            // #endregion
            return false;
        }

        bool hasItemDrop = prefab != null && prefab.GetComponent<ItemDrop>() != null;
        // #region agent log
        DbgLog("FIX", $"item check {name}: found={prefab != null} hasItemDrop={hasItemDrop}",
            $"\"name\":\"{name}\",\"found\":{(prefab != null).ToString().ToLower()},\"hasItemDrop\":{hasItemDrop.ToString().ToLower()}");
        // #endregion
        return hasItemDrop;
    }

    // #region agent log
    private static void DbgLog(string hyp, string msg, string data = "")
    {
        var ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var line = $"{{\"sessionId\":\"cd0be0\",\"hypothesisId\":\"{hyp}\",\"location\":\"ShopVanillaPrefabResolver.cs\",\"message\":\"{msg}\",\"data\":{{{data}}},\"timestamp\":{ts}}}";
        File.AppendAllText(@"c:\Modding\Vahleim\Mod\DrakesWorkshop\debug-cd0be0.log", line + "\n");
    }
    // #endregion
}
