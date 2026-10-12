using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DrakeModsLibs.Display;
using DrakeModsLibs.UI.Toolkit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Mail;

/// <summary>Local mailbox settings.</summary>
internal static class MailboxSettings
{
    private static ConfigEntry<bool> _skipWarning = default!;

    internal static bool SkipWarning
    {
        get => _skipWarning.Value;
        set => _skipWarning.Value = value;
    }

    internal static void Bind(ConfigFile config) =>
        _skipWarning = config.Bind("07 Mailbox", "Skip send warning", false,
            "Don't warn that sent items can't be taken back out from the front (press X on the warning to turn this on).");
}

/// <summary>
/// The public side of a mailbox: pick up to 3 stacks from your inventory and press Send; they go into the box and whoever may open
/// the back door finds them there. One way: nothing can be taken out from here.
/// </summary>
internal sealed class MailboxSendWindow
{
    private const int Slots = 3;
    private const int SlotSize = 64;
    private const int Tile = 52;
    private const float CloseDistance = 6f;

    private static MailboxSendWindow? _current;

    private readonly UkWindow _window;
    private readonly MailboxBehaviour _mail;
    private readonly List<(ItemDrop.ItemData Item, int Amount)> _offer = new();
    private readonly VisualElement _slots = new();
    private readonly VisualElement _inventory = new();
    private readonly Label _status;
    private readonly UkField _note;
    private readonly Button _send;
    private bool _warning;

    internal static bool IsOpen => _current != null && _current._window.IsOpen;

    /// <summary>Per frame: X on the send warning sends and stops the warning from showing again.</summary>
    internal static void PollKeys()
    {
        if (_current is { _warning: true } window && window._window.IsOpen && Input.GetKeyDown(KeyCode.X))
        {
            MailboxSettings.SkipWarning = true;
            window.Deliver();
        }
    }

    internal static void Open(MailboxBehaviour mail)
    {
        _current?._window.CloseSilent();
        _current = new MailboxSendWindow(mail);
        _current.Refresh();
        _current._window.DockRight();   // off to the side, so the flap swinging open stays in view
        _current._window.Open();
        mail.SetFlap(true);
    }

    private MailboxSendWindow(MailboxBehaviour mail)
    {
        _mail = mail;
        _window = new UkWindow("drakes_mailbox_send", 520);
        var filter = mail.FilterText;
        _window.SetHeader(mail.SignName(global::Localization.instance.Localize(mail.Box != null ? mail.Box.m_name : "$drake_mailbox_name")),
            filter.Length == 0 ? "Send up to 3 stacks to the owner" : "Takes only: " + filter);

        var slotsCard = UkControls.Card().Column().Pad(12);
        slotsCard.Add(UkControls.Text("YOU SEND", 12, UkTheme.TextMuted, wrap: false));
        _slots.style.flexDirection = FlexDirection.Row;
        _slots.style.marginTop = 8;
        slotsCard.Add(_slots);
        _window.Body.Add(slotsCard);

        _note = new UkField("Note (optional)", multiline: false);
        _note.SetMax(80);
        _note.Root.style.marginTop = 12;
        _window.Body.Add(_note.Root);

        var title = UkControls.Text("YOUR INVENTORY", 12, UkTheme.TextMuted, wrap: false);
        title.style.marginTop = 14;
        title.style.marginBottom = 6;
        _window.Body.Add(title);
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 240;
        scroll.Add(_inventory);
        var inventoryCard = UkControls.Card().Pad(10);
        inventoryCard.Add(scroll);
        _window.Body.Add(inventoryCard);

        _status = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _status.style.flexGrow = 1;
        _status.style.flexShrink = 1;
        _status.style.marginLeft = 16;
        _status.style.whiteSpace = WhiteSpace.Normal;
        _send = UkControls.MakeButton("Send", Send, UkButtonKind.Primary, 140);
        _window.Footer.Add(UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary));
        _window.Footer.Add(_status);
        _window.Footer.Add(_send);
        _window.ShowFooter(true);

        _window.Closed = () => _mail.SetFlap(false);
        // Walking away closes it, so the flap never stays up.
        _window.Body.schedule.Execute(() =>
        {
            var player = Player.m_localPlayer;
            if (_window.IsOpen && (player == null || Vector3.Distance(player.transform.position, _mail.transform.position) > CloseDistance))
                _window.Close();
        }).Every(500);
    }

    private void Refresh()
    {
        _slots.Clear();
        for (var i = 0; i < Slots; i++)
        {
            var entry = i < _offer.Count ? _offer[i] : default;
            var slot = new VisualElement().Size(SlotSize, SlotSize).Fixed().Fill(UkTheme.Field).Round(UkTheme.Radius)
                .Border(entry.Item == null ? UkTheme.LineSoft : UkTheme.Line);
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            slot.style.marginRight = 8;
            if (entry.Item != null)
            {
                slot.Add(UkControls.IconTile(entry.Item.GetIcon(), SlotSize - 12));
                if (entry.Amount > 1)
                    slot.Add(Corner(entry.Amount.ToString()));
                var captured = entry;
                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    _offer.Remove(captured);
                    Refresh();
                });
                _window.Tips.Attach(slot, () => Name(captured.Item));
            }
            _slots.Add(slot);
        }

        _inventory.Clear();
        _inventory.style.flexDirection = FlexDirection.Row;
        _inventory.style.flexWrap = Wrap.Wrap;
        var inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
        if (inventory != null)
        {
            foreach (var item in inventory.GetAllItems().Where(i => !i.m_equipped))
            {
                var chosen = _offer.Any(o => o.Item == item);
                var tile = new VisualElement().Size(Tile + 8, Tile + 8).Fixed().Fill(chosen ? UkTheme.Active : UkTheme.Field)
                    .Round(UkTheme.RadiusSmall).Border(chosen ? UkTheme.Accent : UkTheme.LineSoft);
                tile.style.marginRight = 6;
                tile.style.marginBottom = 6;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                tile.style.opacity = chosen ? 0.55f : 1f;
                tile.Add(UkControls.IconTile(item.GetIcon(), Tile));
                if (item.m_stack > 1)
                    tile.Add(Corner(item.m_stack.ToString()));
                var captured = item;
                tile.RegisterCallback<ClickEvent>(_ => Toggle(captured));
                _window.Tips.Attach(tile, () => Name(captured));
                _inventory.Add(tile);
            }
        }

        UkControls.SetButtonEnabled(_send, _offer.Count > 0);
    }

    private void Toggle(ItemDrop.ItemData item)
    {
        ClearWarning();
        var existing = _offer.FindIndex(o => o.Item == item);
        if (existing >= 0)
            _offer.RemoveAt(existing);
        else if (!_mail.Accepts(item))
            _status.text = "This box doesn't take that.";
        else if (_offer.Count < Slots)
            _offer.Add((item, item.m_stack));
        else
            _status.text = "The slot takes 3 stacks at a time.";
        Refresh();
    }

    private void ClearWarning()
    {
        _warning = false;
        _send.text = "Send";
    }

    private void Send()
    {
        if (_offer.Count == 0)
            return;
        // Sending is one way: say so first, once per press unless the player has turned the warning off.
        if (!MailboxSettings.SkipWarning && !_warning)
        {
            _warning = true;
            _send.text = "Yes, send";
            _status.text = "Sure? You can't get these out again without access to the back. Press X to skip this message from now on.";
            return;
        }
        Deliver();
    }

    private void Deliver()
    {
        ClearWarning();
        var player = Player.m_localPlayer;
        if (player == null || _offer.Count == 0)
            return;
        if (_offer.Any(o => !player.GetInventory().ContainsItem(o.Item)))
        {
            _status.text = "An item left your inventory.";
            _offer.Clear();
            Refresh();
            return;
        }
        if (!_mail.Deposit(player.GetInventory(), _offer, _note.Value))
        {
            _status.text = "The box is full.";
            return;
        }
        _offer.Clear();
        _status.text = "Sent.";
        Refresh();
        _window.Close();
    }

    private static Label Corner(string text)
    {
        var label = UkControls.Text(text, 12, UkTheme.Gold, wrap: false);
        label.style.position = Position.Absolute;
        label.style.bottom = 2;
        label.style.right = 4;
        return label;
    }

    private static string Name(ItemDrop.ItemData item) => ItemDisplayService.GetDisplayNameForUi(item, localize: true);
}

/// <summary>A PO box has its own owner rules (the person it's assigned to, their delegates, the builder as postmaster), whatever the ward or LockSmith say.</summary>
[HarmonyPatch(typeof(Container), "CheckAccess")]
internal static class PoBoxAccessPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, long playerID, ref bool __result)
    {
        var mail = __instance.GetComponent<MailboxBehaviour>();
        if (mail != null && mail.m_singleDoor)
            __result = mail.MayOpen(playerID);
    }
}

/// <summary>Without LockSmith there is no Lock menu: Shift+E on a mailbox opens the Mail settings by themselves.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class MailboxSettingsInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        var mail = __instance.GetComponent<MailboxBehaviour>();
        if (mail == null || !alt || hold || character is not Player player || MailboxSettingsWindow.LockSmithShowsMenu(__instance))
            return true;
        __result = true;
        if (mail.CanConfigure(player))
            MailboxSettingsWindow.Open(__instance, mail, player);
        else
            Trade.TradeNet.Say("Only the owner can change this box.");
        return false;
    }
}

/// <summary>The public side sends, the back is a plain chest (wards and LockSmith decide who may open that).</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class MailboxInteractPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result)
    {
        var mail = __instance.GetComponent<MailboxBehaviour>();
        if (mail == null || character == null)
            return true;
        // A PO box has one door: people who may open it open it; everyone else uses the slot.
        if (mail.m_singleDoor)
        {
            if (character is Player owner && mail.CanOpen(owner))
                return true;
        }
        else if (!mail.IsPublicSide(character))
        {
            return true;
        }

        __result = true;
        if (hold || MailboxSendWindow.IsOpen)
            return false;
        if (mail.m_singleDoor && mail.SignRaw.Length == 0)
        {
            Trade.TradeNet.Say("This PO box has no owner yet.");
            return false;
        }
        if (!mail.AcceptOn)
        {
            Trade.TradeNet.Say("This mailbox is closed.");
            return false;
        }
        if (character is Player visitor && !mail.MaySend(visitor))
        {
            Trade.TradeNet.Say("You're not on this mailbox's list.");
            return false;
        }
        MailboxSendWindow.Open(mail);
        return false;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class MailboxHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        var mail = __instance.GetComponent<MailboxBehaviour>();
        if (mail == null || Player.m_localPlayer == null)
            return;

        var plain = global::Localization.instance.Localize(__instance.m_name);
        var signed = mail.SignName(plain);
        if (mail.m_singleDoor)
        {
            // Access (LockSmith, wards) decides between the owner's door and the public slot.
            if (mail.CanOpen(Player.m_localPlayer))
            {
                if (__result.Contains(plain))
                    __result = __result.Replace(plain, signed);
                return;
            }
            __result = global::Localization.instance.Localize(mail.SignRaw.Length == 0
                ? $"{signed}\n<color=grey>No owner yet</color>"
                : mail.AcceptOn ? $"{signed}\n[<color=yellow><b>$KEY_Use</b></color>] Send items" : $"{signed}\n<color=grey>Closed for mail</color>");
            return;
        }

        if (mail.IsPublicSide(Player.m_localPlayer))
        {
            __result = global::Localization.instance.Localize(mail.AcceptOn
                ? $"{signed}\n[<color=yellow><b>$KEY_Use</b></color>] Send items"
                : $"{signed}\n<color=grey>Closed for mail</color>");
            return;
        }

        if (__result.Contains(plain))
            __result = __result.Replace(plain, signed);   // the back keeps the chest's own text (LockSmith lines and all)
        // Tell the builder when the back door is open to anyone.
        if (!mail.m_singleDoor && !mail.IsProtected && __instance.GetComponent<Piece>() is { } piece && piece.GetCreator() == Player.m_localPlayer.GetPlayerID())
            __result += "\n<color=orange>Not in a ward: anyone can open this back</color>";
    }
}
