using HarmonyLib;

namespace DrakesItemShop.Runtime;

/// <summary>
/// Game members that are private in Valheim 1.0 but public in the publicized compile refs: touching them directly
/// compiles, then throws in game (FieldAccessException / MethodAccessException).
/// </summary>
internal static class GameAccess
{
    private static readonly AccessTools.FieldRef<Container, ZNetView> ContainerView =
        AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

    private static readonly AccessTools.FieldRef<Container, Inventory> ContainerInventory =
        AccessTools.FieldRefAccess<Container, Inventory>("m_inventory");

    private static readonly System.Func<PrivateArea, long, bool>? PrivateAreaIsPermitted =
        AccessTools.Method(typeof(PrivateArea), "IsPermitted", new[] { typeof(long) }) is { } method
            ? AccessTools.MethodDelegate<System.Func<PrivateArea, long, bool>>(method)
            : null;

    private static readonly System.Action<Trader>? TraderStart =
        AccessTools.Method(typeof(Trader), "Start") is { } start
            ? AccessTools.MethodDelegate<System.Action<Trader>>(start, virtualCall: false)
            : null;

    public static void SetView(Container container, ZNetView view) => ContainerView(container) = view;

    public static void SetInventory(Container container, Inventory inventory) => ContainerInventory(container) = inventory;

    /// <summary>The ward's own permission check; true (no restriction) if this Valheim doesn't have it.</summary>
    public static bool IsPermitted(PrivateArea area, long playerId) => PrivateAreaIsPermitted?.Invoke(area, playerId) ?? true;

    /// <summary>Trader's own Start (private in 1.0), for subclasses that add to it.</summary>
    public static void StartTrader(Trader trader) => TraderStart?.Invoke(trader);
}
