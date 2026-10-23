using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI;
using DrakesItemShop.Trade;
using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The Trade Counter: a double-sided counter for selling to someone standing in front of you. The shopkeeper stands behind it
/// (the +Z side, with the cash drawer), the customer in front (the -Z side, with the payment drawer). Either side presses E: the
/// person on the other side gets the trade request and you both use the usual trade window, so the swap is simultaneous and
/// neither can be cheated. The lids lift for whoever is standing at their side, and a finished trade rings the till.
/// With nobody across the counter, the shopkeeper's E opens the register chest (stock and takings) instead; Shift+E always does.
/// The register chest is an ordinary chest: wards and LockSmith decide who may open it.
/// </summary>
public class TradeCounterBehaviour : MonoBehaviour, IStorefrontUse
{
    internal static readonly List<TradeCounterBehaviour> All = new();

    private const string ChingRpc = "RPC_DrakeCounterChing";

    // tradebox.glb hinge points (glTF; Gl.P flips Z): both lids hinge along the back edge and lift at the front.
    private static readonly Vector3 ShopLidHinge = Gl.P(-0.2965f, 0.5f, -0.07f);
    private static readonly Vector3 PaymentLidHinge = Gl.P(0.2965f, 0.5f, -0.07f);
    private static readonly Vector3 Fulcrum = Gl.P(-0.07f, 0.91f, -0.19f);
    private const float LidOpenDegrees = 85f;
    private const float DrawerTravel = 0.30f;
    /// <summary>How far from the middle of each side a player stands to count as "at the counter".</summary>
    internal const float SideDistance = 0.95f;
    internal const float SideReach = 1.15f;

    private ZNetView? _nview;
    private Container? _register;
    private HingedParts? _shopLid, _paymentLid, _beam, _panL, _panR, _goldDrawer, _paymentDrawer;
    private float _shopLidOpen, _paymentLidOpen, _paymentDrawerOpen, _goldDrawerOpen;
    private float _chingAt = -10f;
    private int _chingSeen = -1;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _register = GetComponent<Container>();
        _shopLid = new HingedParts(transform, "lid_shop");
        _paymentLid = new HingedParts(transform, "lid_payment");
        _beam = new HingedParts(transform, "scale_beam");
        _panL = new HingedParts(transform, "scale_pan_L");
        _panR = new HingedParts(transform, "scale_pan_R", "coins_pan");
        _goldDrawer = new HingedParts(transform, "gold_drawer", "gold_drawer_coins");
        _paymentDrawer = new HingedParts(transform, "payment_drawer");
        if (_nview != null && _nview.IsValid())
            _nview.Register(ChingRpc, RPC_Ching);
    }

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    internal Container? Register => _register;

    /// <summary>The spot where a player stands at one side: the shop side is +Z, the customer side -Z.</summary>
    internal Vector3 StandingSpot(bool shopSide) => transform.TransformPoint(new Vector3(0f, 0f, shopSide ? SideDistance : -SideDistance));

    internal bool IsStandingAt(Player player, bool shopSide)
    {
        var local = transform.InverseTransformPoint(player.transform.position);
        if (Mathf.Abs(local.y) > 1.6f || Mathf.Abs(local.x) > 0.95f)
            return false;
        return shopSide ? local.z > 0.3f && local.z < SideDistance + SideReach : local.z < -0.3f && local.z > -(SideDistance + SideReach);
    }

    /// <summary>The nearest player (not <paramref name="except"/>) standing at a side.</summary>
    internal Player? PlayerAt(bool shopSide, Player? except)
    {
        Player? best = null;
        var bestDistance = float.MaxValue;
        foreach (var other in Player.GetAllPlayers())
        {
            if (other == null || other == except || other.IsDead() || !IsStandingAt(other, shopSide))
                continue;
            var d = (other.transform.position - StandingSpot(shopSide)).sqrMagnitude;
            if (d < bestDistance)
            {
                best = other;
                bestDistance = d;
            }
        }
        return best;
    }

    private void Update()
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        var me = Player.m_localPlayer;
        if (me == null || (me.transform.position - transform.position).sqrMagnitude > 40f * 40f)
            return;

        var step = Time.deltaTime / 0.35f;
        var shopHere = PlayerAt(true, null) != null;
        var customerHere = PlayerAt(false, null) != null;
        _shopLidOpen = Mathf.MoveTowards(_shopLidOpen, shopHere ? 1f : 0f, step);
        _paymentLidOpen = Mathf.MoveTowards(_paymentLidOpen, customerHere ? 1f : 0f, step);
        // The payment drawer comes out for the customer; the cash drawer pops for a moment when a sale goes through.
        _paymentDrawerOpen = Mathf.MoveTowards(_paymentDrawerOpen, customerHere ? 1f : 0f, step);
        var sinceChing = Time.time - _chingAt;
        _goldDrawerOpen = Mathf.MoveTowards(_goldDrawerOpen, sinceChing is > 0f and < 3f ? 1f : 0f, step * 1.5f);

        // Lids lift at the front edge (the -Z side), so positive turn about X.
        _shopLid?.Turn(ShopLidHinge, Quaternion.AngleAxis(LidOpenDegrees * Mathf.SmoothStep(0f, 1f, _shopLidOpen), Vector3.right));
        _paymentLid?.Turn(PaymentLidHinge, Quaternion.AngleAxis(LidOpenDegrees * Mathf.SmoothStep(0f, 1f, _paymentLidOpen), Vector3.right));
        // The cash drawer faces the shop side (+Z), the payment drawer the customer side (-Z).
        _goldDrawer?.Slide(new Vector3(0f, 0f, DrawerTravel * Mathf.SmoothStep(0f, 1f, _goldDrawerOpen)));
        _paymentDrawer?.Slide(new Vector3(0f, 0f, -DrawerTravel * Mathf.SmoothStep(0f, 1f, _paymentDrawerOpen)));
        BalanceScale(sinceChing, shopHere || customerHere);
    }

    private void BalanceScale(float sinceChing, bool busy)
    {
        if (_beam == null || _panL == null || _panR == null)
            return;
        // Idle: barely moves. Customer and shopkeeper both here: it rocks gently. After a sale: a quick settle from a big tip.
        var t = Time.time;
        var angle = (busy ? 3f : 1f) * Mathf.Sin(t * (busy ? 1.6f : 0.7f));
        if (sinceChing is > 0f and < 4f)
            angle += 11f * Mathf.Exp(-1.8f * sinceChing) * Mathf.Sin(sinceChing * 7f);

        var beamTurn = Quaternion.AngleAxis(angle, Vector3.forward);
        _beam.Turn(Fulcrum, beamTurn);
        // Each pan hangs from its end of the beam and stays level: it moves with the beam end but doesn't turn.
        _panL.Slide(beamTurn * (Gl.P(-0.19f, 0.894f, -0.19f) - Fulcrum) + Fulcrum - Gl.P(-0.19f, 0.894f, -0.19f));
        _panR.Slide(beamTurn * (Gl.P(0.05f, 0.894f, -0.19f) - Fulcrum) + Fulcrum - Gl.P(0.05f, 0.894f, -0.19f));
    }

    /// <summary>A trade at this counter went through: ring the till for everyone nearby.</summary>
    internal void RingTill()
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.InvokeRPC(ZNetView.Everybody, ChingRpc);
    }

    private void RPC_Ching(long sender)
    {
        _chingAt = Time.time;
        StorefrontAudio.PlayAt(StorefrontAudio.RegisterChing, transform.TransformPoint(Gl.P(-0.28f, 0.3f, 0.2f)), 1f, 1f, 30f);
    }

    // ---- the trade key: someone just finished a trade next to a counter ----

    internal static void OnTradeCompleted(TradeSession session)
    {
        var me = Player.m_localPlayer;
        if (me == null)
            return;
        TradeCounterBehaviour? best = null;
        var bestDistance = 3.2f * 3.2f;
        foreach (var counter in All)
        {
            if (counter == null)
                continue;
            var d = (counter.transform.position - me.transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                best = counter;
                bestDistance = d;
            }
        }
        if (best == null)
            return;
        // Both people see the trade finish; only one of them rings the till (the lower peer id), so it isn't doubled.
        if (session.Partner is RemotePartner remote && remote.Peer < ZNet.GetUID())
            return;
        best.RingTill();
    }

    // ---- what E does on each side ----

    /// <summary>Hover text for one side of the counter.</summary>
    internal string HoverFor(bool shopSide, Player me)
    {
        var loc = global::Localization.instance;
        var key = "[<color=yellow><b>" + loc.Localize("$KEY_Use") + "</b></color>]";
        var other = PlayerAt(!shopSide, me);
        var name = loc.Localize("$drake_tradecounter_name");
        if (other != null)
            return $"{name}\n{key} Trade with {other.GetPlayerName()}" + (shopSide ? "\n[<color=yellow><b>Shift+" + loc.Localize("$KEY_Use") + "</b></color>] Open register" : "");
        if (shopSide)
            return $"{name}\n{key} Open register\n<color=grey>Nobody at the counter</color>";
        return $"{name}\n<color=grey>Nobody behind the counter to trade with</color>";
    }

    internal bool UseSide(bool shopSide, Humanoid user, bool alt)
    {
        if (user is not Player me)
            return false;

        // Shopkeeper: Shift+E, or E with nobody across the counter, opens the register chest.
        var other = PlayerAt(!shopSide, me);
        if (shopSide && (alt || other == null))
        {
            if (_register == null)
                return false;
            StorefrontUse.Bypass = true;
            try
            {
                return _register.Interact(user, false, false);
            }
            finally
            {
                StorefrontUse.Bypass = false;
            }
        }

        if (other == null)
        {
            DrakeMessage.Show("Nobody is on the other side of the counter.", DrakeMessageKind.Warning, null, 2.5f);
            return true;
        }
        TradeNet.RequestTrade(other);
        return true;
    }

    // ---- IStorefrontUse: the side is whichever the player stands on ----

    private bool OnShopSide(Player me) => transform.InverseTransformPoint(me.transform.position).z > 0f;

    public string GetHoverText() => Player.m_localPlayer == null ? "" : global::Localization.instance.Localize(HoverFor(OnShopSide(Player.m_localPlayer), Player.m_localPlayer));

    public bool Interact(Humanoid user, bool hold, bool alt) =>
        !hold && user is Player me && UseSide(OnShopSide(me), user, alt);
}
