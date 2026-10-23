using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>Everything the sign editor can change. Stored on the piece's ZDO, so it syncs and survives restarts.</summary>
internal sealed class SignStyle
{
    internal const int MaxText = 140;

    public string Text = "Welcome";
    /// <summary>TMP font asset name; empty = the game's own sign font.</summary>
    public string Font = "";
    public string Color = "#f3e6c4";
    /// <summary>0 left, 1 centre, 2 right.</summary>
    public int Align = 1;
    public bool AutoFit = true;
    /// <summary>Line height in centimetres when <see cref="AutoFit"/> is off.</summary>
    public float SizeCm = 8f;
    public float Outline;
    public string OutlineColor = "#1a1208";
    public bool BothSides = true;
    public float LineSpacing;

    private const string P = "drake_ts_";
    private static int K(string name) => (P + name).GetStableHashCode();

    public static SignStyle Read(ZDO zdo)
    {
        var d = new SignStyle();
        return new SignStyle
        {
            Text = zdo.GetString(K("text"), d.Text),
            Font = zdo.GetString(K("font"), d.Font),
            Color = zdo.GetString(K("color"), d.Color),
            Align = zdo.GetInt(K("align"), d.Align),
            AutoFit = zdo.GetInt(K("fit"), d.AutoFit ? 1 : 0) != 0,
            SizeCm = zdo.GetFloat(K("size"), d.SizeCm),
            Outline = zdo.GetFloat(K("outline"), d.Outline),
            OutlineColor = zdo.GetString(K("outcolor"), d.OutlineColor),
            BothSides = zdo.GetInt(K("both"), d.BothSides ? 1 : 0) != 0,
            LineSpacing = zdo.GetFloat(K("spacing"), d.LineSpacing),
        };
    }

    public void Write(ZDO zdo)
    {
        zdo.Set(K("text"), Text);
        zdo.Set(K("font"), Font);
        zdo.Set(K("color"), Color);
        zdo.Set(K("align"), Align);
        zdo.Set(K("fit"), AutoFit ? 1 : 0);
        zdo.Set(K("size"), SizeCm);
        zdo.Set(K("outline"), Outline);
        zdo.Set(K("outcolor"), OutlineColor);
        zdo.Set(K("both"), BothSides ? 1 : 0);
        zdo.Set(K("spacing"), LineSpacing);
    }

    public SignStyle Clone() => (SignStyle)MemberwiseClone();

    public string Signature() =>
        string.Join("|", Text, Font, Color, Align, AutoFit, SizeCm.ToString("0.###"), Outline.ToString("0.###"), OutlineColor, BothSides, LineSpacing.ToString("0.###"));
}

/// <summary>The fonts the sign can use: every TextMeshPro font asset that is loaded, the game's own sign fonts first.</summary>
internal static class SignFonts
{
    private static List<string>? _names;

    internal static IReadOnlyList<string> Names
    {
        get
        {
            if (_names != null)
                return _names;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .Where(f => f != null && !string.IsNullOrEmpty(f.name))
                .Select(f => f.name)
                .Distinct()
                .OrderBy(n => n.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            all.Insert(0, "");   // "" = default sign font
            return _names = all;
        }
    }

    internal static string Label(string name) => name.Length == 0 ? "Sign font (default)" : name.Replace(" SDF", "");

    internal static TMP_FontAsset? Find(string name)
    {
        if (name.Length > 0)
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.name == name)
                    return font;
        var sign = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("sign") : null;
        var vanilla = sign != null ? sign.GetComponentInChildren<TMP_Text>(true)?.font : null;
        if (vanilla != null)
            return vanilla;
        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (font != null && font.name.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0)
                return font;
        return TMP_Settings.defaultFontAsset;
    }
}

/// <summary>
/// The hanging corner sign: a bracket with a board on chains. The board's text is drawn with TextMeshPro (rich text: bold,
/// italic, colours, sizes) on both faces, and is edited in a proper window (<see cref="TextSignWindow"/>) instead of the game's
/// one-line text box. The board sways gently in the wind.
/// </summary>
public class TextSignBehaviour : MonoBehaviour, IStorefrontUse
{
    // Board face (sign_runes.glb, glTF space): x 0.22..1.22, y -0.66..-0.12, front plane at +z. Text sits just inside the frame.
    private const float FaceCx = 0.72f, FaceCy = -0.39f, FaceW = 0.86f, FaceH = 0.40f, FaceZ = 0.0335f;
    // The chains hang from the bracket arm's underside; the board swings about that line (along X), at this height.
    private static readonly Vector3 SwingHinge = Gl.P(0f, 0.106f, 0f);

    private ZNetView? _nview;
    private HingedParts? _swing;
    private TextMeshPro? _front, _back;
    private SignStyle? _preview;
    private string _shown = "";
    private float _nextCheck;
    private float _phase;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _phase = Mathf.Abs(transform.position.x * 3.1f + transform.position.z * 1.7f);
        var names = new List<string> { "sign_board", "glyphs_open", "glyphs_closed", "COL_board" };
        for (var i = 1; i <= 5; i++)
        {
            names.Add($"chain_L_0{i}");
            names.Add($"chain_R_0{i}");
        }
        _swing = new HingedParts(transform, names.ToArray());
    }

    internal SignStyle Stored => StorefrontZdo.Zdo(_nview) is { } zdo ? SignStyle.Read(zdo) : new SignStyle();

    /// <summary>Show a style without saving it (the editor window calls this as you change things).</summary>
    internal void Preview(SignStyle style)
    {
        _preview = style;
        Refresh(force: true);
    }

    internal void ClearPreview()
    {
        _preview = null;
        Refresh(force: true);
    }

    internal void Save(SignStyle style)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        style.Write(_nview.GetZDO());
        _preview = null;
        Refresh(force: true);
    }

    private void Update()
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        var player = Player.m_localPlayer;
        if (player != null && (player.transform.position - transform.position).sqrMagnitude > 60f * 60f)
            return;

        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + 0.5f;
            Refresh(force: false);
        }
        Sway();
    }

    private void Sway()
    {
        if (_swing == null || _swing.Count == 0)
            return;
        var wind = EnvMan.instance != null ? Mathf.Clamp01(EnvMan.instance.GetWindIntensity()) : 0.2f;
        var t = Time.time;
        var gust = 0.55f + 0.45f * Mathf.PerlinNoise(t * 0.25f, _phase);
        var degrees = (1.2f + 7f * wind) * gust * Mathf.Sin(t * 2.2f + _phase);
        _swing.Turn(SwingHinge, Quaternion.AngleAxis(degrees, Vector3.right));
    }

    private void Refresh(bool force)
    {
        var style = _preview ?? Stored;
        var signature = style.Signature();
        if (!force && signature == _shown)
            return;
        _shown = signature;

        var parent = _swing?.First != null ? _swing.First : transform;
        _front ??= MakeFace(parent, "drake_sign_front", back: false);
        Style(_front, style);
        if (style.BothSides)
        {
            _back ??= MakeFace(parent, "drake_sign_back", back: true);
            _back.gameObject.SetActive(true);
            Style(_back, style);
        }
        else if (_back != null)
        {
            _back.gameObject.SetActive(false);
        }
    }

    private static TextMeshPro MakeFace(Transform parent, string name, bool back)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // The board's front is -Z in Unity (glTF +Z); TextMeshPro text reads from its -Z side, so the front needs no turn.
        go.transform.localPosition = Gl.P(FaceCx, FaceCy, back ? -FaceZ : FaceZ);
        go.transform.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.richText = true;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.rectTransform.sizeDelta = new Vector2(FaceW, FaceH);
        return tmp;
    }

    private static void Style(TextMeshPro tmp, SignStyle style)
    {
        var font = SignFonts.Find(style.Font);
        if (font != null && tmp.font != font)
            tmp.font = font;
        tmp.text = style.Text;
        tmp.alignment = style.Align switch
        {
            0 => TextAlignmentOptions.MidlineLeft,
            2 => TextAlignmentOptions.MidlineRight,
            _ => TextAlignmentOptions.Midline,
        };
        tmp.lineSpacing = style.LineSpacing;
        tmp.color = Parse(style.Color, new Color(0.95f, 0.9f, 0.77f));
        if (style.AutoFit)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.4f;
            tmp.fontSizeMax = 8f;
        }
        else
        {
            tmp.enableAutoSizing = false;
            // World text: one font-size unit is about a tenth of a metre of line height.
            tmp.fontSize = Mathf.Max(0.2f, style.SizeCm / 10f);
        }
        tmp.outlineWidth = Mathf.Clamp01(style.Outline);
        tmp.outlineColor = (Color32)Parse(style.OutlineColor, new Color(0.1f, 0.07f, 0.03f));
    }

    internal static Color Parse(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;


    public string GetHoverName() => global::Localization.instance.Localize("$drake_storesign_name");

    public string GetHoverText()
    {
        var text = global::Localization.instance.Localize("$drake_storesign_name");
        if (StorefrontZdo.MayUse(this, flash: false))
            text += "\n[<color=yellow><b>" + global::Localization.instance.Localize("$KEY_Use") + "</b></color>] Edit sign";
        return text;
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user is not Player)
            return false;
        if (!StorefrontZdo.MayUse(this))
            return true;
        TextSignWindow.Open(this);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    private void OnDestroy()
    {
        TextSignWindow.CloseFor(this);
    }
}
