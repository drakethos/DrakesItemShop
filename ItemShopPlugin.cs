using BepInEx;
using DrakesWorkshopLibs;

namespace DrakesItemShop;

[BepInPlugin(GUID, ModName, Version)]
[BepInDependency(CustomizeLibsPlugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
public partial class ItemShopPlugin : BaseUnityPlugin
{
    private void Awake() => Logger.LogInfo($"{ModName} {Version} loaded (Phase 1 stub).");
}
