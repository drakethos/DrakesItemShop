using System;
using System.Linq;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Mats;

/// <summary>
/// Free-layout mats from the JuteMat Forge pack: same jutemat.glb, Fabric swapped per recipe
/// (jute / wolf / lox / deer). Shift+E → Layout tab.
/// </summary>
internal static class MatPiece
{
    public const string PieceName = "Drake_JuteMat";

    private sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "";
        public Action<GameObject> Dress = _ => { };
        public float Half = MatBehaviour.DefaultHalf;
    }

    private static readonly Variant[] Variants =
    {
        new()
        {
            Name = PieceName, Token = "drake_jutemat", Cost = "JuteRed:4,FineWood:1",
            Dress = global::DrakesItemShop.JuteMat.Items.JuteMat.Dress,
        },
        new()
        {
            Name = "Drake_WolfMat", Token = "drake_wolfmat", Cost = "WolfPelt:3,FineWood:1",
            Dress = global::DrakesItemShop.JuteMat.Items.WolfMat.Dress,
        },
        new()
        {
            Name = "Drake_LoxMat", Token = "drake_loxmat", Cost = "LoxPelt:2,FineWood:1",
            Dress = global::DrakesItemShop.JuteMat.Items.LoxMat.Dress,
        },
        new()
        {
            Name = "Drake_DeerMat", Token = "drake_deermat", Cost = "DeerHide:2,FineWood:1",
            Dress = global::DrakesItemShop.JuteMat.Items.DeerMat.Dress,
            Half = 0.65f,
        },
    };

    public static bool IsMat(string? prefabName) =>
        !string.IsNullOrEmpty(prefabName) && Array.Exists(Variants, v => v.Name == prefabName);

    public static void Register()
    {
        foreach (var variant in Variants)
            Register(variant);
    }

    private static void Register(Variant variant)
    {
        var config = new PieceConfig
        {
            Name = $"${variant.Token}_name",
            Description = $"${variant.Token}_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = DisplayCasePiece.ParseCost(variant.Cost, variant.Name)
                .Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
        };

        var piece = new CustomPiece(variant.Name, ShopVanillaPrefabs.DisplayCase, config);
        if (!piece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {variant.Name}: clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = piece.Piece.gameObject;
        variant.Dress(prefab);
        if (prefab.GetComponent<Container>() is { } container)
            container.m_name = $"${variant.Token}_name";

        var mat = prefab.GetComponent<MatBehaviour>() ?? prefab.AddComponent<MatBehaviour>();
        mat.Half = variant.Half;

        if (prefab.GetComponent<Piece>() is { } pieceComp)
        {
            pieceComp.m_allowRotatedOverlap = true;
            pieceComp.m_notOnTiltingSurface = false;
            TrySetGroundPiece(pieceComp, true);
        }

        PieceManager.Instance.AddPiece(piece);
        // One icon bake for the jute mat; leather variants reuse it (avoids extra RenderManager VRAM).
        if (variant.Name == PieceName)
            PieceIcons.Render(piece.Piece, 25f);
        else if (PieceManager.Instance.GetPiece(PieceName)?.Piece?.m_icon is { } shared)
            piece.Piece.m_icon = shared;
        else
            PieceIcons.Render(piece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (forge Dress)");
    }

    private static void TrySetGroundPiece(Piece piece, bool on)
    {
        var field = HarmonyLib.AccessTools.Field(typeof(Piece), "m_groundPiece");
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(piece, on);
    }
}

/// <summary>Old name — prefer <see cref="MatPiece"/>.</summary>
internal static class JuteMatPiece
{
    public const string PieceName = MatPiece.PieceName;
    public static void Register() => MatPiece.Register();
}
