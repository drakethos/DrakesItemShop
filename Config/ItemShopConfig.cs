using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs.API;
using DrakeModsLibs.Sync;

namespace DrakesItemShop.Config;

internal static class ItemShopConfig
{
    private const string SectionGeneral = "01 General";
    private const string SectionWard = "02 Ward defaults";
    private const string SectionPieces = "03 Pieces";

    private static readonly DrakeConfigSync ConfigSync = CustomizeLibsAPI.CreateConfigSync(
        ItemShopPlugin.GUID,
        ItemShopPlugin.ModName,
        ItemShopPlugin.Version);

    private static ConfigEntry<bool> _enabled = default!;
    private static ConfigEntry<bool> _lockSyncedConfig = default!;
    private static ConfigEntry<bool> _enableSellBack = default!;
    private static ConfigEntry<string> _defaultWardMode = default!;
    private static ConfigEntry<float> _maxInteractDistance = default!;
    private static ConfigEntry<int> _maxSignLines = default!;
    private static ConfigEntry<bool> _adminOnlyBuildPieces = default!;
    private static ConfigEntry<string> _displayCaseCost = default!;
    private static ConfigEntry<string> _smallDisplayCaseCost = default!;

    internal static bool Enabled { get; private set; } = true;
    internal static bool EnableSellBack { get; private set; } = true;
    internal static string DefaultWardMode { get; private set; } = "vanilla";
    internal static float MaxInteractDistance { get; private set; } = 5f;
    internal static int MaxSignLines { get; private set; } = 8;
    internal static bool AdminOnlyBuildPieces { get; private set; }
    internal const string DefaultDisplayCaseCost = "FineWood:10,JuteRed:2,Crystal:2";
    internal const string DefaultSmallDisplayCaseCost = "FineWood:5,JuteRed:1,Crystal:1";
    internal static string DisplayCaseCost { get; private set; } = DefaultDisplayCaseCost;
    internal static string SmallDisplayCaseCost { get; private set; } = DefaultSmallDisplayCaseCost;

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

    internal static void Bind(ConfigFile config, ManualLogSource log)
    {
        RackPoseDebug.Bind(config);
        Trade.TradeDebug.Bind(config);
        _enabled = config.Bind(SectionGeneral, "Enabled", true, "Enable DrakesItemShop features.");
        _lockSyncedConfig = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "LockSyncedConfig",
            true,
            "When true, only server admins can change synced shop settings.");

        ConfigSync.AddLockingConfigEntry(_lockSyncedConfig);

        _enableSellBack = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "EnableSellBack",
            true,
            "Allow selling items back for coins when catalog defines sellPrice.");

        _defaultWardMode = ConfigSync.BindSynced(
            config,
            SectionWard,
            SectionWard,
            "DefaultWardMode",
            "vanilla",
            "Default ward mode when shop JSON omits ward.mode: none, vanilla, betterwards, wardislove.");

        _maxInteractDistance = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "MaxInteractDistance",
            5f,
            "Max distance from shop anchor for buy/sell RPC validation.");

        _maxSignLines = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "MaxSignLines",
            8,
            "Maximum sign text lines when regenerating from catalog.");

        _adminOnlyBuildPieces = ConfigSync.BindSynced(
            config,
            SectionGeneral,
            SectionGeneral,
            "AdminOnlyBuildPieces",
            false,
            "When true, only server admins can place Drake Shop hammer pieces (Merchant Box, Sign, NPC Spawner).");

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

        RackPoseSettings.Bind(config, new PoseBinder(), "DrakesItemShop.rack-poses.yaml");

        ConfigSync.FinalizeBinding(log, 8 + Pieces.ItemRackPiece.Variants.Length + RackPoseSettings.SyncedEntryCount, () => _lockSyncedConfig.Value);

        Enabled = _enabled.Value;
        EnableSellBack = _enableSellBack.Value;
        DefaultWardMode = _defaultWardMode.Value ?? "vanilla";
        MaxInteractDistance = _maxInteractDistance.Value;
        MaxSignLines = _maxSignLines.Value;
        AdminOnlyBuildPieces = _adminOnlyBuildPieces.Value;
        DisplayCaseCost = _displayCaseCost.Value;
        SmallDisplayCaseCost = _smallDisplayCaseCost.Value;

        _enabled.SettingChanged += (_, _) => Enabled = _enabled.Value;
        _enableSellBack.SettingChanged += (_, _) => EnableSellBack = _enableSellBack.Value;
        _defaultWardMode.SettingChanged += (_, _) => DefaultWardMode = _defaultWardMode.Value ?? "vanilla";
        _maxInteractDistance.SettingChanged += (_, _) => MaxInteractDistance = _maxInteractDistance.Value;
        _maxSignLines.SettingChanged += (_, _) => MaxSignLines = _maxSignLines.Value;
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
