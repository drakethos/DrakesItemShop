using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The OPEN / CLOSED door sign. E flips the board over on its peg (everyone nearby sees it turn); the state is a ZDO value,
/// so it persists and syncs. Wards decide who may flip it. The board and both rune sets turn about the rope loop on the peg.
/// </summary>
public class OpenSignBehaviour : MonoBehaviour, IStorefrontUse
{
    private const string OpenKey = "drake_sign_open";
    private const float FlipSeconds = 0.45f;

    // Rope loop on the peg (sign_text.glb: board pivot at glTF (0, 0, 0.0245)); the board turns about the vertical axis through it.
    private static readonly Vector3 Hinge = Gl.P(0f, 0f, 0.0245f);

    private ZNetView? _nview;
    private HingedParts? _board;
    private float _flip;          // 0 = OPEN faces out, 1 = CLOSED faces out
    private float _wobbleStart = -10f;
    private int _shownState = -1;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _board = new HingedParts(transform, "sign_board", "glyphs_open", "glyphs_closed", "COL_board");
        if (_nview != null && _nview.IsValid())
            _nview.Register("RPC_DrakeSignFlipped", RPC_Flipped);
        _flip = IsOpen ? 0f : 1f;
        _shownState = IsOpen ? 1 : 0;   // no rocking just because the sign came into view
        Apply();
    }

    public bool IsOpen => StorefrontZdo.Zdo(_nview) is not { } zdo || zdo.GetInt(StorefrontZdo.Key(OpenKey), 1) != 0;

    private void Update()
    {
        var target = IsOpen ? 0f : 1f;
        if (_shownState != (IsOpen ? 1 : 0))
        {
            // The state changed (our flip or someone else's): start the little swing at the end.
            _shownState = IsOpen ? 1 : 0;
            _wobbleStart = Time.time + FlipSeconds * 0.8f;
        }
        var moved = !Mathf.Approximately(_flip, target);
        var wobbling = Time.time - _wobbleStart < 2.5f;
        if (!moved && !wobbling)
            return;
        _flip = Mathf.MoveTowards(_flip, target, Time.deltaTime / FlipSeconds);
        Apply();
    }

    private void Apply()
    {
        if (_board == null || _board.Count == 0)
            return;
        var angle = 180f * Mathf.SmoothStep(0f, 1f, _flip);
        var t = Time.time - _wobbleStart;
        // After the flip the board rocks against the door a few times, settling.
        var rock = t > 0f && t < 2.5f ? 7f * Mathf.Exp(-2.6f * t) * Mathf.Sin(t * 14f) : 0f;
        var turn = Quaternion.AngleAxis(rock, Vector3.forward) * Quaternion.AngleAxis(angle, Vector3.up);
        _board.Turn(Hinge, turn);
    }


    public string GetHoverName() => global::Localization.instance.Localize("$drake_opensign_name");

    public string GetHoverText()
    {
        var loc = global::Localization.instance;
        var state = IsOpen
            ? "<color=#9fe0a0>OPEN</color>"
            : "<color=#f0a184>CLOSED</color>";
        var text = $"{loc.Localize("$drake_opensign_name")}  {state}";
        if (StorefrontZdo.MayUse(this, flash: false))
            text += $"\n[<color=yellow><b>$KEY_Use</b></color>] Flip to {(IsOpen ? "CLOSED" : "OPEN")}";
        return loc.Localize(text);
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold)
            return false;
        if (_nview == null || !_nview.IsValid())
            return false;
        if (!StorefrontZdo.MayUse(this))
            return true;

        _nview.ClaimOwnership();
        _nview.GetZDO().Set(StorefrontZdo.Key(OpenKey), IsOpen ? 0 : 1);
        _nview.InvokeRPC(ZNetView.Everybody, "RPC_DrakeSignFlipped");
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    // The board flips in silence (the flip sound was dropped); the RPC just wakes every client so they all see it turn.
    private void RPC_Flipped(long sender)
    {
    }
}
