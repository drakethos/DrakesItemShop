using DrakeModsLibs.Forge;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;
using Card = DrakesItemShop.DisplayCard.Items.DisplayCard;

namespace DrakesItemShop.DisplayCard;

/// <summary>
/// World card face: UI Toolkit → <see cref="UkTextureSurface"/>, shown on both sloped sides of the tent card so the deal
/// reads from the customer side and the stocker side. Lit like the paper (alpha cutout), dark ink so it reads at a distance.
/// </summary>
internal sealed class DisplayCardFace
{
    // Face quads cover most of each slope; texture matches their aspect so text isn't stretched.
    private const float FaceWidth = Card.Width * 0.92f;
    private const float SlopeFill = 0.9f;
    private const float Lift = 0.004f;
    private const int TexH = 480;
    private static readonly Color Ink = new(0.18f, 0.11f, 0.05f, 1f);
    private static readonly Color InkMuted = new(0.30f, 0.22f, 0.13f, 1f);
    private static readonly Color InkGold = new(0.47f, 0.27f, 0.02f, 1f);

    private readonly UkTextureSurface _surface;
    private readonly Material _material;
    private string _fingerprint = "\0";

    private DisplayCardFace(UkTextureSurface surface, Material material)
    {
        _surface = surface;
        _material = material;
    }

    /// <summary>Adds the face to both slopes of the card mesh under <paramref name="visual"/> (model space).</summary>
    public static DisplayCardFace? Create(Transform visual)
    {
        try
        {
            var slope = new Vector2(Card.RidgeY - Card.FootY, Card.FootZ);
            var slopeLength = slope.magnitude;
            var faceHeight = slopeLength * SlopeFill;
            var texW = Mathf.RoundToInt(TexH * FaceWidth / faceHeight);

            var surface = UkTextureSurface.Create("drake_buy_card_face", texW, TexH);
            surface.Texture.filterMode = FilterMode.Bilinear;
            var root = surface.Root;
            root.style.width = texW;
            root.style.height = TexH;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 8;
            root.pickingMode = PickingMode.Ignore;

            var material = FaceMaterial(surface.Texture);
            var mesh = ForgeSprites.Quad(FaceWidth, faceHeight, false);
            foreach (var side in new[] { 1f, -1f })
            {
                // Up the slope from the foot toward the ridge; out = away from the card's inside.
                var up = new Vector3(0f, slope.x, -side * slope.y).normalized;
                var outward = new Vector3(0f, slope.y, side * slope.x).normalized;
                var foot = new Vector3(0f, Card.FootY, side * Card.FootZ);

                var quad = new GameObject(side > 0 ? "drake_buy_face_front" : "drake_buy_face_back");
                quad.transform.SetParent(visual, false);
                quad.transform.localPosition = foot + up * ((slopeLength - faceHeight) / 2f) + outward * Lift;
                quad.transform.localRotation = Quaternion.LookRotation(outward, up);
                quad.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return new DisplayCardFace(surface, material);
        }
        catch (System.Exception ex)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[DisplayCard] Face create failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>The paper's own lit cutout material with the face texture, so ink is lit like the card it's on.</summary>
    private static Material FaceMaterial(Texture texture)
    {
        var paper = ForgeTextures.Load(typeof(DisplayCardFace).Assembly, "textures/paper_mesh_sheet.png");
        var material = paper != null
            ? new Material(ForgeSprites.Material(paper))
            : new Material(Shader.Find("Sprites/Default"));
        material.name = "drake_buy_face_mat";
        material.mainTexture = texture;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", 0.35f);
        Card.Matte(material);
        return material;
    }

    public void Refresh(BuyOfferEntry offer)
    {
        var fp = $"{offer.WantPrefab}|{offer.WantAmount}|{offer.PayCoins}|{offer.Cap}|{offer.Filled}";
        if (fp == _fingerprint)
            return;
        _fingerprint = fp;

        var root = _surface.Root;
        root.Clear();
        if (!offer.IsValid)
        {
            root.Add(Line("…", 48, InkMuted));
            return;
        }

        var accepting = offer.Accepting;
        root.Add(Line("BUYING", 52, InkMuted));

        // Text takes what it needs (long names step down so a wrap still fits); the icon grows into everything left.
        var name = BuyOffers.ItemLabel(offer.WantPrefab);
        var title = offer.WantAmount == 1 ? name : $"{offer.WantAmount} {name}";
        var titleSize = title.Length <= 10 ? 96 : title.Length <= 14 ? 76 : 58;
        var price = accepting ? $"for {offer.PayCoins} coins" : "not buying";
        var priceSize = price.Length <= 12 ? 84 : 68;

        var image = new VisualElement { pickingMode = PickingMode.Ignore };
        image.style.flexGrow = 1;
        image.style.flexShrink = 1;
        image.style.alignSelf = Align.Stretch;
        image.style.minHeight = 90;
        if (BuyOffers.ItemIcon(offer.WantPrefab) is { } icon)
        {
            image.style.backgroundImage = new StyleBackground(icon);
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            image.style.opacity = accepting ? 1f : 0.45f;
        }
        root.Add(image);

        root.Add(Line(title, titleSize, accepting ? Ink : InkMuted, wrap: true));
        root.Add(Line(price, priceSize, accepting ? InkGold : InkMuted));
        if (offer.Cap > 0)
            root.Add(Line($"{offer.Remaining} of {offer.Cap} wanted", 44, InkMuted));
    }

    private static Label Line(string text, int size, Color color, bool wrap = false)
    {
        var label = UkControls.Text(text, size, color, heading: true, wrap: wrap);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public void Dispose()
    {
        if (_surface != null)
            Object.Destroy(_surface.gameObject);
        if (_material != null)
            Object.Destroy(_material);
    }
}
