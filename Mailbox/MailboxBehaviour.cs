using System;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DrakesItemShop.Mail;

/// <summary>
/// Animates the mailbox: the front send flap lifts while someone is at the send window, the back door swings open while the
/// chest is in use. Both are the model's "Flap" and "Door" parts (renderers named glb{i}_Flap / glb{i}_Door), turned about their
/// hinge points. Which side a player is on decides what E does (see Patches).
/// </summary>
public class MailboxBehaviour : MonoBehaviour
{
    public Vector3 m_flapHinge;
    public float m_flapAngle;
    public Vector3 m_doorHinge;
    public float m_doorAngle;
    public Vector3 m_flagHinge;
    public float m_flagAngle;
    /// <summary>A PO box: its one front door is the mail slot and the owner's door.</summary>
    public bool m_singleDoor;
    public Vector3 m_platePosition;

    private TextMeshPro? _plateText;
    private string _plateShown = "\u0001";

    private const float SwingSeconds = 0.35f;
    /// <summary>Synced on the piece: 1 while somebody is at the send window.</summary>
    private static readonly int FlapKey = "drake_mail_flap".GetStableHashCode();
    private static readonly int InUseKey = "InUse".GetStableHashCode();
    /// <summary>The builder's name, shown on the box as its little sign.</summary>
    private static readonly int OwnerKey = "drake_mail_owner".GetStableHashCode();
    /// <summary>Counts deliveries, so the builder's client can say "mail arrived".</summary>
    private static readonly int SeqKey = "drake_mail_seq".GetStableHashCode();
    private static readonly int NotifyKey = "drake_mail_notify".GetStableHashCode();   // 0 = off; unset = on
    private static readonly int LogKey = "drake_mail_log".GetStableHashCode();         // "name|stacks|utc seconds" lines, newest last
    private static readonly int AllowKey = "drake_mail_allow".GetStableHashCode();     // names allowed to send, comma separated; empty = anyone
    private static readonly int FilterKey = "drake_mail_filter".GetStableHashCode();   // item names accepted, comma separated; empty = anything
    private static readonly int AssigneeKey = "drake_mail_assignee".GetStableHashCode();   // player id of a PO box's owner
    private static readonly int AssigneeNameKey = "drake_mail_assignee_name".GetStableHashCode();
    private static readonly int DelegatesKey = "drake_mail_delegates".GetStableHashCode(); // names that may also open it
    private static readonly int PostmasterKey = "drake_mail_postmaster".GetStableHashCode(); // 0 = the builder can't open it; unset = can
    private static readonly int AcceptKey = "drake_mail_accept".GetStableHashCode();   // 0 = send slot closed; unset = open
    private const int LogLines = 6;
    private int _seenSeq = -1;

    private ZNetView? _nview;
    private Container? _container;
    // A part with two materials arrives as several renderers (glb{i}_Door, glb{j}_Door.001, ...): all swing together.
    private readonly List<(Transform Part, Vector3 Pos, Quaternion Rot)> _flap = new();
    private readonly List<(Transform Part, Vector3 Pos, Quaternion Rot)> _door = new();
    private readonly List<(Transform Part, Vector3 Pos, Quaternion Rot)> _flag = new();
    private readonly List<Renderer> _flagRenderers = new();
    private float _flagUp;
    private int _flagState = -1;
    private static readonly MaterialPropertyBlock FlagPaint = new();
    private float _flapOpen;
    private float _doorOpen;

    public Container? Box => _container != null ? _container : _container = GetComponent<Container>();

    internal static readonly List<MailboxBehaviour> Live = new();

    /// <summary>The post wall section this PO box is built into (0 = a loose box).</summary>
    public long WallId => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetLong(PostSectionBehaviour.WallIdKey) : 0L;

    private float _orphanCheckAt;
    private int _orphanStrikes;

    private void OnEnable() => Live.Add(this);

    private void OnDisable() => Live.Remove(this);

    /// <summary>
    /// A box built into a wall gives nothing back when it breaks (the section's cost covered it), and goes away when its section
    /// is gone: checked a few times over several seconds by the box's owner, so a section that loads a moment later still counts.
    /// The box breaks like a chest, so any mail in it drops on the ground.
    /// </summary>
    private void UpdateWallLink()
    {
        if (Time.time < _orphanCheckAt || _nview == null || !_nview.IsOwner())
            return;
        _orphanCheckAt = Time.time + 2f;
        var wallId = WallId;
        if (wallId == 0)
            return;
        if (GetComponent<Piece>() is { } piece && piece.m_resources.Length > 0)
            piece.m_resources = Array.Empty<Piece.Requirement>();
        if (PostSectionBehaviour.Find(wallId) != null)
        {
            _orphanStrikes = 0;
            return;
        }
        if (++_orphanStrikes < 4)
            return;
        ItemShopPlugin.LogSource?.LogInfo($"[PostWall] PO box at {transform.position} lost its section (wall {wallId}); removing it.");
        if (GetComponent<WearNTear>() is { } wear)
            wear.Remove(false);
        else
            ZNetScene.instance.Destroy(gameObject);
    }

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _container = GetComponent<Container>();
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            var t = renderer.transform;
            if (renderer.name.Contains("_Flap"))
                _flap.Add((t, t.localPosition, t.localRotation));
            else if (renderer.name.Contains("_Door"))
                _door.Add((t, t.localPosition, t.localRotation));
            else if (renderer.name.Contains("_Flag"))
            {
                _flag.Add((t, t.localPosition, t.localRotation));
                _flagRenderers.Add(renderer);
            }
        }
        var names = new StringBuilder();
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            names.Append(renderer.name).Append(renderer.enabled ? "" : "(off)").Append(", ");
        ItemShopPlugin.LogSource?.LogInfo($"[Mailbox] {name}: {_flap.Count} flap and {_door.Count} door renderers; all renderers: {names}");
    }

    private void Start()
    {
        // Whoever built it signs it: the builder's own client writes their name once.
        var player = Player.m_localPlayer;
        var piece = GetComponent<Piece>();
        if (!m_singleDoor && player != null && piece != null && _nview != null && _nview.IsValid() && _nview.IsOwner()
            && string.IsNullOrEmpty(_nview.GetZDO().GetString(OwnerKey)) && piece.GetCreator() == player.GetPlayerID())
            _nview.GetZDO().Set(OwnerKey, player.GetPlayerName());
    }

    /// <summary>The sign text as stored (empty = unsigned).</summary>
    public string SignRaw => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetString(OwnerKey) : "";

    /// <summary>Set the sign text for everyone (blank goes back to the builder's name).</summary>
    public void SetSign(string text)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(OwnerKey, text.Trim());
    }

    private string Stored(int key) => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetString(key) : "";

    private void Store(int key, string value)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(key, value.Trim());
    }

    /// <summary>Who may send, as typed (names, comma separated). Empty means anyone.</summary>
    public string AllowText
    {
        get => Stored(AllowKey);
        set => Store(AllowKey, value);
    }

    /// <summary>What it accepts, as typed (item names, comma separated). Empty means anything.</summary>
    public string FilterText
    {
        get => Stored(FilterKey);
        set => Store(FilterKey, value);
    }

    private static string[] Tokens(string text) =>
        text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).ToArray();

    /// <summary>The builder always may; otherwise anyone unless an allow list names people.</summary>
    public bool MaySend(Player player)
    {
        var allowed = Tokens(AllowText);
        if (allowed.Length == 0)
            return true;
        if (GetComponent<Piece>() is { } piece && piece.GetCreator() == player.GetPlayerID())
            return true;
        return allowed.Contains(player.GetPlayerName().ToLowerInvariant());
    }

    /// <summary>Whether an item may go in: matches a filter word by its prefab name or part of its shown name.</summary>
    public bool Accepts(ItemDrop.ItemData item)
    {
        var words = Tokens(FilterText);
        if (words.Length == 0)
            return true;
        var prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name.ToLowerInvariant() : "";
        var shown = global::Localization.instance.Localize(item.m_shared.m_name).ToLowerInvariant();
        return words.Any(w => w == prefab || shown.Contains(w));
    }

    // ---- PO box ownership, without LockSmith: an assigned person, their delegates, and the builder as postmaster ----

    public long AssigneeId => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetLong(AssigneeKey) : 0L;
    public string AssigneeName => Stored(AssigneeNameKey);

    /// <summary>Make this player the box's owner (their name goes on the plate when it has no label yet).</summary>
    public void Assign(long playerId, string name)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(AssigneeKey, playerId);
        _nview.GetZDO().Set(AssigneeNameKey, name);
        if (SignRaw.Length == 0)
            SetSign(name);
    }

    public void ClearAssignee()
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(AssigneeKey, 0L);
        _nview.GetZDO().Set(AssigneeNameKey, "");
    }

    /// <summary>Other names that may open the box (the owner hands out access).</summary>
    public string DelegatesText
    {
        get => Stored(DelegatesKey);
        set => Store(DelegatesKey, value);
    }

    /// <summary>Whether whoever built the box (the postmaster) can open it too.</summary>
    public bool PostmasterOn
    {
        get => _nview == null || !_nview.IsValid() || _nview.GetZDO().GetInt(PostmasterKey, 1) != 0;
        set
        {
            if (_nview == null || !_nview.IsValid())
                return;
            _nview.ClaimOwnership();
            _nview.GetZDO().Set(PostmasterKey, value ? 1 : 0);
        }
    }

    public bool IsBuilder(long playerId) => GetComponent<Piece>() is { } piece && piece.GetCreator() == playerId;

    /// <summary>Who may open a PO box's door: its owner, names the owner added, and the builder while postmaster access is on.</summary>
    public bool MayOpen(long playerId)
    {
        if (playerId == 0)
            return false;
        if (PostmasterOn && IsBuilder(playerId))
            return true;
        if (AssigneeId == 0)
            return false;
        if (AssigneeId == playerId)
            return true;
        var name = Player.GetAllPlayers().FirstOrDefault(p => p != null && p.GetPlayerID() == playerId)?.GetPlayerName();
        return name != null && Tokens(DelegatesText).Contains(name.ToLowerInvariant());
    }

    /// <summary>The owner, the builder or a delegate may change a PO box's settings.</summary>
    public bool CanConfigure(Player player) => !m_singleDoor ? IsBuilder(player.GetPlayerID()) || MayOpen(player.GetPlayerID()) : MayOpen(player.GetPlayerID());

    /// <summary>Whether the send slot is open.</summary>
    public bool AcceptOn
    {
        get => _nview == null || !_nview.IsValid() || _nview.GetZDO().GetInt(AcceptKey, 1) != 0;
        set
        {
            if (_nview == null || !_nview.IsValid())
                return;
            _nview.ClaimOwnership();
            _nview.GetZDO().Set(AcceptKey, value ? 1 : 0);
        }
    }

    /// <summary>True when the box stands inside an active ward (so its back door isn't open to just anyone).</summary>
    private static readonly System.Reflection.MethodInfo? CheckAccess =
        HarmonyLib.AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) });

    /// <summary>Whether this player may open the chest (wards and LockSmith included). The game keeps CheckAccess protected, hence reflection.</summary>
    public bool CanOpen(Player player) =>
        Box != null && (CheckAccess == null || (bool)CheckAccess.Invoke(Box, new object[] { player.GetPlayerID() }));

    public bool IsProtected
    {
        get
        {
            // Read by reflection: the API stubs we build against predate it. Unknown counts as protected (no nagging).
            var check = typeof(PrivateArea).GetMethod("CheckInPrivateArea", new[] { typeof(Vector3), typeof(bool) });
            return check == null || (bool)check.Invoke(null, new object[] { transform.position, false });
        }
    }

    public bool NotifyOn
    {
        get => _nview == null || !_nview.IsValid() || _nview.GetZDO().GetInt(NotifyKey, 1) != 0;
        set
        {
            if (_nview == null || !_nview.IsValid())
                return;
            _nview.ClaimOwnership();
            _nview.GetZDO().Set(NotifyKey, value ? 1 : 0);
        }
    }

    /// <summary>Recent deliveries, newest first: who sent how many stacks, and how long ago.</summary>
    public List<(string From, int Stacks, TimeSpan Ago, string Note)> RecentMail()
    {
        var result = new List<(string, int, TimeSpan, string)>();
        if (_nview == null || !_nview.IsValid())
            return result;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var line in _nview.GetZDO().GetString(LogKey).Split('\n'))
        {
            var parts = line.Split('|');
            if (parts.Length >= 3 && int.TryParse(parts[1], out var stacks) && long.TryParse(parts[2], out var at))
                result.Insert(0, (parts[0], stacks, TimeSpan.FromSeconds(Math.Max(0, now - at)), parts.Length > 3 ? parts[3] : ""));
        }
        return result;
    }

    public void ClearLog()
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(LogKey, "");
    }

    private void RecordDelivery(int stacks, string note)
    {
        var zdo = _nview!.GetZDO();
        var lines = new List<string>(zdo.GetString(LogKey).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        var from = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName().Replace('|', ' ') : "Someone";
        lines.Add(from + "|" + stacks + "|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "|" + note.Replace('|', ' ').Replace('\n', ' '));
        if (lines.Count > LogLines)
            lines.RemoveRange(0, lines.Count - LogLines);
        zdo.Set(LogKey, string.Join("\n", lines));
        zdo.Set(SeqKey, zdo.GetInt(SeqKey) + 1);
    }

    /// <summary>The builder's client says "mail arrived" when a delivery lands (not for deliveries before it loaded).</summary>
    private void WatchDeliveries(ZDO zdo)
    {
        var seq = zdo.GetInt(SeqKey);
        if (_seenSeq >= 0 && seq > _seenSeq && NotifyOn && Player.m_localPlayer != null
            && GetComponent<Piece>() is { } piece && piece.GetCreator() == Player.m_localPlayer.GetPlayerID())
            Trade.TradeNet.Say("You have mail: " + SignName("Mailbox"));
        _seenSeq = seq;
    }

    /// <summary>"Bjorn's Mailbox", or the plain piece name when nobody signed it.</summary>
    public string SignName(string plainName)
    {
        if (m_singleDoor)
            return SignRaw.Length > 0 ? SignRaw : "Unassigned PO Box";
        var owner = _nview != null && _nview.IsValid() ? _nview.GetZDO().GetString(OwnerKey) : "";
        return string.IsNullOrEmpty(owner) ? plainName : owner + "'s " + plainName;
    }

    private void Update()
    {
        if (_nview == null || !_nview.IsValid())
            return;

        UpdateWallLink();
        var step = Time.deltaTime / SwingSeconds;
        var zdo = _nview.GetZDO();
        WatchDeliveries(zdo);
        _flapOpen = Mathf.MoveTowards(_flapOpen, zdo.GetInt(FlapKey) == 1 ? 1f : 0f, step);
        _doorOpen = Mathf.MoveTowards(_doorOpen, zdo.GetInt(InUseKey) == 1 ? 1f : 0f, step);
        foreach (var flap in _flap)
            Swing(flap, m_flapHinge, Vector3.right, m_flapAngle * Mathf.SmoothStep(0f, 1f, _flapOpen));
        foreach (var door in _door)
            Swing(door, m_doorHinge, Vector3.up, m_doorAngle * Mathf.SmoothStep(0f, 1f, _doorOpen));
        UpdateFlag(step);
        UpdatePlate();
    }

    /// <summary>The label on the name plate (PO boxes): drawn as 3D text that follows the stored label.</summary>
    private void UpdatePlate()
    {
        if (!m_singleDoor)
            return;
        var label = SignRaw.Length > 0 ? SignRaw : "Unassigned";
        if (label == _plateShown)
            return;
        _plateShown = label;
        if (_plateText == null)
        {
            var go = new GameObject("drake_plate_text");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = m_platePosition;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // readable from the front (+Z)
            _plateText = go.AddComponent<TextMeshPro>();
            _plateText.font = PlateFont();
            _plateText.alignment = TextAlignmentOptions.Center;
            _plateText.enableAutoSizing = true;
            _plateText.fontSizeMin = 0.4f;
            _plateText.fontSizeMax = 1.6f;
            _plateText.enableWordWrapping = true;
            _plateText.rectTransform.sizeDelta = new Vector2(0.16f, 0.044f);
            _plateText.color = new Color(0.94f, 0.9f, 0.82f);
        }
        _plateText.text = label;
    }

    private static TMP_FontAsset? _plateFont;

    private static TMP_FontAsset? PlateFont()
    {
        if (_plateFont != null)
            return _plateFont;
        // The game's own sign font (its signs carry a TextMeshPro), else any font asset that is loaded.
        var sign = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("sign") : null;
        _plateFont = sign != null ? sign.GetComponentInChildren<TMP_Text>(true)?.font : null;
        if (_plateFont == null)
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font.name.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0)
                    return _plateFont = font;
        return _plateFont ??= TMP_Settings.defaultFontAsset;
    }

    /// <summary>Flag down when empty; up and green with mail, red when the box is full.</summary>
    private void UpdateFlag(float step)
    {
        if (_flag.Count == 0 || Box == null)
            return;
        var inventory = Box.GetInventory();
        var hasMail = inventory.GetAllItems().Count > 0;
        var full = inventory.GetEmptySlots() == 0;
        _flagUp = Mathf.MoveTowards(_flagUp, hasMail ? 1f : 0f, step);
        foreach (var flag in _flag)
            Swing(flag, m_flagHinge, Vector3.forward, m_flagAngle * (1f - Mathf.SmoothStep(0f, 1f, _flagUp)));

        var state = !hasMail ? 0 : full ? 2 : 1;
        if (state == _flagState)
            return;
        _flagState = state;
        FlagPaint.SetColor("_Color", full ? new Color(0.85f, 0.12f, 0.1f) : new Color(0.25f, 0.7f, 0.2f));
        foreach (var renderer in _flagRenderers)
            renderer.SetPropertyBlock(FlagPaint);
    }

    /// <summary>Turn a part about a hinge point (both in the piece's own space), whatever its own pivot is.</summary>
    private static void Swing((Transform Part, Vector3 Pos, Quaternion Rot) part, Vector3 hinge, Vector3 axis, float degrees)
    {
        var turn = Quaternion.AngleAxis(degrees, axis);
        part.Part.localPosition = hinge + turn * (part.Pos - hinge);
        part.Part.localRotation = turn * part.Rot;
    }

    /// <summary>True when <paramref name="viewer"/> stands on the public (send) side, the front (+Z).</summary>
    public bool IsPublicSide(Component viewer) =>
        transform.InverseTransformPoint(viewer.transform.position).z > 0f;

    /// <summary>Lift or drop the front flap for everybody (the send window being open).</summary>
    public void SetFlap(bool open)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(FlapKey, open ? 1 : 0);
    }

    /// <summary>Put stacks into the box, from anywhere on the public side. False (nothing moved) when it can't hold them.</summary>
    public bool Deposit(Inventory from, IReadOnlyList<(ItemDrop.ItemData Item, int Amount)> stacks, string note = "")
    {
        var box = Box;
        if (box == null || _nview == null || !_nview.IsValid())
            return false;
        var inventory = box.GetInventory();
        if (inventory.GetEmptySlots() < stacks.Count)
            return false;

        // The box saves itself when its owner's copy changes: become the owner first.
        _nview.ClaimOwnership();
        foreach (var (item, amount) in stacks)
        {
            var copy = item.Clone();
            copy.m_stack = amount;
            if (!inventory.AddItem(copy))
                return false;
            from.RemoveItem(item, amount);
        }
        RecordDelivery(stacks.Count, note);
        return true;
    }
}
