using BepInEx.Bootstrap;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine.UIElements;

namespace DrakesItemShop.Mail;

/// <summary>
/// The Mail settings on their own, for servers without LockSmith (which otherwise shows them as a tab in its Lock menu):
/// Shift+E on a mailbox or PO box opens this for its owner.
/// </summary>
internal static class MailboxSettingsWindow
{
    private const string LockSmithGuid = "com.drakesworkshop.locksmith";

    internal static bool LockSmithPresent => Chainloader.PluginInfos.ContainsKey(LockSmithGuid);

    /// <summary>
    /// True when LockSmith will open the shared menu for this player on this chest (they are on the lock), and that menu carries the
    /// Mail tab. Otherwise this window is the menu: no LockSmith, or nothing there for them.
    /// </summary>
    internal static bool LockSmithShowsMenu(Container container)
    {
        if (!LockSmithPresent)
            return false;
        try
        {
            var menu = System.Type.GetType("LockSmith.UI.PieceAccessMenu, LockSmith");
            var has = menu?.GetMethod("HasLockMenu", new[] { typeof(Container) });
            return has != null && (bool)has.Invoke(null, new object[] { container });
        }
        catch
        {
            return false;
        }
    }

    private static UkWindow? _window;

    internal static void Open(Container container, MailboxBehaviour mail, Player player)
    {
        _window?.CloseSilent();
        var window = new UkWindow("drakes_mail_settings", 560);
        _window = window;
        window.SetHeader(mail.SignName(global::Localization.instance.Localize(container.m_name)), "Mail settings");
        Draw(window, container, mail, player);
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Close", () => window.Close(), UkButtonKind.Secondary));
        window.ShowFooter(true);
        window.Open();
    }

    private static void Draw(UkWindow window, Container container, MailboxBehaviour mail, Player player)
    {
        window.Body.Clear();
        var body = new VisualElement().Column();
        MailboxMenuTab.BuildInto(new DrakeModsLibs.Integration.PieceMenuTabContext(container, body, mail.CanConfigure(player),
            () => Draw(window, container, mail, player)));
        window.Body.Add(body);
    }
}
