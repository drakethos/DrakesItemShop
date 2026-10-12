using BepInEx;
using BepInEx.Logging;
using DrakesItemShop.Config;
using DrakesItemShop.Localization;
using DrakesItemShop.Patches;
using DrakesItemShop.Pieces;
using DrakeModsLibs;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;

namespace DrakesItemShop;

[BepInPlugin(GUID, ModName, Version)]
[BepInDependency(Main.ModGuid)]
[BepInDependency(CustomizeLibsPlugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
public partial class ItemShopPlugin : BaseUnityPlugin
{
    public static ItemShopPlugin? Instance { get; private set; }
    internal static ManualLogSource LogSource { get; private set; } = null!;

    private readonly Harmony _harmony = new("drakesmod.DrakesItemShop");

    private void Awake()
    {
        Instance = this;
        LogSource = Logger;
        ItemShopConfig.Bind(Config, Logger);
        ItemShopLocalization.Init();
        _harmony.PatchAll();
        PieceBuildGatePatch.TryApply(_harmony, Logger);
        PrefabManager.OnVanillaPrefabsAvailable += RegisterWhenPrefabsReady;
        Mail.MailboxMenuTab.Register();
        Pricing.ShopMenuTab.Register();
        Pieces.Layout.DisplayLayoutTab.Register();
        Logger.LogInfo($"{ModName} {Version} loaded.");
    }

    private void Update()
    {
        Trade.TradeKey.Update();
        Pricing.PriceLedger.Poll();
        Pricing.ShopServer.Tick();
        Pricing.ShopState.TickPendingSaves();
    }

    private static void RegisterWhenPrefabsReady()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= RegisterWhenPrefabsReady;
        ShopPieceRegistrar.RegisterAll();
    }
}
