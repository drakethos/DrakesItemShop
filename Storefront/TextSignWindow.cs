using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The sign editor (Uk toolkit UI): rich text with bold / italic / colour / size, a font list, alignment, text and outline colours,
/// fit-to-board or a fixed height, line spacing and one or both faces. Every change shows on the sign right away; Save keeps it,
/// Cancel (or walking away) puts the old text back.
/// </summary>
internal sealed class TextSignWindow
{
    private const float CloseDistance = 8f;

    private static TextSignWindow? _current;
    private static string _recentColours = "";

    private readonly UkWindow _window;
    private readonly TextSignBehaviour _sign;
    private readonly SignStyle _style;
    private bool _saved;

    internal static bool IsOpen => _current != null && _current._window.IsOpen;

    internal static void CloseFor(TextSignBehaviour sign)
    {
        if (_current != null && _current._sign == sign)
            _current._window.Close();
    }

    internal static void Open(TextSignBehaviour sign)
    {
        _current?._window.CloseSilent();
        _current = new TextSignWindow(sign);
        _current._window.Open();
    }

    private TextSignWindow(TextSignBehaviour sign)
    {
        _sign = sign;
        _style = sign.Stored;
        _window = new UkWindow("drakes_text_sign", 660);
        _window.SetHeader("Store Sign", "Changes show on the sign as you make them");
        var body = _window.Body;

        // ---- text ----
        var text = new UkField("Sign text", multiline: true, rows: 3, tips: _window.Tips);
        text.SetMax(SignStyle.MaxText);
        text.EnableRich();
        text.SetValue(_style.Text);
        text.Changed = raw =>
        {
            _style.Text = raw;
            Push();
        };
        var toolbar = new UkRichToolbar(_window.Tips, _window.Card, () => _recentColours, value => _recentColours = value, ("Sign text", text));
        toolbar.Root.style.marginBottom = 8;
        body.Add(toolbar.Root);
        body.Add(text.Root);
        body.Add(Hint("Select words to make them bold, italic, bigger or a different colour. Enter starts a new line."));

        // ---- font and alignment ----
        var fonts = SignFonts.Names;
        var fontIndex = Mathf.Max(0, fonts.ToList().IndexOf(_style.Font));
        var fontDrop = new UkDropdown(_window.Card, _window.Tips, 300f);
        fontDrop.SetOptions(fonts.Select(SignFonts.Label).ToList(), fontIndex);
        fontDrop.Changed = i =>
        {
            _style.Font = i >= 0 && i < fonts.Count ? fonts[i] : "";
            Push();
        };
        var align = new UkSegmented("Left", "Centre", "Right");
        align.SetIndex(_style.Align);
        align.Changed = i =>
        {
            _style.Align = i;
            Push();
        };
        var fontRow = new VisualElement().Row(Align.Center);
        fontRow.style.marginTop = 16;
        fontRow.Add(Heading("Font"));
        fontRow.Add(UkControls.Spacer());
        fontRow.Add(fontDrop.Root);
        body.Add(fontRow);
        var alignRow = new VisualElement().Row(Align.Center);
        alignRow.style.marginTop = 10;
        alignRow.Add(Heading("Alignment"));
        alignRow.Add(UkControls.Spacer());
        alignRow.Add(align.Root);
        body.Add(alignRow);

        // ---- colours (one picker, aimed at the text or the outline) ----
        var picker = new UkColorPicker();
        picker.SetColor(_style.Color);
        var aim = 0;
        picker.Picked = hex =>
        {
            if (aim == 0)
                _style.Color = hex;
            else
                _style.OutlineColor = hex;
            Push();
        };
        var aimSwitch = new UkSegmented("Text colour", "Outline colour");
        aimSwitch.Changed = i =>
        {
            aim = i;
            picker.SetColor(i == 0 ? _style.Color : _style.OutlineColor);
        };
        var colourHead = new VisualElement().Row(Align.Center);
        colourHead.style.marginTop = 16;
        colourHead.Add(Heading("Colour"));
        colourHead.Add(UkControls.Spacer());
        colourHead.Add(aimSwitch.Root);
        body.Add(colourHead);
        var pickerCard = UkControls.Card().Column().Pad(10);
        pickerCard.style.marginTop = 8;
        pickerCard.Add(picker.Root);
        body.Add(pickerCard);
        body.Add(Hint("Colours you pick inside the text field (above) win over this one. This is the colour of everything else."));

        // ---- size, outline, spacing ----
        var fit = new UkSwitch("Fit text to the board", "Shrinks or grows the text so it always fills the board. Off lets you set the height.");
        var size = new UkSlider("Text height", 2f, 30f, _style.SizeCm, v => $"{v:0} cm");
        fit.Root.style.marginTop = 16;
        size.Root.style.marginTop = 8;
        size.Root.style.display = _style.AutoFit ? DisplayStyle.None : DisplayStyle.Flex;
        fit.SetValue(_style.AutoFit, notify: false);
        fit.Changed = on =>
        {
            _style.AutoFit = on;
            size.Root.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            Push();
        };
        size.Changed = v =>
        {
            _style.SizeCm = v;
            Push();
        };
        body.Add(fit.Root);
        body.Add(size.Root);

        var outline = new UkSlider("Outline", 0f, 0.4f, _style.Outline, v => v < 0.005f ? "Off" : v.ToString("0.00"));
        outline.Root.style.marginTop = 14;
        outline.Changed = v =>
        {
            _style.Outline = v;
            Push();
        };
        body.Add(outline.Root);

        var spacing = new UkSlider("Line spacing", -20f, 40f, _style.LineSpacing, v => v.ToString("0"));
        spacing.Root.style.marginTop = 14;
        spacing.Changed = v =>
        {
            _style.LineSpacing = v;
            Push();
        };
        body.Add(spacing.Root);

        var both = new UkSwitch("Same text on both sides", "Off leaves the back of the board blank.");
        both.Root.style.marginTop = 14;
        both.SetValue(_style.BothSides, notify: false);
        both.Changed = on =>
        {
            _style.BothSides = on;
            Push();
        };
        body.Add(both.Root);

        // ---- footer ----
        _window.Footer.Add(UkControls.MakeButton("Cancel", () => _window.Close(), UkButtonKind.Secondary));
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(UkControls.MakeButton("Reset", () =>
        {
            _window.CloseSilent();
            _sign.ClearPreview();
            Reopen(new SignStyle());
        }, UkButtonKind.Ghost));
        _window.Footer.Add(UkControls.MakeButton("Save", Save, UkButtonKind.Primary, 130));
        _window.ShowFooter(true);

        // Closing any other way (Esc, the X, walking off) drops the preview.
        _window.Closed = () =>
        {
            if (!_saved)
                _sign.ClearPreview();
        };
        _window.Body.schedule.Execute(() =>
        {
            var player = Player.m_localPlayer;
            if (_window.IsOpen && (player == null || Vector3.Distance(player.transform.position, _sign.transform.position) > CloseDistance))
                _window.Close();
        }).Every(500);

        // Show the stored style on the sign straight away (so the preview is the real thing).
        Push();
    }

    private void Reopen(SignStyle fresh)
    {
        // Reset: save the defaults over the sign, then reopen the editor on them.
        _sign.Save(fresh);
        _saved = true;
        Open(_sign);
    }

    private void Push() => _sign.Preview(_style.Clone());

    private void Save()
    {
        _saved = true;
        _window.CloseSilent();
        _sign.Save(_style);
    }

    private static Label Heading(string text) => UkControls.Text(text, 15, UkTheme.Text, heading: true, wrap: false);

    private static Label Hint(string text)
    {
        var hint = UkControls.Text(text, 13, UkTheme.TextMuted);
        hint.style.marginTop = 4;
        return hint;
    }
}
