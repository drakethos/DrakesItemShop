using System.Collections;
using BepInEx;
using BepInEx.Logging;
using DrakesItemShop.Config;
using DrakesItemShop.Networking;
using DrakesItemShop.Patches;
using DrakesItemShop.Runtime;
using DrakesWorkshopLibs;
using HarmonyLib;
using Jotunn;

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
        ItemShopRuntime.Initialize(Logger);
        _harmony.PatchAll();
        LedgerUsePatch.TryApply(_harmony, Logger);
        Logger.LogInfo($"{ModName} {Version} loaded.");
    }

    private void Start()
    {
        ItemShopRuntime.RegisterAssets();
        StartCoroutine(RegisterShopRpcWhenReady());
    }

    private static IEnumerator RegisterShopRpcWhenReady()
    {
        for (int i = 0; i < 600 && ZRoutedRpc.instance == null; i++)
            yield return null;

        ShopRpc.EnsureRegistered();
    }
}
