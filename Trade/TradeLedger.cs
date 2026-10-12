using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace DrakesItemShop.Trade;

/// <summary>
/// Settles a finished trade through the server so no item is lost or doubled when a connection drops.
/// <para>
/// Server mode (the server runs this mod): nothing leaves a player's inventory until the server says COMMIT. Each client sends its
/// offer (PREPARE) and keeps its items where they are. When the server holds both offers it writes the trade to a ledger file and
/// sends each player the other's items (COMMIT); the player removes their own, adds the new ones in one step, and answers DONE. If
/// anything fails before COMMIT the trade is dropped and nobody loses anything. After COMMIT the server keeps the trade in the
/// ledger until both have answered DONE, and sends it again (RESUME) to anyone who was offline or crashed, so it is delivered
/// exactly once (applied trade ids are remembered on each client).
/// </para>
/// <para>
/// Client mode (the server doesn't run this mod): the two clients swap directly, as before. Best effort only.
/// </para>
/// </summary>
internal static class TradeLedger
{
    internal enum Msg { Hello = 1, HelloAck = 2, Prepare = 3, Commit = 4, Done = 5, AbortRequest = 6, Aborted = 7, Resume = 8 }

    private const string Rpc = "DrakesItemShop_TradeLedger";
    private const float HelloSeconds = 3f;
    private const float CommitWaitSeconds = 15f;
    private const float PrepareWaitSeconds = 20f;

    private static ZRoutedRpc? _registeredFor;

    // ---- client state ----
    private static bool _serverChecked;
    private static bool _serverMode;
    private static float _helloSent = -1f;
    private static bool _resumed;
    private static readonly Dictionary<string, (TradeSession Session, float Since, bool Committed)> Waiting = new();
    private static readonly HashSet<string> Applied = new();
    private static bool _appliedLoaded;

    // ---- server state ----
    private sealed class Tx
    {
        public string Id = "";
        public long A;
        public long B;
        public byte[]? ItemsA;
        public byte[]? ItemsB;
        public bool Committed;
        public bool DoneA;
        public bool DoneB;
        public float Created;
    }

    private static readonly Dictionary<string, Tx> Txs = new();
    private static bool _ledgerLoaded;

    private static string LedgerPath => Path.Combine(Paths.ConfigPath, "DrakesItemShop.trade-ledger.txt");
    private static string AppliedPath => Path.Combine(Paths.ConfigPath, "DrakesItemShop.trades-applied.txt");

    internal static bool ServerMode => _serverMode;

    private static long ServerPeer() =>
        ZNet.instance == null ? 0L : ZNet.instance.IsServer() ? ZNet.GetUID() : ZNet.instance.GetServerPeer()?.m_uid ?? 0L;

    private static void Send(long peer, Msg type, string id, Action<ZPackage>? fill = null)
    {
        if (ZRoutedRpc.instance == null || peer == 0)
            return;
        var pkg = new ZPackage();
        pkg.Write((int)type);
        pkg.Write(id);
        fill?.Invoke(pkg);
        ZRoutedRpc.instance.InvokeRoutedRPC(peer, Rpc, pkg);
    }

    internal static void EnsureRegistered()
    {
        if (ZRoutedRpc.instance == null || ZRoutedRpc.instance == _registeredFor)
            return;
        ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnMessage);
        _registeredFor = ZRoutedRpc.instance;
        _serverChecked = false;
        _serverMode = false;
        _helloSent = -1f;
        _resumed = false;
        Waiting.Clear();
    }

    /// <summary>Every frame: server timeouts, client waits, and (once per connection) finding out whether the server runs this mod.</summary>
    internal static void Tick()
    {
        EnsureRegistered();
        if (ZNet.instance == null)
            return;
        if (ZNet.instance.IsServer())
            ServerTick();

        if (Player.m_localPlayer == null)
            return;
        if (!_serverChecked && _helloSent < 0f)
        {
            _helloSent = Time.unscaledTime;
            Send(ServerPeer(), Msg.Hello, "");
        }
        if (!_serverChecked && _helloSent >= 0f && Time.unscaledTime - _helloSent > HelloSeconds)
        {
            _serverChecked = true;          // no answer: the server doesn't run this mod, trades settle client to client
            foreach (var waiting in Waiting.ToList())
            {
                Waiting.Remove(waiting.Key);
                waiting.Value.Session.FallBackToDirect();
            }
        }
        if (_serverMode && !_resumed)
        {
            _resumed = true;
            Send(ServerPeer(), Msg.Resume, "");
        }

        foreach (var entry in Waiting.ToList())
        {
            var (session, since, committed) = entry.Value;
            if (!committed && Time.unscaledTime - since > CommitWaitSeconds)
            {
                Waiting.Remove(entry.Key);
                Send(ServerPeer(), Msg.AbortRequest, entry.Key);
                session.SettleFailed("The server didn't answer. Nothing was taken.");
            }
        }
    }

    // ================= client =================

    /// <summary>
    /// A trade both players accepted. True when it is being settled here (the session will be told how it ended); false when the server
    /// doesn't run this mod, so the caller swaps directly.
    /// </summary>
    internal static bool Begin(TradeSession session, RemotePartner partner, string id)
    {
        EnsureRegistered();
        if (_serverChecked && !_serverMode)
            return false;
        if (!_serverChecked)
        {
            // Still asking the server; settle as soon as we know (the window shows "Checking the server").
            Waiting[id] = (session, Time.unscaledTime, false);
            session.WaitForServerCheck(() => SendPrepare(session, partner, id));
            return true;
        }
        SendPrepare(session, partner, id);
        return true;
    }

    private static void SendPrepare(TradeSession session, RemotePartner partner, string id)
    {
        Waiting[id] = (session, Time.unscaledTime, false);
        var pkg = new ZPackage();
        TradeItemCodec.Write(pkg, session.Mine);
        var bytes = pkg.GetArray();
        Send(ServerPeer(), Msg.Prepare, id, p =>
        {
            p.Write(partner.Peer);
            p.Write(bytes);
        });
    }

    /// <summary>The player closed the window while it was settling: ask the server to drop the trade if it can.</summary>
    internal static void Abort(string id) => Send(ServerPeer(), Msg.AbortRequest, id);

    private static void OnClientMessage(long sender, Msg type, string id, ZPackage pkg)
    {
        switch (type)
        {
            case Msg.HelloAck:
                _serverChecked = true;
                _serverMode = true;
                foreach (var waiting in Waiting.Values.ToList())
                    waiting.Session.RunServerCheckCallback();
                break;
            case Msg.Commit:
                OnCommit(id, pkg.ReadByteArray(), pkg.ReadByteArray());
                break;
            case Msg.Aborted:
                if (Waiting.TryGetValue(id, out var entry) && !entry.Committed)
                {
                    Waiting.Remove(id);
                    entry.Session.SettleFailed("The trade was cancelled. Nothing was taken.");
                }
                break;
        }
    }

    private static void OnCommit(string id, byte[] giveBytes, byte[] getBytes)
    {
        LoadApplied();
        if (Applied.Contains(id))
        {
            Send(ServerPeer(), Msg.Done, id, p => p.Write(1));    // already applied: just confirm again
            return;
        }

        var player = Player.m_localPlayer;
        var inventory = player != null ? player.GetInventory() : null;
        var give = TradeItemCodec.Read(new ZPackage(giveBytes));
        var get = TradeItemCodec.Read(new ZPackage(getBytes));
        Waiting.TryGetValue(id, out var waiting);
        if (inventory == null || !TryApply(inventory, give, get))
        {
            // Rare (an offered item went missing, or no room): tell the server it didn't land so it stays in the ledger and is logged.
            Send(ServerPeer(), Msg.Done, id, p => p.Write(0));
            Waiting.Remove(id);
            waiting.Session?.SettleFailed("The trade couldn't be applied here. It was left with the server.");
            return;
        }

        Applied.Add(id);
        SaveApplied();
        Send(ServerPeer(), Msg.Done, id, p => p.Write(1));
        Waiting.Remove(id);
        waiting.Session?.SettleDone();
        if (waiting.Session == null && MessageHud.instance != null)
            TradeNet.Say("A trade finished while you were away: " + get.Count + " stack(s) received.");
    }

    /// <summary>Remove what we gave and add what we got, all in one step, or change nothing.</summary>
    private static bool TryApply(Inventory inventory, List<TradeEntry> give, List<TradeEntry> get)
    {
        var chosen = new List<(ItemDrop.ItemData Item, int Amount)>();
        foreach (var entry in give)
        {
            var found = inventory.GetAllItems().FirstOrDefault(i =>
                chosen.All(c => c.Item != i) && Same(i, entry.Item) && i.m_stack >= entry.Amount);
            if (found == null)
                return false;
            chosen.Add((found, entry.Amount));
        }
        var freed = chosen.Count(c => c.Amount >= c.Item.m_stack);
        if (inventory.GetEmptySlots() + freed < get.Count)
            return false;

        foreach (var (item, amount) in chosen)
            inventory.RemoveItem(item, amount);
        foreach (var entry in get)
        {
            var copy = entry.Item.Clone();
            copy.m_stack = entry.Amount;
            inventory.AddItem(copy);
        }
        return true;
    }

    private static bool Same(ItemDrop.ItemData a, ItemDrop.ItemData b)
    {
        if (a.m_dropPrefab == null || b.m_dropPrefab == null || a.m_dropPrefab.name != b.m_dropPrefab.name)
            return false;
        if (a.m_quality != b.m_quality || a.m_variant != b.m_variant || a.m_crafterID != b.m_crafterID)
            return false;
        var x = a.m_customData ?? new Dictionary<string, string>();
        var y = b.m_customData ?? new Dictionary<string, string>();
        return x.Count == y.Count && x.All(p => y.TryGetValue(p.Key, out var v) && v == p.Value);
    }

    private static void LoadApplied()
    {
        if (_appliedLoaded)
            return;
        _appliedLoaded = true;
        try
        {
            if (File.Exists(AppliedPath))
                foreach (var line in File.ReadAllLines(AppliedPath))
                    if (line.Length > 0)
                        Applied.Add(line.Trim());
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Trade] Could not read the applied trades list: {e.Message}");
        }
    }

    private static void SaveApplied()
    {
        try
        {
            File.WriteAllLines(AppliedPath, Applied.Skip(Math.Max(0, Applied.Count - 500)));
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Trade] Could not save the applied trades list: {e.Message}");
        }
    }

    // ================= server =================

    private static void OnServerMessage(long sender, Msg type, string id, ZPackage pkg)
    {
        LoadLedger();
        switch (type)
        {
            case Msg.Hello:
                Send(sender, Msg.HelloAck, "");
                break;
            case Msg.Prepare:
            {
                var partner = pkg.ReadLong();
                var bytes = pkg.ReadByteArray();
                if (bytes.Length > 64 * 1024 || sender == partner)
                    return;
                if (!Txs.TryGetValue(id, out var tx))
                    Txs[id] = tx = new Tx { Id = id, A = sender, B = partner, Created = Time.unscaledTime };
                if (tx.Committed)
                    return;
                if (sender == tx.A)
                    tx.ItemsA = bytes;
                else if (sender == tx.B && partner == tx.A)
                    tx.ItemsB = bytes;
                else
                    return;
                if (tx.ItemsA != null && tx.ItemsB != null)
                {
                    tx.Committed = true;
                    SaveLedger();                       // written before anyone is told, so a crash can't lose it
                    Deliver(tx);
                    ItemShopPlugin.LogSource?.LogInfo($"[Trade] {id} committed ({tx.A} <-> {tx.B}).");
                }
                break;
            }
            case Msg.Done:
            {
                var ok = pkg.ReadInt() == 1;
                if (!Txs.TryGetValue(id, out var tx))
                    return;
                if (!ok)
                {
                    ItemShopPlugin.LogSource?.LogWarning($"[Trade] {id}: peer {sender} could not apply it; kept in the ledger.");
                    return;
                }
                if (sender == tx.A)
                    tx.DoneA = true;
                else if (sender == tx.B)
                    tx.DoneB = true;
                if (tx.DoneA && tx.DoneB)
                    Txs.Remove(id);
                SaveLedger();
                break;
            }
            case Msg.AbortRequest:
                if (Txs.TryGetValue(id, out var open) && !open.Committed && (sender == open.A || sender == open.B))
                {
                    Txs.Remove(id);
                    Send(open.A, Msg.Aborted, id);
                    Send(open.B, Msg.Aborted, id);
                }
                break;
            case Msg.Resume:
                foreach (var tx in Txs.Values.Where(t => t.Committed).ToList())
                    if ((sender == tx.A && !tx.DoneA) || (sender == tx.B && !tx.DoneB))
                        Deliver(tx, only: sender);
                break;
        }
    }

    private static void Deliver(Tx tx, long only = 0)
    {
        if (tx.ItemsA == null || tx.ItemsB == null)
            return;
        if (!tx.DoneA && (only == 0 || only == tx.A))
            Send(tx.A, Msg.Commit, tx.Id, p => { p.Write(tx.ItemsA); p.Write(tx.ItemsB); });   // give A's, get B's
        if (!tx.DoneB && (only == 0 || only == tx.B))
            Send(tx.B, Msg.Commit, tx.Id, p => { p.Write(tx.ItemsB); p.Write(tx.ItemsA); });
    }

    private static float _nextSweep;

    private static void ServerTick()
    {
        LoadLedger();
        if (Time.unscaledTime < _nextSweep)
            return;
        _nextSweep = Time.unscaledTime + 1f;
        foreach (var tx in Txs.Values.Where(t => !t.Committed && Time.unscaledTime - t.Created > PrepareWaitSeconds).ToList())
        {
            Txs.Remove(tx.Id);                  // only one side prepared in time: drop it, nobody lost anything
            Send(tx.A, Msg.Aborted, tx.Id);
            Send(tx.B, Msg.Aborted, tx.Id);
        }
    }

    private static void LoadLedger()
    {
        if (_ledgerLoaded)
            return;
        _ledgerLoaded = true;
        try
        {
            if (!File.Exists(LedgerPath))
                return;
            foreach (var line in File.ReadAllLines(LedgerPath))
            {
                var f = line.Split('|');
                if (f.Length != 7)
                    continue;
                Txs[f[0]] = new Tx
                {
                    Id = f[0], A = long.Parse(f[1]), B = long.Parse(f[2]), DoneA = f[3] == "1", DoneB = f[4] == "1",
                    ItemsA = Convert.FromBase64String(f[5]), ItemsB = Convert.FromBase64String(f[6]), Committed = true,
                };
            }
            ItemShopPlugin.LogSource?.LogInfo($"[Trade] Ledger loaded: {Txs.Count} trade(s) still to deliver.");
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogError($"[Trade] Could not read the ledger: {e.Message}");
        }
    }

    private static void SaveLedger()
    {
        try
        {
            File.WriteAllLines(LedgerPath, Txs.Values.Where(t => t.Committed && t.ItemsA != null && t.ItemsB != null).Select(t =>
                string.Join("|", t.Id, t.A, t.B, t.DoneA ? "1" : "0", t.DoneB ? "1" : "0", Convert.ToBase64String(t.ItemsA!), Convert.ToBase64String(t.ItemsB!))));
        }
        catch (Exception e)
        {
            ItemShopPlugin.LogSource?.LogError($"[Trade] Could not write the ledger: {e.Message}");
        }
    }

    // ================= routing =================

    private static void OnMessage(long sender, ZPackage pkg)
    {
        var type = (Msg)pkg.ReadInt();
        var id = pkg.ReadString();
        if (type is Msg.HelloAck or Msg.Commit or Msg.Aborted)
            OnClientMessage(sender, type, id, pkg);
        else if (ZNet.instance != null && ZNet.instance.IsServer())
            OnServerMessage(sender, type, id, pkg);
    }
}
