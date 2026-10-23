using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI;
using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The desk bell: press E and it dings for everyone nearby (the plunger dips, the dome shivers). Whoever is within earshot gets a
/// "someone is at the desk" card, so a shopkeeper in the back room knows a customer is waiting.
/// </summary>
public class DeskBellBehaviour : MonoBehaviour, IStorefrontUse
{
    private const string Rpc = "RPC_DrakeDeskBell";
    private const float Cooldown = 0.6f;
    private const float AlertRange = 30f;

    // desk_bell.glb: the plunger sits on the dome top (y 0.104); the dome's base centre is (0, 0.036, 0).
    private static readonly Vector3 DomeHinge = Gl.P(0f, 0.036f, 0f);

    private ZNetView? _nview;
    private HingedParts? _dome, _plunger;
    private float _ringAt = -10f;
    private float _lastRing = -10f;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _dome = new HingedParts(transform, "bell");
        _plunger = new HingedParts(transform, "plunger");
        if (_nview != null && _nview.IsValid())
            _nview.Register<string>(Rpc, RPC_Ring);
    }

    private void Update()
    {
        var t = Time.time - _ringAt;
        if (t > 1.2f)
            return;
        // Plunger: quick dip, slower return. Dome: a small shiver that dies away.
        var press = t < 0.06f ? t / 0.06f : Mathf.Clamp01(1f - (t - 0.06f) / 0.22f);
        _plunger?.Slide(new Vector3(0f, -0.014f * press, 0f));
        var shake = 4f * Mathf.Exp(-5f * t) * Mathf.Sin(t * 70f);
        _dome?.Turn(DomeHinge, Quaternion.Euler(shake * 0.6f, 0f, shake));
    }


    public string GetHoverName() => global::Localization.instance.Localize("$drake_deskbell_name");

    public string GetHoverText() =>
        global::Localization.instance.Localize("$drake_deskbell_name") + "\n[<color=yellow><b>" + global::Localization.instance.Localize("$KEY_Use") + "</b></color>] Ring";

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user is not Player player || _nview == null || !_nview.IsValid())
            return false;
        if (Time.time - _lastRing < Cooldown)
            return true;
        _lastRing = Time.time;
        _nview.InvokeRPC(ZNetView.Everybody, Rpc, player.GetPlayerName());
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    private void RPC_Ring(long sender, string ringer)
    {
        _ringAt = Time.time;
        StorefrontAudio.PlayAt(StorefrontAudio.DeskDing, transform.position + Vector3.up * 0.1f, 1f, Random.Range(0.97f, 1.03f), 32f);

        var me = Player.m_localPlayer;
        if (me == null || sender == ZNet.GetUID())
            return;
        if ((me.transform.position - transform.position).sqrMagnitude <= AlertRange * AlertRange)
            DrakeMessage.Show(string.IsNullOrEmpty(ringer) ? "Someone is at the desk bell" : ringer + " is at the desk bell",
                DrakeMessageKind.Info, "Desk bell", 3.5f);
    }
}

/// <summary>
/// The door bell, mounted on a bracket by the door. When a door close to it opens it rings (the bell and clapper swing), and anyone
/// nearby who isn't the one opening the door gets a card: "A customer has arrived". It reads the door's synced open/closed state,
/// so every client near it hears the same thing without any extra network traffic.
/// </summary>
public class DoorBellBehaviour : MonoBehaviour
{
    private const float DoorPoll = 0.25f;
    private const float RingCooldown = 2.5f;
    private const float OpenerDistance = 2.1f;

    // door_bell.glb: the bell hangs from (0, 0.08, 0.31) at the end of the arm; the clapper from (0, 0.03, 0.31).
    private static readonly Vector3 BellHinge = Gl.P(0f, 0.08f, 0.31f);

    private HingedParts? _bell, _clapper;
    private readonly Dictionary<Door, int> _doorStates = new();
    private float _nextPoll;
    private float _lastRing = -10f;
    private float _ringAt = -10f;
    private float _swing;

    private void Awake()
    {
        _bell = new HingedParts(transform, "bell");
        _clapper = new HingedParts(transform, "clapper");
        _nextPoll = Time.time + Random.value * DoorPoll;
    }

    private void Update()
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        Animate();
        if (Time.time < _nextPoll)
            return;
        _nextPoll = Time.time + DoorPoll;
        WatchDoors();
    }

    private void Animate()
    {
        var t = Time.time - _ringAt;
        if (t > 4f)
            return;
        // Two damped swings: the bell, and the clapper that lags behind and swings wider.
        var decay = Mathf.Exp(-1.5f * t);
        var bell = 16f * decay * Mathf.Sin(t * 19f);
        var clapper = 30f * decay * Mathf.Sin(t * 19f - 1.3f);
        _bell?.Turn(BellHinge, Quaternion.Euler(bell, 0f, bell * 0.25f));
        _clapper?.Turn(BellHinge, Quaternion.Euler(clapper, 0f, clapper * 0.25f));
    }

    private void WatchDoors()
    {
        var seen = new HashSet<Door>();
        foreach (var hit in Physics.OverlapSphere(transform.position, StorefrontSettings.DoorRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var door = hit.GetComponentInParent<Door>();
            if (door == null || !seen.Add(door))
                continue;
            var nview = door.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
                continue;
            var state = nview.GetZDO().GetInt(StorefrontZdo.Key("state"), 0);
            if (_doorStates.TryGetValue(door, out var before) && before == 0 && state != 0)
                OnDoorOpened(door);
            _doorStates[door] = state;
        }

        // Forget doors that left range or were removed.
        foreach (var gone in _doorStates.Keys.Where(d => d == null || !seen.Contains(d)).ToList())
            _doorStates.Remove(gone);
    }

    private void OnDoorOpened(Door door)
    {
        if (Time.time - _lastRing < RingCooldown)
            return;
        _lastRing = Time.time;
        _ringAt = Time.time;
        StorefrontAudio.PlayAt(StorefrontAudio.DoorJingle, transform.position + Vector3.down * 0.05f, 1f, Random.Range(0.98f, 1.02f), 34f);

        var me = Player.m_localPlayer;
        if (!StorefrontSettings.DoorMessage || me == null)
            return;
        if ((me.transform.position - transform.position).sqrMagnitude > StorefrontSettings.AlertRange * StorefrontSettings.AlertRange)
            return;
        // The person pushing the door open hears the bell but doesn't need telling they arrived.
        if ((me.transform.position - door.transform.position).sqrMagnitude < OpenerDistance * OpenerDistance)
            return;
        DrakeMessage.Show("A customer has arrived", DrakeMessageKind.Info, "Door bell", 4f);
    }
}
