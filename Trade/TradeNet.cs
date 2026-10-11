using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Trade;

/// <summary>Trade settings (local to each player).</summary>
internal static class TradeSettings
{
    private static ConfigEntry<KeyboardShortcut> _key = default!;
    private static ConfigEntry<int> _seconds = default!;
    private static ConfigEntry<float> _range = default!;
    private static ConfigEntry<float> _cancelDistance = default!;

    /// <summary>Shift+T by default: plain T is the emote wheel.</summary>
    internal static KeyboardShortcut Shortcut => _key.Value;
    internal static int RequestSeconds => Math.Max(5, _seconds.Value);
    internal static float Range => Math.Max(2f, _range.Value);
    /// <summary>Walk further than this from the other player and the trade, request or prompt ends.</summary>
    internal static float CancelDistance => Math.Max(5f, _cancelDistance.Value);

    internal static void Bind(ConfigFile config)
    {
        _key = config.Bind("06 Trade", "Trade shortcut", new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift), "Press near other players to ask one of them to trade (plain T is the emote wheel).");
        _seconds = config.Bind("06 Trade", "Request seconds", 15, "How long a trade request waits for an answer.");
        _cancelDistance = config.Bind("06 Trade", "Cancel distance", 15f, "A trade or request ends when you or the other player get further apart than this many metres (at least 5).");
        _range = config.Bind("06 Trade", "Nearby range", 12f, "How far away (metres) a player can be and still be offered for trading.");
    }
}

/// <summary>The other player: messages go to their client over a routed RPC.</summary>
internal sealed class RemotePartner : ITradePartner
{
    private readonly long _peer;
    private readonly string _id;

    public string Name { get; }
    public long Peer => _peer;

    public RemotePartner(long peer, string name, string id)
    {
        _peer = peer;
        Name = name;
        _id = id;
    }

    public void SendOffer(IReadOnlyList<TradeEntry> mine, int version)
    {
        var pkg = TradeNet.Start(TradeNet.Msg.Offer, _id);
        pkg.Write(version);
        TradeItemCodec.Write(pkg, mine);
        TradeNet.Send(_peer, pkg);
    }

    public void SendAccept(int mineVersion, int theirsVersion)
    {
        var pkg = TradeNet.Start(TradeNet.Msg.Accept, _id);
        pkg.Write(mineVersion);
        pkg.Write(theirsVersion);
        TradeNet.Send(_peer, pkg);
    }

    public void SendCancel() => TradeNet.Send(_peer, TradeNet.Start(TradeNet.Msg.Cancel, _id));

    /// <summary>Their client does its own half of the swap.</summary>
    public void Receive(IReadOnlyList<TradeEntry> items)
    {
    }
}

/// <summary>
/// Player to player trade over the game's routed RPC: ask (with a 15 s answer window), answer, then each change of offer,
/// accept and cancel travels to the other client, which mirrors it into its own <see cref="TradeSession"/>.
/// </summary>
internal static class TradeNet
{
    internal enum Msg { Request = 1, Reply = 2, Offer = 3, Accept = 4, Cancel = 5 }

    private const string Rpc = "DrakesItemShop_Trade";

    private static ZRoutedRpc? _registeredFor;

    private static string? _pendingId;
    private static long _pendingPeer;
    private static string _pendingName = "";
    private static UkWindow? _waiting;

    private static string? _promptId;
    private static long _promptPeer;
    private static TradeSession? _active;
    private static string? _activeId;
    private static long _activePeer;

    /// <summary>True while a request is out, being asked, or a trade is on.</summary>
    internal static bool Busy => _pendingId != null || _promptId != null || (_active != null && !_active.Locked);

    /// <summary>Called every frame: a trade, request or prompt ends when the other player walks too far away (or leaves).</summary>
    internal static void WatchRange()
    {
        var me = Player.m_localPlayer;
        if (me == null)
            return;
        var limit = TradeSettings.CancelDistance;

        bool Away(long peer)
        {
            var other = Player.GetAllPlayers().FirstOrDefault(p => p != null && p.GetZDOID().UserID == peer);
            return other == null || Vector3.Distance(other.transform.position, me.transform.position) > limit;
        }

        if (_active is { Locked: false } && _active.Partner is RemotePartner partner && Away(partner.Peer))
        {
            _active.Cancel();
            Say("Trade cancelled: too far apart.");
        }
        if (_pendingId != null && Away(_pendingPeer))
            CancelPending(_pendingId, "Trade request cancelled: too far apart.");
        if (_promptId != null && Away(_promptPeer))
        {
            var id = _promptId;
            _promptId = null;
            TradePrompt.Dismiss();
            Reply(_promptPeer, id, false);
        }
    }

    internal static void EnsureRegistered()
    {
        if (ZRoutedRpc.instance == null || ZRoutedRpc.instance == _registeredFor)
            return;
        ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnMessage);
        _registeredFor = ZRoutedRpc.instance;
    }

    internal static ZPackage Start(Msg type, string id)
    {
        var pkg = new ZPackage();
        pkg.Write((int)type);
        pkg.Write(id);
        return pkg;
    }

    internal static void Send(long peer, ZPackage pkg)
    {
        EnsureRegistered();
        ZRoutedRpc.instance?.InvokeRoutedRPC(peer, Rpc, pkg);
    }

    internal static void Say(string text)
    {
        // The game's ShowMessage gained a parameter in 1.0 (the stubs we build against predate it): find whichever exists.
        if (MessageHud.instance == null)
            return;
        var type = typeof(MessageHud);
        var current = type.GetMethod("ShowMessage", new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite), typeof(bool), typeof(bool) });
        if (current != null)
        {
            current.Invoke(MessageHud.instance, new object?[] { MessageHud.MessageType.TopLeft, text, 0, null, false, false });
            return;
        }
        type.GetMethod("ShowMessage", new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite), typeof(bool) })
            ?.Invoke(MessageHud.instance, new object?[] { MessageHud.MessageType.TopLeft, text, 0, null, false });
    }

    // ---- asking ----

    /// <summary>Players near enough to trade with, nearest first.</summary>
    internal static List<Player> Nearby()
    {
        var me = Player.m_localPlayer;
        var found = new List<Player>();
        if (me == null)
            return found;
        foreach (var other in Player.GetAllPlayers())
            if (other != null && other != me && !other.IsDead()
                && Vector3.Distance(other.transform.position, me.transform.position) <= TradeSettings.Range)
                found.Add(other);
        found.Sort((a, b) => Vector3.Distance(a.transform.position, me.transform.position)
            .CompareTo(Vector3.Distance(b.transform.position, me.transform.position)));
        return found;
    }

    internal static void RequestTrade(Player target)
    {
        EnsureRegistered();
        if (Busy)
        {
            Say("Finish or cancel the current trade first.");
            return;
        }

        _pendingId = Guid.NewGuid().ToString("N").Substring(0, 8);
        _pendingPeer = target.GetZDOID().UserID;
        _pendingName = target.GetPlayerName();
        var pkg = Start(Msg.Request, _pendingId);
        pkg.Write(Player.m_localPlayer.GetPlayerName());
        Send(_pendingPeer, pkg);
        ShowWaiting(_pendingId);
    }

    private static void ShowWaiting(string id)
    {
        _waiting?.CloseSilent();
        var window = new UkWindow("drakes_trade_waiting", 380, UkWindow.DialogLayer, dim: false, draggable: false);
        _waiting = window;
        window.SetHeader("Trade request");
        var left = TradeSettings.RequestSeconds;
        var text = UkControls.Text($"Waiting for {_pendingName}... {left} s", 17, UkTheme.Text);
        window.Body.Add(text);
        var timer = window.Body.schedule.Execute(() =>
        {
            left--;
            text.text = $"Waiting for {_pendingName}... {left} s";
            if (left <= 0)
                CancelPending(id, "No answer.");
        }).Every(1000);
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Cancel", () => CancelPending(id, null), UkButtonKind.Secondary));
        window.ShowFooter(true);
        window.Closed = () =>
        {
            timer.Pause();
            CancelPending(id, null);
        };
        window.Open();
    }

    private static void CancelPending(string id, string? say)
    {
        if (_pendingId != id)
            return;
        var peer = _pendingPeer;
        _pendingId = null;
        _waiting?.CloseSilent();
        _waiting = null;
        Send(peer, Start(Msg.Cancel, id));
        if (say != null)
            Say(say);
    }

    // ---- messages ----

    private static void OnMessage(long sender, ZPackage pkg)
    {
        var type = (Msg)pkg.ReadInt();
        var id = pkg.ReadString();
        switch (type)
        {
            case Msg.Request:
                OnRequest(sender, id, pkg.ReadString());
                break;
            case Msg.Reply:
                OnReply(sender, id, pkg.ReadInt() == 1);
                break;
            case Msg.Offer:
                if (Mine(sender, id) is { } offerSession)
                {
                    var version = pkg.ReadInt();
                    offerSession.SetTheirs(TradeItemCodec.Read(pkg), version);
                }
                break;
            case Msg.Accept:
                if (Mine(sender, id) is { } acceptSession)
                    acceptSession.ReceiveAccept(pkg.ReadInt(), pkg.ReadInt());
                break;
            case Msg.Cancel:
                if (Mine(sender, id) is { } cancelSession)
                    cancelSession.PartnerCancels();
                else if (_promptId == id)
                {
                    _promptId = null;
                    TradePrompt.Dismiss();
                    Say("Trade request cancelled.");
                }
                break;
        }
    }

    private static TradeSession? Mine(long sender, string id) =>
        _active != null && _activeId == id && _activePeer == sender ? _active : null;

    private static void OnRequest(long sender, string id, string name)
    {
        if (Player.m_localPlayer == null || Busy)
        {
            Reply(sender, id, false);
            return;
        }

        _promptId = id;
        _promptPeer = sender;
        TradePrompt.Show(name, TradeSettings.RequestSeconds, () =>
        {
            if (_promptId != id)
                return;
            _promptId = null;
            Reply(sender, id, true);
            Begin(sender, name, id);
        }, () =>
        {
            if (_promptId != id)
                return;
            _promptId = null;
            Reply(sender, id, false);
        });
    }

    private static void Reply(long peer, string id, bool accepted)
    {
        var pkg = Start(Msg.Reply, id);
        pkg.Write(accepted ? 1 : 0);
        Send(peer, pkg);
    }

    private static void OnReply(long sender, string id, bool accepted)
    {
        if (_pendingId != id || _pendingPeer != sender)
            return;
        var name = _pendingName;
        _pendingId = null;
        _waiting?.CloseSilent();
        _waiting = null;
        if (accepted)
            Begin(sender, name, id);
        else
            Say($"{name} declined the trade.");
    }

    private static void Begin(long peer, string name, string id)
    {
        var session = new TradeSession(new RemotePartner(peer, name, id));
        _active = session;
        _activeId = id;
        _activePeer = peer;
        TradeWindow.Open(session);
    }
}

/// <summary>The trade key: press it near other players, pick one (or the only one) and they get the request.</summary>
internal static class TradeKey
{
    private static UkWindow? _picker;

    internal static void Update()
    {
        TradeNet.EnsureRegistered();
        TradeNet.WatchRange();
        Mail.MailboxSendWindow.PollKeys();
        if (!TradeSettings.Shortcut.IsDown() || !CanUse())
            return;

        var near = TradeNet.Nearby();
        if (near.Count == 0)
            return;
        if (near.Count == 1)
            TradeNet.RequestTrade(near[0]);
        else
            ShowPicker(near);
    }

    private static bool CanUse()
    {
        var player = Player.m_localPlayer;
        if (player == null || player.IsDead() || UkWindow.AnyOpen || TradeNet.Busy)
            return false;
        if (Chat.instance != null && Chat.instance.HasFocus())
            return false;
        if (Console.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || TextInput.IsVisible())
            return false;
        // The trade dummy answers its own T.
        var hover = player.GetHoverObject();
        return hover == null || hover.GetComponentInParent<TradeDummyBehaviour>() == null;
    }

    private static void ShowPicker(List<Player> players)
    {
        _picker?.CloseSilent();
        var window = new UkWindow("drakes_trade_picker", 380, UkWindow.DialogLayer, dim: false, draggable: false);
        _picker = window;
        window.SetHeader("Trade with", "Players near you");
        var me = Player.m_localPlayer;
        foreach (var other in players.Take(8))
        {
            var captured = other;
            var meters = Mathf.RoundToInt(Vector3.Distance(other.transform.position, me.transform.position));
            var button = UkControls.MakeButton($"{other.GetPlayerName()}   {meters} m", () =>
            {
                window.CloseSilent();
                TradeNet.RequestTrade(captured);
            }, UkButtonKind.Secondary);
            button.style.marginBottom = 8;
            window.Body.Add(button);
        }
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Cancel", () => window.Close(), UkButtonKind.Ghost));
        window.ShowFooter(true);
        window.Open();
    }
}
