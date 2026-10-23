using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>One apron you can make: its look (painted in code), its shape (full with a bib, or a short waist apron) and its recipe.</summary>
internal sealed class ApronStyle
{
    public string PrefabName = "";
    public string Name = "";
    public string Description = "";
    public string Cost = "";
    public Color Cloth, ClothDark, Stitch, Leather, LeatherLight;
    /// <summary>A bib held by a loop round the neck; false is a waist apron (belt and skirt only).</summary>
    public bool Bib = true;
    /// <summary>Added to the hem length (metres): positive is longer.</summary>
    public float HemExtra;
    public bool Pocket = true;
    public bool Studs;
    public bool Trim;
    public Color TrimColor;
    public int Hash;
}

internal static class ApronStyles
{
    private static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

    internal static readonly ApronStyle[] All =
    {
        new()
        {
            PrefabName = "Drake_ShopApron", Name = "Shopkeeper's Apron",
            Description = "A linen apron with a leather belt and a loop for the neck. Worn over your armor; a shopkeeper carries a little more.",
            Cost = "LinenThread:6,LeatherScraps:4,BronzeNails:2",
            Cloth = C(204, 184, 143), ClothDark = C(168, 148, 110), Stitch = C(84, 51, 28), Leather = C(84, 51, 28), LeatherLight = C(115, 74, 41),
        },
        new()
        {
            PrefabName = "Drake_ShopApronBlack", Name = "Black Apron",
            Description = "A charcoal linen apron with a pale stitched edge, for a shop that keeps its coal dust off its sleeves.",
            Cost = "LinenThread:6,LeatherScraps:4,Coal:2",
            Cloth = C(58, 58, 62), ClothDark = C(38, 38, 42), Stitch = C(190, 188, 180), Leather = C(30, 26, 24), LeatherLight = C(52, 46, 42),
            Trim = true, TrimColor = C(190, 188, 180),
        },
        new()
        {
            PrefabName = "Drake_ShopApronGreen", Name = "Green Apron",
            Description = "A forest-green linen apron with a tan stitched edge.",
            Cost = "LinenThread:6,LeatherScraps:4,Resin:2",
            Cloth = C(70, 104, 62), ClothDark = C(48, 78, 44), Stitch = C(214, 196, 150), Leather = C(84, 56, 32), LeatherLight = C(112, 78, 46),
            Trim = true, TrimColor = C(214, 196, 150),
        },
        new()
        {
            PrefabName = "Drake_ShopApronRed", Name = "Red Apron",
            Description = "A deep red linen apron with a gold stitched edge.",
            Cost = "LinenThread:6,LeatherScraps:4,Raspberry:4",
            Cloth = C(140, 40, 38), ClothDark = C(104, 28, 28), Stitch = C(222, 184, 92), Leather = C(60, 36, 24), LeatherLight = C(88, 56, 36),
            Trim = true, TrimColor = C(222, 184, 92),
        },
        new()
        {
            PrefabName = "Drake_ShopApronCafe", Name = "Barista's Waist Apron",
            Description = "A short brown waist apron with a deep pocket and a tied belt: for pouring and serving, not for forging.",
            Cost = "LinenThread:4,LeatherScraps:2",
            Cloth = C(98, 66, 44), ClothDark = C(74, 48, 30), Stitch = C(230, 214, 178), Leather = C(46, 30, 20), LeatherLight = C(70, 48, 32),
            Bib = false, HemExtra = -0.14f, Trim = true, TrimColor = C(230, 214, 178),
        },
        new()
        {
            PrefabName = "Drake_ShopApronMerchant", Name = "Merchant's Apron",
            Description = "A long tan leather apron with brass studs, a neck loop and a tied belt.",
            Cost = "LeatherScraps:8,BronzeNails:4,LinenThread:4",
            Cloth = C(150, 104, 62), ClothDark = C(116, 78, 44), Stitch = C(58, 36, 20), Leather = C(64, 40, 22), LeatherLight = C(92, 60, 34),
            HemExtra = 0.10f, Studs = true,
        },
    };

    internal static readonly Dictionary<int, ApronStyle> ByHash = new();

    internal static void Index()
    {
        ByHash.Clear();
        foreach (var style in All)
        {
            style.Hash = style.PrefabName.GetStableHashCode();
            ByHash[style.Hash] = style;
        }
    }
}
