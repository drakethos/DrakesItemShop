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
    internal static string DisplayCaseCost { get; private set; } = "Wood:10";
    internal static string SmallDisplayCaseCost { get; private set; } = "Wood:5";

    /// <summary>Either display case cost changed (locally or synced from the server).</summary>
    internal static event System.Action? DisplayCaseCostChanged;

    internal static void Bind(ConfigFile config, ManualLogSource log)
    {
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
            "Wood:10",
            "Build cost of the Display Case: Item:amount pairs separated by commas (prefab names, e.g. Wood:10, Crystal:2).");

        _smallDisplayCaseCost = ConfigSync.BindSynced(
            config,
            SectionPieces,
            SectionPieces,
            "SmallDisplayCaseCost",
            "Wood:5",
            "Build cost of the Small Display Case: Item:amount pairs separated by commas (prefab names, e.g. Wood:5).");

        ConfigSync.FinalizeBinding(log, 8, () => _lockSyncedConfig.Value);

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
