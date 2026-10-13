using DrakesItemShop.Pricing;
using HarmonyLib;

namespace DrakesItemShop.DisplayCard;

/// <summary>Hover a buy card: the deal from the seller's side ("Sell 50 Wood for 20 coins") and [E] Sell.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class BuyCardHoverPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        if (__instance.GetComponent<BuyCardShelf>() == null)
            return;
        if (BuyCardTag.UnderCrosshair(__instance.transform) is not { } card)
            return;
        var nview = __instance.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        var zdo = nview.GetZDO();
        var offers = BuyOffers.All(zdo);
        if (card.OfferIndex < 0 || card.OfferIndex >= offers.Count)
            return;
        var offer = offers[card.OfferIndex];
        if (!offer.IsValid)
            return;

        var name = BuyOffers.ItemLabel(offer.WantPrefab);
        var lot = offer.WantAmount == 1 ? name : $"{offer.WantAmount} {name}";
        var shopName = global::Localization.instance.Localize(__instance.m_name);
        var player = Player.m_localPlayer;

        __result = $"Sell <color=#e0ad4f>{lot}</color> for <color=#e0ad4f>{offer.PayCoins} coins</color>\n{shopName}";
        if (offer.Cap > 0)
            __result += $"\n<color=#c0b090>Wants {offer.Remaining} more (of {offer.Cap})</color>";
        if (player != null && ShopState.CanManage(__instance, player))
            __result += "\n<color=grey>Your buy offer (Shift+E → Buy tab to edit)</color>";

        if (!ShopState.IsOpen(__instance))
            __result += "\n<color=grey>Shop closed — not taking sales</color>";
        else if (!offer.Accepting)
            __result += $"\n<color=grey>Not taking sales of {name} right now</color>";
        else if (BuyOffers.Spendable(__instance, zdo, offer) < offer.PayCoins)
            __result += $"\n<color=grey>{ShopSell.OutOfMoney(offer)}</color>";
        else
            __result += "\n[<color=yellow><b>E</b></color>] Sell";
    }
}

/// <summary>E on a buy card opens the sell window — for anyone, owners included (the chest still opens off the card).</summary>
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class BuyCardInteractPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        if (alt || __instance.GetComponent<BuyCardShelf>() == null || character is not Player player)
            return true;
        if (BuyCardTag.UnderCrosshair(__instance.transform) is not { } card)
            return true;

        __result = true;
        if (hold || SellWindow.IsOpen)
            return false;

        var refuse = ShopSell.RefuseReason(__instance, card.OfferIndex, 1, player);
        if (refuse != null)
        {
            Trade.TradeNet.Say(refuse);
            return false;
        }
        SellWindow.Open(__instance, card.OfferIndex);
        return false;
    }
}
