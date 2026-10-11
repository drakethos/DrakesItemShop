using System;
using System.Linq;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Mail;

/// <summary>The "Mail" tab in LockSmith's Lock menu for mailboxes: the sign, whether the slot is open, and who sent what.</summary>
internal static class MailboxMenuTab
{
    internal static void Register() =>
        PieceMenuTabs.Register(new PieceMenuTab("drakes.mail", "Mail", c => c.GetComponent<MailboxBehaviour>() != null, Build));

    internal static void BuildInto(PieceMenuTabContext ctx) => Build(ctx);

    private static void Build(PieceMenuTabContext ctx)
    {
        var mail = ctx.Piece.GetComponent<MailboxBehaviour>();
        if (mail == null)
            return;
        var body = ctx.Body;
        if (mail.m_singleDoor)
            BuildOwnerSection(ctx, mail);

        // Where the back door stands in the world: the point of a mailbox is that only the owner can open it.
        var single = mail.m_singleDoor;
        var protectedBox = single || mail.IsProtected;
        var banner = UkControls.Card(protectedBox ? UkTheme.PanelDeep : UkTheme.DangerBg, border: true).Column().Pad(12);
        banner.style.marginBottom = 14;
        banner.Add(UkControls.Text(single ? "Who can open this box" : protectedBox ? "Back door protected" : "Back door not protected", 16,
            protectedBox ? UkTheme.Ok : UkTheme.DangerText, heading: true, wrap: false));
        banner.Add(UkControls.Text(single
            ? "Choose the owner below and give the box a label. Only the owner (and people they add) can open the door; everyone else can only post mail through the slot."
            : protectedBox
            ? "This box is inside a ward, so only people the ward and LockSmith allow can open the back."
            : "This box is not inside a ward. Anyone can walk round and open the back to take everything sent here, which defeats the mailbox. Build it inside a ward, or lock it in the Piece tab.",
            14, UkTheme.TextBody));
        body.Add(banner);

        var sign = new UkField(single ? "Label on the plate" : "Sign", multiline: false);
        sign.SetMax(24);
        sign.SetValue(mail.SignRaw);
        sign.Changed = text => mail.SetSign(text);
        sign.Root.style.marginBottom = 4;
        body.Add(sign.Root);
        body.Add(UkControls.Text(single
            ? "A display name for the plate, like Bjorn - Smith. Empty means unassigned: nobody can post until it has one."
            : "Shows on the hover text and the send window. Leave it empty for your name.", 13, UkTheme.TextMuted));

        var allow = new UkField("Who can send", multiline: false);
        allow.SetValue(mail.AllowText);
        allow.Changed = text => mail.AllowText = text;
        allow.Root.style.marginTop = 14;
        body.Add(allow.Root);
        body.Add(UkControls.Text("Player names, separated by commas. Empty means anyone. You can always send.", 13, UkTheme.TextMuted));

        var filter = new UkField("Only takes", multiline: false);
        filter.SetValue(mail.FilterText);
        filter.Changed = text => mail.FilterText = text;
        filter.Root.style.marginTop = 14;
        body.Add(filter.Root);
        body.Add(UkControls.Text("Item names, separated by commas, like Coins, Iron. Empty means anything.", 13, UkTheme.TextMuted));

        var accept = new UkSwitch("Open for mail", "Off closes the send slot: nobody can send until you turn it back on.");
        accept.SetValue(mail.AcceptOn, notify: false);
        accept.Changed = on => mail.AcceptOn = on;
        accept.Root.style.marginTop = 14;
        body.Add(accept.Root);

        var notify = new UkSwitch("Tell me when mail arrives", "A message on your screen when someone sends something and you're nearby.");
        notify.SetValue(mail.NotifyOn, notify: false);
        notify.Changed = on => mail.NotifyOn = on;
        notify.Root.style.marginTop = 8;
        body.Add(notify.Root);

        if (!ctx.CanManage)
        {
            sign.Input.SetEnabled(false);
            allow.Input.SetEnabled(false);
            filter.Input.SetEnabled(false);
            accept.Root.SetEnabled(false);
            notify.Root.SetEnabled(false);
        }

        // Recent deliveries, newest first. The items stay behind the door.
        var title = UkControls.Text("RECENT MAIL", 12, UkTheme.TextMuted, wrap: false);
        title.style.marginTop = 16;
        title.style.marginBottom = 6;
        body.Add(title);
        var recent = mail.RecentMail();
        var card = UkControls.Card().Column().Pad(12);
        if (recent.Count == 0)
        {
            card.Add(UkControls.Text("Nothing yet.", 14, UkTheme.TextMuted));
        }
        else
        {
            foreach (var (from, stacks, ago, note) in recent)
            {
                var line = new VisualElement().Row();
                line.Add(UkControls.Text(from, 15, UkTheme.Text, wrap: false));
                line.Add(UkControls.Spacer());
                line.Add(UkControls.Text($"{stacks} stack{(stacks == 1 ? "" : "s")}, {Ago(ago)}", 14, UkTheme.TextMuted, wrap: false));
                card.Add(line);
                if (!string.IsNullOrEmpty(note))
                {
                    var noteLine = UkControls.Text("\u201c" + note + "\u201d", 14, UkTheme.TextBody);
                    noteLine.style.marginBottom = 6;
                    card.Add(noteLine);
                }
            }
        }
        body.Add(card);

        var capacity = mail.Box != null ? mail.Box.GetInventory() : null;
        if (capacity != null)
        {
            var used = capacity.GetAllItems().Count;
            var slots = capacity.GetWidth() * capacity.GetHeight();
            var count = UkControls.Text($"{used} of {slots} slots used behind the door", 14, UkTheme.TextMuted, wrap: false);
            count.style.marginTop = 10;
            body.Add(count);
        }

        if (ctx.CanManage && recent.Count > 0)
        {
            var clear = UkControls.MakeButton("Clear the list", () =>
            {
                mail.ClearLog();
                ctx.Rebuild();
            }, UkButtonKind.Ghost);
            clear.style.marginTop = 10;
            clear.style.alignSelf = Align.FlexStart;
            body.Add(clear);
        }
    }

    /// <summary>PO boxes: who owns it, who else may open it, and whether the builder (postmaster) can.</summary>
    private static void BuildOwnerSection(PieceMenuTabContext ctx, MailboxBehaviour mail)
    {
        var body = ctx.Body;
        var card = UkControls.Card().Column().Pad(12);
        card.style.marginBottom = 14;
        card.Add(UkControls.Text("OWNER", 12, UkTheme.TextMuted, wrap: false));
        var assigned = mail.AssigneeId != 0;
        var who = UkControls.Text(assigned ? mail.AssigneeName : "Nobody yet", 18, assigned ? UkTheme.Text : UkTheme.TextMuted, heading: true, wrap: false);
        who.style.marginTop = 4;
        who.style.marginBottom = 8;
        card.Add(who);

        if (ctx.CanManage)
        {
            var row = new VisualElement().Row();
            row.style.flexWrap = Wrap.Wrap;
            var me = Player.m_localPlayer;
            if (me != null)
                row.Add(AssignButton("Me", () => { mail.Assign(me.GetPlayerID(), me.GetPlayerName()); ctx.Rebuild(); }));
            foreach (var other in Trade.TradeNet.Nearby().Take(5))
            {
                var captured = other;
                row.Add(AssignButton(other.GetPlayerName(), () => { mail.Assign(captured.GetZDOID().UserID, captured.GetPlayerName()); ctx.Rebuild(); }));
            }
            if (assigned)
                row.Add(AssignButton("Clear", () => { mail.ClearAssignee(); ctx.Rebuild(); }));
            card.Add(row);
            card.Add(UkControls.Text("Pick the person this box belongs to (players near you are listed). Only they can open the door.", 13, UkTheme.TextMuted));
        }
        body.Add(card);

        var delegates = new UkField("Also may open", multiline: false);
        delegates.SetValue(mail.DelegatesText);
        delegates.Changed = text => mail.DelegatesText = text;
        delegates.Root.style.marginBottom = 4;
        body.Add(delegates.Root);
        body.Add(UkControls.Text("Player names, separated by commas, for people the owner trusts with this box.", 13, UkTheme.TextMuted));

        var postmaster = new UkSwitch("Postmaster can open it", "The player who built the hall can open every box, to sort or clear mail. Off makes it owner only.");
        postmaster.SetValue(mail.PostmasterOn, notify: false);
        postmaster.Changed = on => mail.PostmasterOn = on;
        postmaster.Root.style.marginTop = 12;
        postmaster.Root.style.marginBottom = 14;
        body.Add(postmaster.Root);
        if (!ctx.CanManage)
        {
            delegates.Input.SetEnabled(false);
            postmaster.Root.SetEnabled(false);
        }
    }

    private static Button AssignButton(string label, Action onClick)
    {
        var button = UkControls.MakeButton(label, onClick, UkButtonKind.Secondary);
        button.style.marginRight = 8;
        button.style.marginBottom = 6;
        return button;
    }

    private static string Ago(TimeSpan span) =>
        span.TotalMinutes < 1 ? "just now"
        : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
        : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
        : $"{(int)span.TotalDays} d ago";
}
