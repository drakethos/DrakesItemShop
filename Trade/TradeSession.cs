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

/// <summary>The other side of a trade. A test dummy today; a real player once trade is networked.</summary>
internal interface ITradePartner
{
    string Name { get; }

    /// <summary>The partner takes what you gave (the test dummy just keeps a list).</summary>
    void Receive(IReadOnlyList<TradeEntry> items);
}

/// <summary>
/// One trade between the local player and a partner: what each side offers, who has accepted, and the swap itself.
/// Rules (WoW style): up to <see cref="MaxSlots"/> stacks per side; any change to either offer clears both Accepts;
/// when both have accepted the swap runs in one step, or not at all. UI-free, so a networked trade can drive the same object.
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
    public string Message { get; private set; } = "";

    /// <summary>Anything on screen may need redrawing.</summary>
    public event Action? Changed;

    public TradeSession(ITradePartner partner) => Partner = partner;

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
        Edited();
    }

    public void RemoveMine(TradeEntry entry)
    {
        if (Locked || !Mine.Remove(entry))
            return;
        Edited();
    }

    public void ChangeAmount(TradeEntry entry, int delta)
    {
        if (Locked)
            return;
        var amount = Math.Max(1, Math.Min(entry.Item.m_stack, entry.Amount + delta));
        if (amount == entry.Amount)
            return;
        entry.Amount = amount;
        Edited();
    }

    // ---- their side (the partner drives these) ----

    public void SetTheirs(IEnumerable<TradeEntry> items)
    {
        if (Locked)
            return;
        Theirs.Clear();
        Theirs.AddRange(items.Take(MaxSlots));
        Edited();
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
        MineAccepted = true;
        Message = TheirsAccepted ? "" : $"Waiting for {Partner.Name}.";
        Changed?.Invoke();
        TryComplete();
    }

    public void Cancel()
    {
        if (Locked)
            return;
        Cancelled = true;
        Message = "Trade cancelled.";
        Changed?.Invoke();
    }

    public bool Locked => Done || Cancelled;

    private void Edited()
    {
        MineAccepted = false;
        TheirsAccepted = false;
        Message = "Offer changed. Both need to accept again.";
        Changed?.Invoke();
    }

    // ---- the swap ----

    private void TryComplete()
    {
        if (!MineAccepted || !TheirsAccepted || Locked)
            return;

        var player = Player.m_localPlayer;
        var inventory = player != null ? player.GetInventory() : null;
        if (inventory == null)
            return;

        // Everything you offered must still be there, in full.
        foreach (var entry in Mine)
        {
            if (!inventory.ContainsItem(entry.Item) || entry.Item.m_stack < entry.Amount)
            {
                Fail("An item you offered is no longer in your inventory.");
                return;
            }
        }

        // Room for what you get, counting the slots you free.
        var freed = Mine.Count(e => e.Amount >= e.Item.m_stack);
        if (inventory.GetEmptySlots() + freed < Theirs.Count)
        {
            Fail("Not enough room in your inventory.");
            return;
        }

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

    private void Fail(string message)
    {
        MineAccepted = false;
        TheirsAccepted = false;
        Message = message;
        Changed?.Invoke();
    }
}
