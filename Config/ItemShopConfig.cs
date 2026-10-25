using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs.API;
using DrakeModsLibs.Sync;
using UnityEngine;

namespace DrakesItemShop.Config;

internal static class ItemShopConfig
{
    private const string SectionGeneral = "01 General";
    private const string SectionPieces = "03 Pieces";
    private const string SectionBuyCards = "04 Buy Cards";

    /// <summary>Where a case's buy cards sit along its length when there are fewer cards than slots.</summary>
    internal enum CardAlign { Center, Left, Right }

    private static readonly DrakeConfigSync ConfigSync = CustomizeLibsAPI.CreateConfigSync(
        ItemShopPlugin.GUID,
        ItemShopPlugin.ModName,
        ItemShopPlugin.Version);

    private static ConfigEntry<bool> _enabled = default!;
    private static ConfigEntry<bool> _lockSyncedConfig = default!;
    private static ConfigEntry<bool> _adminOnlyBuildPieces = default!;
    private static ConfigEntry<string> _displayCaseCost = default!;
    private static ConfigEntry<string> _smallDisplayCaseCost = default!;

    internal static bool Enabled { get; private set; } = true;
    internal static bool AdminOnlyBuildPieces { get; private set; }
    internal const string DefaultDisplayCaseCost = "FineWood:10,JuteRed:2,Crystal:2";
    internal const string DefaultSmallDisplayCaseCost = "FineWood:5,JuteRed:1,Crystal:1";
    internal static string DisplayCaseCost { get; private set; } = DefaultDisplayCaseCost;
    internal static string SmallDisplayCaseCost { get; private set; } = DefaultSmallDisplayCaseCost;

    private static ConfigEntry<float> _buyCardScale = default!;
    private static ConfigEntry<CardAlign> _buyCardAlign = default!;
    /// <summary>Buy card size (1 = the 20 cm model); shrunk further if the case's slots are narrower.</summary>
    internal static float BuyCardScale => _buyCardScale?.Value ?? 2.3f;
    internal static CardAlign BuyCardAlign => _buyCardAlign?.Value ?? CardAlign.Center;

    private static readonly System.Collections.Generic.Dictionary<string, ConfigEntry<string>> _rackCosts = new();

    /// <summary>Weapon rack pose settings ride the shop's server sync: the host's values are everyone's.</summary>
    private sealed class PoseBinder : IRackPoseBinder
    {
        public ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T defaultValue, string description, AcceptableValueBase? range) =>
            ConfigSync.BindSynced(config, section, section, key, defaultValue, description, range);

        public bool CanEdit => ConfigSync.IsSourceOfTruth;
    }

    /// <summary>A rack's build cost (config key per rack, see <see cref="Pieces.ItemRackPiece.Variants"/>).</summary>
    internal static string RackCost(Pieces.ItemRackPiece.Variant rack) =>
        _rackCosts.TryGetValue(rack.ConfigKey, out var entry) ? entry.Value : rack.DefaultCost;

    /// <summary>Any rack's cost changed (locally or synced from the server).</summary>
    internal static event System.Action? RackCostChanged;

    /// <summary>Either display case cost changed (locally or synced from the server).</summary>
    internal static event System.Action? DisplayCaseCostChanged;

    /// <summary>A setting that follows the server: the host's value is everyone's.</summary>
    internal static ConfigEntry<T> Synced<T>(ConfigFile config, string section, string key, T defaultValue, string description, AcceptableValueBase? range = null) =>
        ConfigSync.BindSynced(config, section, section, key, defaultValue, description, range);

    internal static void Bind(ConfigFile config, ManualLogSource log)
    {
        RackPoseDebug.Bind(config);
        Trade.TradeSettings.Bind(config);
        Pricing.PriceLedgerSettings.Bind(config);
        Mail.MailboxSettings.Bind(config);
        Storefront.StorefrontSettings.Bind(config);
        Apron.ApronSettings.Bind(config);
        Mail.PostWallPiece.Bind(config);
        _enabled = config.Bind(SectionGeneral, "Enabled", true, "Enable DrakesItemShop features.");
        _lockSyncedConfig = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "LockSyncedConfig",
            true,
            "When true, only server admins can change synced shop settings.");

        ConfigSync.AddLockingConfigEntry(_lockSyncedConfig);

        _adminOnlyBuildPieces = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "AdminOnlyBuildPieces",
            false,
            "When true, only server admins can place Drake Shop hammer pieces .");

        _displayCaseCost = ConfigSync.BindSynced(
            config,
            SectionPieces,
            SectionPieces,
            "DisplayCaseCost",
            DefaultDisplayCaseCost,
            "Build cost of the Display Case: Item:amount pairs separated by commas (prefab names, e.g. Wood:10, Crystal:2).");

        _smallDisplayCaseCost = ConfigSync.BindSynced(
            config,
            SectionPieces,
            SectionPieces,
            "SmallDisplayCaseCost",
            DefaultSmallDisplayCaseCost,
            "Build cost of the Small Display Case: Item:amount pairs separated by commas (prefab names, e.g. Wood:5).");

        // 0.1.0 shipped placeholder costs (Wood:10 / Wood:5); move configs still on them to the real defaults.
        if (_displayCaseCost.Value == "Wood:10")
            _displayCaseCost.Value = DefaultDisplayCaseCost;
        if (_smallDisplayCaseCost.Value == "Wood:5")
            _smallDisplayCaseCost.Value = DefaultSmallDisplayCaseCost;

        foreach (var rack in Pieces.ItemRackPiece.Variants)
        {
            var entry = ConfigSync.BindSynced(
                config,
                SectionPieces,
                SectionPieces,
                rack.ConfigKey,
                rack.DefaultCost,
                $"Build cost of the {rack.Name.Replace("Drake_", "")}: Item:amount pairs separated by commas (prefab names).");
            entry.SettingChanged += (_, _) => RackCostChanged?.Invoke();
            _rackCosts[rack.ConfigKey] = entry;
        }

        _buyCardScale = config.Bind(SectionBuyCards, "CardScale", 2.3f,
            new ConfigDescription("Size of the buy cards standing on display cases (1 = 20 cm wide). Capped so cards never overlap.",
                new AcceptableValueRange<float>(0.5f, 3f)));
        // The first build defaulted to 1.6 (too small to read); move untouched configs to the new default.
        if (Mathf.Approximately(_buyCardScale.Value, 1.6f))
            _buyCardScale.Value = 2.3f;
        _buyCardAlign = config.Bind(SectionBuyCards, "CardAlign", CardAlign.Center,
            "Where buy cards sit along a case when there are fewer cards than slots (Left = the stocker's left).");

        RackPoseSettings.Bind(config, new PoseBinder(), "DrakesItemShop.rack-poses.yaml");

        ConfigSync.FinalizeBinding(log, 4 + Pieces.ItemRackPiece.Variants.Length + RackPoseSettings.SyncedEntryCount, () => _lockSyncedConfig.Value);

        Enabled = _enabled.Value;
        AdminOnlyBuildPieces = _adminOnlyBuildPieces.Value;
        DisplayCaseCost = _displayCaseCost.Value;
        SmallDisplayCaseCost = _smallDisplayCaseCost.Value;

        _enabled.SettingChanged += (_, _) => Enabled = _enabled.Value;
        _adminOnlyBuildPieces.SettingChanged += (_, _) => AdminOnlyBuildPieces = _adminOnlyBuildPieces.Value;
        _displayCaseCost.SettingChanged += (_, _) =>
        {
            DisplayCaseCost = _displayCaseCost.Value;
            DisplayCaseCostChanged?.Invoke();
        };
        _smallDisplayCaseCost.SettingChanged += (_, _) =>
        {
            SmallDisplayCaseCost = _smallDisplayCaseCost.Value;
            DisplayCaseCostChanged?.Invoke();
        };
    }
}
