using System.Linq;
using DrakeModsLibs.API;
using DrakesItemShop.Mail;
using HarmonyLib;

namespace DrakesItemShop.Patches;

/// <summary>
/// Post wall rules for the hammer: a PO box built into a section can't be removed on its own (remove the section instead),
/// and a section can't be removed while one of its boxes still holds mail.
/// </summary>
[HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]
internal static class PostWallRemovePatch
{
    private static float _lastMessage;

    [HarmonyPostfix]
    private static void Postfix(Piece __instance, ref bool __result)
    {
        if (!__result)
            return;

        if (__instance.GetComponent<MailboxBehaviour>() is { } box && box.WallId != 0)
        {
            __result = false;
            Say("This PO box is part of the wall: remove the wall section instead.");
            return;
        }

        if (__instance.GetComponent<PostSectionBehaviour>() is { } section
            && section.Boxes().Any(b => b.Box != null && b.Box.GetInventory().GetAllItems().Count > 0))
        {
            __result = false;
            Say("Empty the PO boxes in this section first.");
        }
    }

    private static void Say(string text)
    {
        // CanBeRemoved is asked on every removal attempt; don't flood the screen.
        if (UnityEngine.Time.time - _lastMessage < 2f || Player.m_localPlayer == null)
            return;
        _lastMessage = UnityEngine.Time.time;
        CustomizeLibsAPI.ShowHudMessage(Player.m_localPlayer, MessageHud.MessageType.Center, text);
    }
}
