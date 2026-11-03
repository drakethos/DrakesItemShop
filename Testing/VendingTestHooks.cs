using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrakesItemShop.Vending;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Testing;

/// <summary>
/// Test hooks for the vending machines (suite DrakesItemShop-Vending). They use the machine's own code paths: the store
/// window it opens, its gating and its shelves. The machine is the tagged piece (args "target"), else the nearest one.
/// </summary>
internal static class VendingTestHooks
{
    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static string S(Dictionary<string, object?> a, string key, string fallback = "") =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    static VendingBehaviour Machine(GameObject? target)
    {
        var m = target != null ? target.GetComponentInParent<VendingBehaviour>() ?? target.GetComponentInChildren<VendingBehaviour>() : null;
        if (m != null)
            return m;
        return UnityEngine.Object.FindObjectsOfType<VendingBehaviour>()
                   .OrderBy(v => Vector3.Distance(v.transform.position, P.transform.position))
                   .FirstOrDefault() ?? throw new InvalidOperationException("no vending machine nearby");
    }

    /// <summary>"service=on;glow=lit;light=off;items=12;cards=3;lockedShown=0;available=12;vending=no" (see VendingBehaviour.TestState).</summary>
    [TestHook("itemshop.vending.state")]
    static string State(GameObject? target = null) => Machine(target).TestState();

    /// <summary>The hover text the crosshair shows on the machine.</summary>
    [TestHook("itemshop.vending.hover")]
    static string Hover(GameObject? target = null) => Machine(target).GetHoverText();

    /// <summary>Press E on the machine (its real use code). True when the store window is open afterwards.</summary>
    [TestHook("itemshop.vending.use")]
    static bool Use(GameObject? target = null)
    {
        Machine(target).Interact(P, false, false);
        return StoreOpen();
    }

    /// <summary>The store window's panel is up (StoreGui.IsVisible only turns true a frame after it opens).</summary>
    [TestHook("itemshop.vending.storeOpen")]
    static bool StoreOpen() => StoreGui.instance != null && StoreGui.instance.m_rootPanel.activeSelf;

    [TestHook("itemshop.vending.closeStore")]
    static bool CloseStore()
    {
        StoreGui.instance?.Hide();
        return true;
    }

    /// <summary>
    /// Buy from the store window through its own buy code (select the item, Buy): prefab, or the first item for sale.
    /// Opens the store first if it isn't. Returns "ok" when the coins went down by exactly the price and the item's stack
    /// came into the bag, else what went wrong.
    /// </summary>
    [TestHook("itemshop.vending.buy")]
    static string Buy(Dictionary<string, object?> args, GameObject? target = null)
    {
        var machine = Machine(target);
        if (!StoreOpen() && !Use(target))
            return "the store did not open";
        var prefab = S(args, "prefab");
        var item = machine.TestStore()?.GetAvailableItems().FirstOrDefault(i => i.m_prefab != null &&
                       (prefab.Length == 0 || i.m_prefab.name.Equals(prefab, StringComparison.OrdinalIgnoreCase)));
        if (item == null)
            return $"{(prefab.Length == 0 ? "nothing" : prefab)} is not for sale here";
        var inventory = P.GetInventory();
        var name = item.m_prefab.m_itemData.m_shared.m_name;
        var coinsBefore = inventory.CountItems("$item_coins");
        var before = inventory.CountItems(name);
        AccessTools.Field(typeof(StoreGui), "m_selectedItem").SetValue(StoreGui.instance, item);
        AccessTools.Method(typeof(StoreGui), "BuySelectedItem").Invoke(StoreGui.instance, null);
        var paid = coinsBefore - inventory.CountItems("$item_coins");
        var got = inventory.CountItems(name) - before;
        if (paid != item.m_price)
            return $"paid {paid} coins for {item.m_prefab.name}, price {item.m_price}";
        if (got < Math.Min(item.m_stack, item.m_prefab.m_itemData.m_shared.m_maxStackSize))
            return $"got {got} {item.m_prefab.name}, expected {item.m_stack}";
        return "ok";
    }

    /// <summary>The price of prefab at this machine's store (coins), or -1 when it isn't sold.</summary>
    [TestHook("itemshop.vending.price")]
    static int Price(Dictionary<string, object?> args, GameObject? target = null)
    {
        var prefab = S(args, "prefab");
        var item = Machine(target).TestStore()?.m_items.FirstOrDefault(i => i.m_prefab != null && i.m_prefab.name.Equals(prefab, StringComparison.OrdinalIgnoreCase));
        return item?.m_price ?? -1;
    }

    /// <summary>The first item the store sells you right now (its prefab name), or "" when nothing.</summary>
    [TestHook("itemshop.vending.firstForSale")]
    static string FirstForSale(GameObject? target = null) =>
        Machine(target).TestStore()?.GetAvailableItems().FirstOrDefault(i => i.m_prefab != null)?.m_prefab.name ?? "";

    /// <summary>Set (on = true, default) or remove a world key, as killing a boss or the console's setkey/removekey would.</summary>
    [TestHook("itemshop.vending.setKey")]
    static bool SetKey(Dictionary<string, object?> args)
    {
        var key = S(args, "key");
        if (key.Length == 0 || ZoneSystem.instance == null)
            return false;
        if (!args.TryGetValue("on", out var on) || on is not false)
            ZoneSystem.instance.SetGlobalKey(key);
        else
            ZoneSystem.instance.RemoveGlobalKey(key);
        return true;
    }

    /// <summary>Act as a player who isn't an admin (the single-player host always is one) until called with on = false.</summary>
    [TestHook("itemshop.vending.notAdmin")]
    static bool NotAdmin(Dictionary<string, object?> args)
    {
        Patches.PieceBuildGatePatch.TestNotAdmin = !args.TryGetValue("on", out var on) || on is not false;
        return Patches.PieceBuildGatePatch.TestNotAdmin;
    }
}
