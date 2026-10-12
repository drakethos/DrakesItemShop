using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace DrakesItemShop.Pricing;

/// <summary>
/// Your saved prices: a plain text file in the config folder, one line per item (<c>IronSword = 120</c>), editable by hand and kept on
/// this computer like LockSmith's groups. The Shop tab and the Price Ledger read and write it; prices travel on the stacks you stock,
/// so other players never need the file.
/// </summary>
internal static class Catalog
{
    private static readonly Dictionary<string, string> Prices = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _stamp;
    private static float _nextCheck;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "DrakesItemShop.catalog.txt");

    /// <summary>The saved price for an item (its prefab name), or "".</summary>
    public static string Get(ItemDrop.ItemData item)
    {
        Reload();
        return item.m_dropPrefab != null && Prices.TryGetValue(item.m_dropPrefab.name, out var price) ? price : "";
    }

    public static int Count
    {
        get
        {
            Reload();
            return Prices.Count;
        }
    }

    public static void Set(ItemDrop.ItemData item, string price)
    {
        if (item.m_dropPrefab == null)
            return;
        Reload();
        price = (price ?? "").Trim();
        if (price.Length == 0)
            Prices.Remove(item.m_dropPrefab.name);
        else
            Prices[item.m_dropPrefab.name] = price;
        Save();
    }

    private static void Reload()
    {
        if (Time.unscaledTime < _nextCheck && _stamp != default)
            return;
        _nextCheck = Time.unscaledTime + 2f;
        try
        {
            if (!File.Exists(FilePath))
            {
                if (_stamp == default)
                    _stamp = DateTime.MinValue.AddTicks(1);
                return;
            }
            var written = File.GetLastWriteTimeUtc(FilePath);
            if (written == _stamp)
                return;
            _stamp = written;
            Prices.Clear();
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                var equals = line.IndexOf('=');
                if (line.Length == 0 || line.StartsWith("#") || equals <= 0)
                    continue;
                Prices[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Shop] Could not read the catalog: {e.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            var lines = new List<string> { "# Saved prices, one per line: PrefabName = coins (or a short note). Edit freely." };
            foreach (var pair in Prices)
                lines.Add(pair.Key + " = " + pair.Value);
            File.WriteAllLines(FilePath, lines);
            _stamp = File.GetLastWriteTimeUtc(FilePath);
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Shop] Could not save the catalog: {e.Message}");
        }
    }
}
