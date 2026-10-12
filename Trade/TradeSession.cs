using System;
using System.Collections.Generic;
using System.Linq;

namespace DrakesItemShop.Trade;

/// <summary>One stack on offer: an item and how many of it.</summary>
internal sealed class TradeEntry
{
    public ItemDrop.ItemData Item;
    public int Amount;

    public TradeEntry(ItemDrop.ItemData item, int amount)
    {
        Item = item;
        Amount = amount;
    }
}

/// <summary>The other side of a trade: the test dummy, or another player over the network.</summary>
internal interface ITradePartner
{
    string Name { get; }

    /// <summary>You changed your offer (version counts your changes).</summary>
    void SendOffer(IReadOnlyList<TradeEntry> mine, int version);

    /// <summary>You accepted, seeing your offer at <paramref name="mineVersion"/> and theirs at <paramref name="theirsVersion"/>.</summary>
    void SendAccept(int mineVersion, int theirsVersion);

    void SendCancel();

    /// <summary>The trade went through: the partner takes what you gave (the dummy keeps a list; a player's own client does its own swap).</summary>
    void Receive(IReadOnlyList<TradeEntry> items);
}

/// <summary>
/// One trade between the local player and a partner: what each side offers, who has accepted, and the swap itself.
/// Rules (WoW style): up to <see cref="MaxSlots"/> stacks per side; any change to either offer clears both Accepts;
/// when both have accepted, each client runs the swap in one step. UI-free; the partner object carries the messages.
/// </summary>
internal sealed class TradeSession
{
    public const int MaxSlots = 6;

    public readonly ITradePartner Partner;
    public readonly List<TradeEntry> Mine = new();
    public readonly List<TradeEntry> Theirs = new();

    public bool MineAccepted { get; private set; }
    public bool TheirsAccepted { get; private set; }
    public bool Done { get; private set; }
    public bool Cancelled { get; private set; }
    /// <summary>Both accepted; the server is confirming. Nothing has left either inventory yet.</summary>
    public bool Settling { get; private set; }
    public string Message { get; private set; } = "";
    public int MineVersion { get; private set; }
    public int TheirsVersion { get; private set; }

    /// <summary>Anything on screen may need redrawing.</summary>
    public event Action? Changed;

    public TradeSession(ITradePartner partner) => Partner = partner;

    public bool Locked => Done || Cancelled || Settling;

    // ---- your side ----

    /// <summary>Click on an inventory item: put its whole stack on offer, or take it back off.</summary>
    public void ToggleMine(ItemDrop.ItemData item)
    {
        if (Locked)
            return;
        var existing = Mine.FirstOrDefault(e => e.Item == item);
        if (existing != null)
            Mine.Remove(existing);
        else if (Mine.Count < MaxSlots)
            Mine.Add(new TradeEntry(item, item.m_stack));
        else
        {
            Message = "Your offer is full.";
            Changed?.Invoke();
            return;
        }
        EditedMine();
    }

    public void RemoveMine(TradeEntry entry)
    {
        if (Locked || !Mine.Remove(entry))
            return;
        EditedMine();
    }

    public void ChangeAmount(TradeEntry entry, int delta)
    {
        if (Locked)
            return;
        var amount = Math.Max(1, Math.Min(entry.Item.m_stack, entry.Amount + delta));
        if (amount == entry.Amount)
            return;
        entry.Amount = amount;
        EditedMine();
    }

    private void EditedMine()
    {
        MineVersion++;
        ClearAccepts("Offer changed. Both need to accept again.");
        Partner.SendOffer(Mine, MineVersion);
    }

    // ---- their side ----

    /// <summary>The partner's offer changed (the test dummy calls this directly; a player's arrives with its version).</summary>
    public void SetTheirs(IEnumerable<TradeEntry> items, int? version = null)
    {
        if (Locked)
            return;
        Theirs.Clear();
        Theirs.AddRange(items.Take(MaxSlots));
        TheirsVersion = version ?? TheirsVersion + 1;
        ClearAccepts("Offer changed. Both need to accept again.");
    }

    public void PartnerAccepts()
    {
        if (Locked)
            return;
        TheirsAccepted = true;
        Message = $"{Partner.Name} accepted.";
        Changed?.Invoke();
        TryComplete();
    }

    /// <summary>A player's Accept: only counts if they were looking at the same two offers we are.</summary>
    public void ReceiveAccept(int theirMineVersion, int theirTheirsVersion)
    {
        if (theirMineVersion != TheirsVersion || theirTheirsVersion != MineVersion)
            return;
        PartnerAccepts();
    }

    public void PartnerCancels()
    {
        if (Locked)
            return;
        Cancelled = true;
        Message = $"{Partner.Name} cancelled the trade.";
        Changed?.Invoke();
    }

    // ---- accept / cancel ----

    public bool CanAccept => !Locked && (Mine.Count > 0 || Theirs.Count > 0);

    public void Accept()
    {
        if (!CanAccept)
            return;
        if (Problem() is { } problem)
        {
            Message = problem;
            Changed?.Invoke();
            return;
        }
        MineAccepted = true;
        Message = TheirsAccepted ? "" : $"Waiting for {Partner.Name}.";
        Partner.SendAccept(MineVersion, TheirsVersion);
        Changed?.Invoke();
        TryComplete();
    }

    public void Cancel()
    {
        if (Done || Cancelled)
            return;
        if (Settling)
        {
            // Closing while the server confirms: ask it to drop the trade. If it already committed, the swap still lands (both sides accepted).
            if (Partner is RemotePartner remote)
                TradeLedger.Abort(remote.Id);
            return;
        }
        Cancelled = true;
        Message = "Trade cancelled.";
        Partner.SendCancel();
        Changed?.Invoke();
    }

    private void ClearAccepts(string message)
    {
        MineAccepted = false;
        TheirsAccepted = false;
        Message = message;
        Changed?.Invoke();
    }

    // ---- the swap ----

    /// <summary>Why the swap can't run right now (null = it can).</summary>
    private string? Problem()
    {
        var inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
        if (inventory == null)
            return "No inventory.";

        // Everything you offered must still be there, in full.
        foreach (var entry in Mine)
            if (!inventory.ContainsItem(entry.Item) || entry.Item.m_stack < entry.Amount)
                return "An item you offered is no longer in your inventory.";

        // Room for what you get, counting the slots you free.
        var freed = Mine.Count(e => e.Amount >= e.Item.m_stack);
        if (inventory.GetEmptySlots() + freed < Theirs.Count)
            return "Not enough room in your inventory.";
        return null;
    }

    private void TryComplete()
    {
        if (!MineAccepted || !TheirsAccepted || Locked)
            return;

        if (Problem() is { } problem)
        {
            // Our accept was fine a moment ago: tell them it's off so neither side swaps alone.
            ClearAccepts(problem);
            Partner.SendOffer(Mine, ++MineVersion);
            return;
        }

        // With the server running this mod, it settles the trade; nothing is taken from anyone until it says so.
        if (Partner is RemotePartner remote && TradeLedger.Begin(this, remote, remote.Id))
        {
            Settling = true;
            Message = "Confirming with the server...";
            Changed?.Invoke();
            return;
        }

        SwapLocally();
    }

    private Action? _afterServerCheck;

    /// <summary>The ledger is still finding out whether the server runs this mod: continue when it knows.</summary>
    internal void WaitForServerCheck(Action next) => _afterServerCheck = next;

    internal void RunServerCheckCallback()
    {
        var next = _afterServerCheck;
        _afterServerCheck = null;
        next?.Invoke();
    }

    /// <summary>The server committed and this client applied it.</summary>
    internal void SettleDone()
    {
        Settling = false;
        Done = true;
        Message = "Trade complete.";
        Changed?.Invoke();
    }

    /// <summary>The server dropped the trade, or didn't answer: nothing was taken. Accepts are cleared so either side can try again or cancel.</summary>
    internal void SettleFailed(string message)
    {
        Settling = false;
        ClearAccepts(message);
    }

    /// <summary>The server doesn't run this mod: swap directly (best effort).</summary>
    internal void FallBackToDirect()
    {
        Settling = false;
        _afterServerCheck = null;
        SwapLocally();
    }

    private void SwapLocally()
    {
        var inventory = Player.m_localPlayer.GetInventory();
        var given = new List<TradeEntry>();
        foreach (var entry in Mine)
        {
            var copy = entry.Item.Clone();
            copy.m_stack = entry.Amount;
            given.Add(new TradeEntry(copy, entry.Amount));
            inventory.RemoveItem(entry.Item, entry.Amount);
        }
        foreach (var entry in Theirs)
        {
            var copy = entry.Item.Clone();
            copy.m_stack = entry.Amount;
            inventory.AddItem(copy);
        }
        Partner.Receive(given);

        Done = true;
        Message = "Trade complete.";
        Changed?.Invoke();
    }
}
