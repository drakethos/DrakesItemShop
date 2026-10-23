using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>The aprons' pictures, painted in code (no files to ship): the cloth, the leather straps and belt, and the inventory icon.</summary>
internal static class ApronArt
{
    private static readonly Dictionary<ApronStyle, Sprite> Icons = new();
    private static readonly Dictionary<ApronStyle, Texture2D> Cloths = new(), Leathers = new();

    private static readonly Color Brass = new(0.86f, 0.68f, 0.28f);

    private static float Hash(int x, int y)
    {
        var n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xffff) / 65535f;
    }

    /// <summary>Woven cloth with a stitched border, a hem band and (when the style has one) a patch pocket. u across, v up.</summary>
    internal static Texture2D Cloth(ApronStyle style)
    {
        if (Cloths.TryGetValue(style, out var cached) && cached != null)
            return cached;
        const int w = 128, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "drake_apron_cloth_" + style.PrefabName, wrapMode = TextureWrapMode.Clamp };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var weave = ((x / 2 + y / 2) & 1) == 0 ? 1f : 0.93f;
                var c = Color.Lerp(style.ClothDark, style.Cloth, 0.55f + 0.45f * Hash(x / 2, y / 2)) * weave;
                float u = x / (w - 1f), v = y / (h - 1f);

                // Hem band along the bottom with a stitch line above it.
                if (v < 0.07f)
                    c = Color.Lerp(c, style.ClothDark, 0.5f);
                if (Mathf.Abs(v - 0.09f) < 0.008f && (x / 3) % 2 == 0)
                    c = style.Stitch;

                // Stitched border down the sides, and a plain trim stripe outside it when the style has one.
                var edge = Mathf.Min(u, 1f - u);
                if (Mathf.Abs(edge - 0.045f) < 0.008f && (y / 3) % 2 == 0)
                    c = style.Stitch;
                if (style.Trim && edge < 0.03f)
                    c = style.TrimColor;
                if (style.Trim && v < 0.03f)
                    c = style.TrimColor;

                // Brass studs along the border.
                if (style.Studs && (Mathf.Abs(edge - 0.08f) < 0.012f) && y % 10 < 3)
                    c = Brass;

                // Patch pocket.
                if (style.Pocket && u > 0.27f && u < 0.73f && v > 0.16f && v < 0.38f)
                {
                    c = Color.Lerp(c, style.ClothDark, 0.3f);
                    var line = Mathf.Min(Mathf.Min(u - 0.27f, 0.73f - u), Mathf.Min(v - 0.16f, 0.38f - v));
                    if (line < 0.012f)
                        c = style.LeatherLight;
                    else if (Mathf.Abs(line - 0.03f) < 0.005f && ((x + y) / 3) % 2 == 0)
                        c = style.Stitch;
                    if (Mathf.Abs(u - 0.5f) < 0.006f && v < 0.38f)
                        c = Color.Lerp(c, style.Leather, 0.5f);
                }

                c.a = 1f;
                t.SetPixel(x, y, c);
            }
        }
        t.Apply(true);
        return Cloths[style] = t;
    }

    internal static Texture2D Leather(ApronStyle style)
    {
        if (Leathers.TryGetValue(style, out var cached) && cached != null)
            return cached;
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "drake_apron_leather_" + style.PrefabName, wrapMode = TextureWrapMode.Repeat };
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var c = Color.Lerp(style.Leather, style.LeatherLight, Hash(x, y) * 0.8f);
                if (x % 16 == 0 || y % 16 == 0)
                    c *= 0.85f;
                c.a = 1f;
                t.SetPixel(x, y, c);
            }
        }
        t.Apply(true);
        return Leathers[style] = t;
    }

    /// <summary>A little apron in the style's colours: bib (when it has one), skirt, belt and neck loop.</summary>
    internal static Sprite Icon(ApronStyle style)
    {
        if (Icons.TryGetValue(style, out var cached) && cached != null)
            return cached;
        const int s = 128;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "drake_apron_icon_" + style.PrefabName };
        var clear = new Color(0f, 0f, 0f, 0f);
        var bibTop = style.Bib ? 0.86f : 0.5f;
        for (var y = 0; y < s; y++)
        {
            for (var x = 0; x < s; x++)
            {
                float u = x / (s - 1f) - 0.5f, v = y / (s - 1f);
                var c = clear;

                var skirtTop = style.Bib ? 0.52f : 0.55f;
                var skirtHalf = Mathf.Lerp(0.34f, 0.24f, Mathf.InverseLerp(0.08f, skirtTop, v));
                var bibHalf = Mathf.Lerp(0.24f, 0.14f, Mathf.InverseLerp(0.52f, bibTop, v));
                var onSkirt = v >= 0.08f && v < skirtTop && Mathf.Abs(u) < skirtHalf;
                var onBib = style.Bib && v >= 0.52f && v <= bibTop && Mathf.Abs(u) < bibHalf;
                if (onSkirt || onBib)
                {
                    c = Color.Lerp(style.ClothDark, style.Cloth, 0.5f + 0.5f * Hash(x / 2, y / 2));
                    var edge = onSkirt ? skirtHalf - Mathf.Abs(u) : bibHalf - Mathf.Abs(u);
                    if (edge < 0.018f || (onSkirt && v < 0.105f))
                        c = Color.Lerp(c, style.Trim ? style.TrimColor : style.Stitch, 0.7f);
                    if (style.Pocket && Mathf.Abs(u) < 0.13f && v > 0.18f && v < 0.34f && (Mathf.Abs(Mathf.Abs(u) - 0.13f) < 0.014f || Mathf.Abs(v - 0.18f) < 0.014f || Mathf.Abs(v - 0.34f) < 0.014f))
                        c = style.LeatherLight;
                }

                // Belt across the waist.
                if (v > skirtTop - 0.02f && v < skirtTop + 0.04f && Mathf.Abs(u) < skirtHalf + 0.03f)
                    c = style.Leather;
                // Neck loop above the bib.
                if (style.Bib && v >= bibTop - 0.02f && v < 0.98f)
                {
                    var ring = Mathf.Abs(Mathf.Sqrt(u * u + (v - 0.93f) * (v - 0.93f) * 1.4f) - 0.1f);
                    if (ring < 0.018f)
                        c = style.LeatherLight;
                }

                t.SetPixel(x, y, c);
            }
        }
        var copy = t.GetPixels();
        for (var y = 1; y < s - 1; y++)
        {
            for (var x = 1; x < s - 1; x++)
            {
                if (copy[y * s + x].a > 0f)
                    continue;
                if (copy[y * s + x - 1].a > 0.5f || copy[y * s + x + 1].a > 0.5f || copy[(y - 1) * s + x].a > 0.5f || copy[(y + 1) * s + x].a > 0.5f)
                    t.SetPixel(x, y, new Color(0.1f, 0.06f, 0.03f, 0.95f));
            }
        }
        t.Apply();
        return Icons[style] = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }
}
