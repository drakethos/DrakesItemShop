using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Pricing;

/// <summary>A price on an item stack, stored with the stack so it follows it from chest to chest.</summary>
internal static class ItemPrice
{
    private const string Key = "drake_price";

    /// <summary>The price as typed: a number of coins, or a note ("2 iron bars", "make an offer").</summary>
    public static string Raw(ItemDrop.ItemData? item) =>
        item?.m_customData != null && item.m_customData.TryGetValue(Key, out var value) ? value : "";

    /// <summary>Coins per deal when the price is a number, else 0 (a note, or no price). One deal gives <see cref="ItemLot.Of"/> of this stack.</summary>
    public static int Coins(ItemDrop.ItemData? item) =>
        int.TryParse(Raw(item), out var coins) && coins > 0 ? coins : 0;

    public static void Set(ItemDrop.ItemData item, string text)
    {
        item.m_customData ??= new Dictionary<string, string>();
        text = (text ?? "").Trim();
        if (text.Length == 0)
            item.m_customData.Remove(Key);
        else
            item.m_customData[Key] = text;
    }

    /// <summary>e.g. "40 coins for 1 Iron" or "40 coins each".</summary>
    public static string CoinSummary(ItemDrop.ItemData? item)
    {
        var coins = Coins(item);
        if (coins <= 0)
            return "";
        var lot = ItemLot.Of(item);
        var name = item != null
            ? DrakeModsLibs.Display.ItemDisplayService.GetDisplayNameForUi(item, localize: true)
            : "item";
        return lot == 1 ? $"{coins} coins each" : $"{coins} coins for {lot} {name}";
    }
}

/// <summary>
/// How many of this stack the buyer receives per deal (default 1). Stored as <c>drake_lot</c>.
/// Example: 40 coins for 1 Iron → lot 1; 100 coins for 5 Iron → lot 5.
/// </summary>
internal static class ItemLot
{
    private const string Key = "drake_lot";

    public static int Of(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null || !item.m_customData.TryGetValue(Key, out var raw))
            return 1;
        return int.TryParse(raw, out var n) && n > 0 ? n : 1;
    }

    public static void Set(ItemDrop.ItemData item, int lot)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (lot <= 1)
            item.m_customData.Remove(Key);
        else
            item.m_customData[Key] = lot.ToString();
    }

    public static void Clear(ItemDrop.ItemData item) => item.m_customData?.Remove(Key);
}

/// <summary>What makes a display piece a shop: an open switch, a till of coins from sales, and a short sales log. All on the piece.</summary>
internal static class ShopState
{
    private static readonly int OpenKey = "drake_shop_open".GetStableHashCode();
    private static readonly int TillKey = "drake_shop_till".GetStableHashCode();
    private static readonly int SeqKey = "drake_shop_seq".GetStableHashCode();
    private static readonly int LogKey = "drake_shop_log".GetStableHashCode();     // "buyer|item|quantity|coins|utc seconds" lines, newest last
    private const int LogLines = 8;

    private static readonly System.Reflection.MethodInfo? CheckAccess =
        AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) });

    private static readonly System.Reflection.MethodInfo? ContainerSave =
        AccessTools.Method(typeof(Container), "Save");

    private static readonly System.Reflection.FieldInfo? AllAreas =
        AccessTools.Field(typeof(PrivateArea), "m_allAreas");

    private static readonly System.Reflection.MethodInfo? WardCheckAccess =
        AccessTools.Method(typeof(PrivateArea), "CheckAccess",
            new[] { typeof(Vector3), typeof(float), typeof(bool), typeof(bool) });

    // Live Valheim keeps these protected; publicized stubs compile but throw in game.
    private static readonly System.Reflection.MethodInfo? AreaIsEnabled =
        AccessTools.Method(typeof(PrivateArea), "IsEnabled");
    private static readonly System.Reflection.MethodInfo? AreaIsInside =
        AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });
    private static readonly System.Reflection.MethodInfo? AreaIsPermitted =
        AccessTools.Method(typeof(PrivateArea), "IsPermitted", new[] { typeof(long) });

    /// <summary>Chests waiting to become ZDO owner so a price edit can be written for real.</summary>
    private static readonly Dictionary<Container, float> PendingSaves = new();

    /// <summary>Display cases and racks can be shops.</summary>
    public static bool IsPiece(Container? container) =>
        container != null && (container.GetComponent<DisplayCaseBehaviour>() != null || container.GetComponent<ItemRackBehaviour>() != null
            || container.GetComponent<Mats.MatBehaviour>() != null);

    private static ZNetView? View(Container container) => container.GetComponent<ZNetView>();

    public static bool IsOpen(Container container) =>
        View(container) is { } nview && nview.IsValid() && nview.GetZDO().GetInt(OpenKey) == 1;

    public static void SetOpen(Container container, bool open)
    {
        if (View(container) is not { } nview || !nview.IsValid())
            return;
        nview.ClaimOwnership();
        nview.GetZDO().Set(OpenKey, open ? 1 : 0);
        // Flush prices onto the ZDO whenever the shop opens, so the server buy-check sees them.
        if (open)
            NotifyChanged(container);
        ItemShopPlugin.LogSource?.LogInfo($"[Shop] {container.name}: open for business = {open}");
    }

    /// <summary>Counts sales, so the builder's client can say "Bjorn bought...".</summary>
    public static int Seq(Container container) =>
        View(container) is { } nview && nview.IsValid() ? nview.GetZDO().GetInt(SeqKey) : 0;

    public static int Till(Container container) =>
        View(container) is { } nview && nview.IsValid() ? nview.GetZDO().GetInt(TillKey) : 0;

    /// <summary>Whether this player may open the chest itself (wards and LockSmith included). The game keeps CheckAccess protected, hence reflection.</summary>
    public static bool CanOpen(Container container, Player player) =>
        CheckAccess == null || (bool)CheckAccess.Invoke(container, new object[] { player.GetPlayerID() });

    private static bool AreaEnabled(PrivateArea area) =>
        AreaIsEnabled != null && (bool)AreaIsEnabled.Invoke(area, null);

    private static bool AreaContains(PrivateArea area, Vector3 pos) =>
        AreaIsInside != null && (bool)AreaIsInside.Invoke(area, new object[] { pos, 0f });

    private static bool AreaPermits(PrivateArea area, long playerId) =>
        AreaIsPermitted != null && (bool)AreaIsPermitted.Invoke(area, new object[] { playerId });

    /// <summary>True when the piece stands inside an enabled ward. Unknown (no area list) counts as protected, so nobody is nagged.</summary>
    public static bool InWard(Container container)
    {
        if (AllAreas?.GetValue(null) is not System.Collections.IEnumerable areas)
            return true;
        var pos = container.transform.position;
        foreach (var entry in areas)
        {
            if (entry is not PrivateArea area || area == null)
                continue;
            try
            {
                if (AreaEnabled(area) && AreaContains(area, pos))
                    return true;
            }
            catch
            {
                // Prefab stubs / mid-destroy: skip this ward.
            }
        }
        return false;
    }

    /// <summary>True when the local player is allowed through the ward covering this piece (or there is no ward in the way).</summary>
    public static bool WardAllows(Container container, Player player)
    {
        if (player == null)
            return false;
        // Builder of the piece always passes the ward gate for shop settings.
        if (IsBuilder(container, player))
            return true;
        if (WardCheckAccess != null && player == Player.m_localPlayer)
            return (bool)WardCheckAccess.Invoke(null, new object[] { container.transform.position, 0f, false, false });
        if (AllAreas?.GetValue(null) is not System.Collections.IEnumerable areas)
            return true;
        var pos = container.transform.position;
        var id = player.GetPlayerID();
        var sawWard = false;
        foreach (var entry in areas)
        {
            if (entry is not PrivateArea area || area == null)
                continue;
            try
            {
                if (!AreaEnabled(area) || !AreaContains(area, pos))
                    continue;
                sawWard = true;
                if (AreaPermits(area, id) || area.GetComponent<Piece>() is { } piece && piece.GetCreator() == id)
                    return true;
            }
            catch
            {
                // Skip broken ward entries.
            }
        }
        // No covering ward: unrestricted for this check (CanManage still requires builder outside a ward).
        return !sawWard;
    }

    public static bool IsBuilder(Container container, Player player) =>
        container.GetComponent<Piece>() is { } piece && piece.GetCreator() == player.GetPlayerID();

    /// <summary>
    /// Who runs the shop: its builder, or someone the piece's ward lets in (and who can open the chest). Outside a ward an open chest
    /// is open to anyone, so being able to open it is NOT enough: only the builder. LockSmith's guest-manage flag is intentionally
    /// not used here — the Shop tab decides with this, so a ward owner / piece builder is never locked out of pricing.
    /// </summary>
    public static bool CanManage(Container container, Player player) =>
        player != null && (IsBuilder(container, player) || (InWard(container) && WardAllows(container, player) && CanOpen(container, player)));

    /// <summary>Coins from a sale go into the till (a number on the piece), collected from the Shop tab.</summary>
    public static void AddSale(Container container, string buyer, string item, int quantity, int coins)
    {
        if (View(container) is not { } nview || !nview.IsValid())
            return;
        nview.ClaimOwnership();
        var zdo = nview.GetZDO();
        zdo.Set(TillKey, zdo.GetInt(TillKey) + coins);
        zdo.Set(SeqKey, zdo.GetInt(SeqKey) + 1);
        var lines = new List<string>(zdo.GetString(LogKey).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        lines.Add(string.Join("|", buyer.Replace('|', ' '), item.Replace('|', ' '), quantity, coins, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        if (lines.Count > LogLines)
            lines.RemoveRange(0, lines.Count - LogLines);
        zdo.Set(LogKey, string.Join("\n", lines));
    }

    /// <summary>Move the till by <paramref name="coins"/> (negative to pay buy offers from it); never below zero.</summary>
    public static void AddTill(Container container, int coins)
    {
        if (coins == 0 || View(container) is not { } nview || !nview.IsValid())
            return;
        nview.ClaimOwnership();
        var zdo = nview.GetZDO();
        zdo.Set(TillKey, Math.Max(0, zdo.GetInt(TillKey) + coins));
    }

    /// <summary>Take the till's coins (returns how many; the till is empty after).</summary>
    public static int TakeTill(Container container)
    {
        if (View(container) is not { } nview || !nview.IsValid())
            return 0;
        nview.ClaimOwnership();
        var coins = nview.GetZDO().GetInt(TillKey);
        nview.GetZDO().Set(TillKey, 0);
        return coins;
    }

    public static List<(string Buyer, string Item, int Quantity, int Coins, TimeSpan Ago)> Sales(Container container)
    {
        var result = new List<(string, string, int, int, TimeSpan)>();
        if (View(container) is not { } nview || !nview.IsValid())
            return result;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var line in nview.GetZDO().GetString(LogKey).Split('\n'))
        {
            var f = line.Split('|');
            if (f.Length == 5 && int.TryParse(f[2], out var qty) && int.TryParse(f[3], out var coins) && long.TryParse(f[4], out var at))
                result.Insert(0, (f[0], f[1], qty, coins, TimeSpan.FromSeconds(Math.Max(0, now - at))));
        }
        return result;
    }

    /// <summary>
    /// Write the chest's live inventory (prices included) to its ZDO. Must be ZDO owner — a Save from a non-owner is discarded on
    /// the next sync and wipes the price you just typed. If ownership is still transferring, retry for a couple of seconds.
    /// </summary>
    public static void NotifyChanged(Container container)
    {
        if (container == null)
            return;
        var nview = View(container);
        if (nview == null || !nview.IsValid())
            return;

        if (!nview.IsOwner())
            nview.ClaimOwnership();

        if (TrySave(container, nview))
            return;

        PendingSaves[container] = Time.realtimeSinceStartup + 2.5f;
        ItemShopPlugin.LogSource?.LogInfo($"[Shop] Waiting for ZDO ownership before saving prices on {container.name}");
    }

    /// <summary>Retry price saves that were queued until the piece became ZDO owner.</summary>
    public static void TickPendingSaves()
    {
        if (PendingSaves.Count == 0)
            return;
        foreach (var entry in PendingSaves.ToList())
        {
            var container = entry.Key;
            if (container == null)
            {
                PendingSaves.Remove(entry.Key);
                continue;
            }
            var nview = View(container);
            if (nview != null && nview.IsValid() && TrySave(container, nview))
            {
                PendingSaves.Remove(container);
                ItemShopPlugin.LogSource?.LogInfo($"[Shop] Saved prices on {container.name} after claiming ownership");
                continue;
            }
            if (Time.realtimeSinceStartup > entry.Value)
            {
                PendingSaves.Remove(container);
                ItemShopPlugin.LogSource?.LogWarning($"[Shop] Could not claim ownership to save prices on {container.name}");
            }
        }
    }

    private static bool TrySave(Container container, ZNetView nview)
    {
        if (!nview.IsOwner())
            return false;
        // Ensure every stack has a dictionary — Inventory.Save writes m_customData.Count with no null check.
        foreach (var item in container.GetInventory().GetAllItems())
            item.m_customData ??= new Dictionary<string, string>();
        ContainerSave?.Invoke(container, null);
        return true;
    }

    /// <summary>Put a price on the live inventory copy of this stack (chests reload and replace item objects).</summary>
    public static void SetPrice(Container container, ItemDrop.ItemData item, string text)
    {
        var live = Live(container, item);
        var before = ItemPrice.Raw(live);
        ItemPrice.Set(live, text);
        if (before == ItemPrice.Raw(live))
            return;
        NotifyChanged(container);
        var nview = View(container);
        ItemShopPlugin.LogSource?.LogInfo(
            $"[Shop] Price '{ItemPrice.Raw(live)}' on {(live.m_dropPrefab != null ? live.m_dropPrefab.name : "?")} at {live.m_gridPos} (zdo owner={(nview != null && nview.IsValid() && nview.IsOwner())})");
    }

    /// <summary>
    /// The inventory's own copy of a displayed item. A chest reloads its inventory from its saved copy now and then, which replaces the
    /// item objects; what a display tag remembers can be the old one, without the latest price.
    /// </summary>
    public static ItemDrop.ItemData Live(Container container, ItemDrop.ItemData item) =>
        container.GetInventory().GetItemAt(item.m_gridPos.x, item.m_gridPos.y) is { } found
        && found.m_dropPrefab == item.m_dropPrefab ? found : item;

    public static int CountCoins(Inventory inventory) =>
        inventory.GetAllItems().Where(IsCoin).Sum(i => i.m_stack);

    /// <summary>True when this stack is Valheim coins (buying coins with coins is nonsense).</summary>
    public static bool IsCoinsStack(ItemDrop.ItemData? item) =>
        item?.m_dropPrefab != null && item.m_dropPrefab.name == "Coins";

    private static bool IsCoin(ItemDrop.ItemData item) => IsCoinsStack(item);

    /// <summary>Take <paramref name="coins"/> from the player's coin stacks. False (nothing taken) if they don't have enough.</summary>
    public static bool TakeCoins(Inventory inventory, int coins)
    {
        if (CountCoins(inventory) < coins)
            return false;
        var left = coins;
        foreach (var stack in inventory.GetAllItems().Where(IsCoin).ToList())
        {
            var take = Math.Min(stack.m_stack, left);
            inventory.RemoveItem(stack, take);
            left -= take;
            if (left == 0)
                break;
        }
        return true;
    }

    /// <summary>Put coins into the player's inventory (several stacks if needed). Returns false if there is no room.</summary>
    public static bool GiveCoins(Inventory inventory, int coins)
    {
        var left = coins;
        while (left > 0)
        {
            var entry = Trade.TradeItemCodec.Make("Coins", left);
            if (entry == null)
                return false;
            var take = entry.Item.m_stack;
            if (!inventory.AddItem(entry.Item))
                return false;
            left -= take;
        }
        return true;
    }
}
