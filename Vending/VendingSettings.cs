using BepInEx.Configuration;

namespace DrakesItemShop.Vending;

/// <summary>Server settings for the vending machines: what unlocks each one and what a locked one says.</summary>
internal static class VendingSettings
{
    private const string Section = "10 Vending";

    private static ConfigEntry<string>? _outOfOrder;
    private static ConfigEntry<string>? _haldor;
    private static ConfigEntry<string>? _hildir;
    private static ConfigEntry<string>? _bogWitch;
    private static ConfigEntry<string>? _soldOut;
    private static ConfigEntry<LightMode>? _bayLight;

    internal enum LightMode { Night, Always, Off }

    /// <summary>Synced entries <see cref="Bind"/> registers (for the mod's expected-count check).</summary>
    internal const int SyncedEntryCount = 6;

    internal static string OutOfOrderMessage => _outOfOrder?.Value ?? "This seems to be out of order";
    internal static string HaldorKeys => _haldor?.Value ?? "";
    internal static string HildirKeys => _hildir?.Value ?? "";
    internal static string BogWitchKeys => _bogWitch?.Value ?? "";
    internal static string SoldOutText => _soldOut?.Value ?? "Sold out";
    internal static LightMode BayLight => _bayLight?.Value ?? LightMode.Night;

    internal static void Bind(ConfigFile config)
    {
        _outOfOrder = Config.ItemShopConfig.Synced(config, Section, "Out of order message", "This seems to be out of order",
            "What a vending machine says (and shows on hover) while its trader isn't unlocked yet.");
        _haldor = Config.ItemShopConfig.Synced(config, Section, "Haldor unlock keys", "defeated_eikthyr",
            "World keys Haldor's vending machine needs before it works (comma-separated, all of them). Empty = always works. Default: Eikthyr defeated (the Black Forest).");
        _hildir = Config.ItemShopConfig.Synced(config, Section, "Hildir unlock keys", "",
            "World keys Hildir's vending machine needs before it works (comma-separated, all of them). Empty = always works (the Meadows).");
        _bogWitch = Config.ItemShopConfig.Synced(config, Section, "Bog Witch unlock keys", "defeated_gdking",
            "World keys the Bog Witch's vending machine needs before it works (comma-separated, all of them). Empty = always works. Default: The Elder defeated (the Swamp).");
        _soldOut = Config.ItemShopConfig.Synced(config, Section, "Sold out card", "Sold out",
            "What the card says in a slot whose item isn't unlocked yet (the item itself stays hidden).");
        _bayLight = Config.ItemShopConfig.Synced(config, Section, "Bay light", LightMode.Night,
            "The light behind the glass: Night (only at night; by day it adds glare), Always, or Off. A purchase still flashes it.");
    }

    /// <summary>True when every key in the comma-separated list is set in the world (an empty list is always met).</summary>
    internal static bool KeysMet(string keys)
    {
        if (ZoneSystem.instance == null)
            return false;
        foreach (var raw in keys.Split(','))
        {
            var key = raw.Trim();
            if (key.Length > 0 && !ZoneSystem.instance.GetGlobalKey(key))
                return false;
        }
        return true;
    }
}
