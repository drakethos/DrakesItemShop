using System;
using System.Linq;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.OpenCrate;

/// <summary>
/// Placeable open crates on a 1 m footprint / 0.5 m height grid (bottom-center, Y-up).
/// Final lineup: standard, small_low, big_low, tall. Big (⅔) and wide removed.
/// Stand/buy-box crates (same meshes, different behaviour) are tabled.
/// </summary>
internal static class OpenCratePiece
{
    private sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "Wood:5";
        public Action<GameObject> Dress = _ => { };
        public float IconYaw = 35f;
        public float Width = 1f, Depth = 1f, Height = 1f;
        public int InvW = 2, InvH = 2;
        public float FloorY = 0.08f;
        public bool StackRowsUp;
    }

    private static readonly Variant[] Variants =
    {
        new()
        {
            // 1×1×1 — square; long items stick up
            Name = "Drake_OpenCrate", Token = "drake_opencrate",
            Dress = Items.OpenCrate.Dress,
            Width = 1f, Depth = 1f, Height = 1f,
            InvW = 2, InvH = 2,
        },
        new()
        {
            // 1×1×0.5 — long items stick up out of the rim
            Name = "Drake_OpenCrateSmallLow", Token = "drake_opencrate_smalllow",
            Dress = Items.OpenCrateSmallLow.Dress, IconYaw = 40f,
            Width = 1f, Depth = 1f, Height = 0.5f,
            InvW = 2, InvH = 2,
        },
        new()
        {
            // 2×1×0.5 — long crate; long items lie flat along the length
            Name = "Drake_OpenCrateBigLow", Token = "drake_opencrate_biglow",
            Dress = Items.OpenCrateBigLow.Dress, IconYaw = 50f,
            Width = 2f, Depth = 1f, Height = 0.5f,
            InvW = 4, InvH = 2,
        },
        new()
        {
            // 1×1×1.5 — tall; items stand upright
            Name = "Drake_OpenCrateTall", Token = "drake_opencrate_tall",
            Dress = Items.OpenCrateTall.Dress, IconYaw = 20f,
            Width = 1f, Depth = 1f, Height = 1.5f,
            InvW = 2, InvH = 4, StackRowsUp = true,
        },
    };

    public static bool IsOpenCrate(string? prefabName) =>
        !string.IsNullOrEmpty(prefabName) && Array.Exists(Variants, v => v.Name == prefabName);

    public static void RegisterAll()
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
            ItemShopPlugin.LogSource?.LogError(
                $"Failed to register {variant.Name}: clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = piece.Piece.gameObject;
        variant.Dress(prefab);
        OpenCrateShell.Apply(prefab, variant.Width, variant.Depth, variant.Height);
        OpenCrateShell.SnapGrid(prefab, variant.Width, variant.Depth, variant.Height);
        OpenCrateShell.InnerBounds(variant.Width, variant.Depth, out var x0, out var x1, out var z0, out var z1);

        if (prefab.GetComponent<Container>() is { } container)
        {
            container.m_name = $"${variant.Token}_name";
            container.m_width = variant.InvW;
            container.m_height = variant.InvH;
        }

        var behaviour = prefab.GetComponent<OpenCrateBehaviour>() ?? prefab.AddComponent<OpenCrateBehaviour>();
        behaviour.FloorY = variant.FloorY;
        behaviour.InnerX0 = x0;
        behaviour.InnerX1 = x1;
        behaviour.InnerZ0 = z0;
        behaviour.InnerZ1 = z1;
        behaviour.OuterWidth = variant.Width;
        behaviour.OuterDepth = variant.Depth;
        behaviour.OuterHeight = variant.Height;
        behaviour.StackRowsUp = variant.StackRowsUp;

        PieceManager.Instance.AddPiece(piece);
        PieceIcons.Render(piece.Piece, variant.IconYaw);
        ItemShopPlugin.LogSource?.LogInfo(
            $"Registered {variant.Name} ({variant.Width}×{variant.Depth}×{variant.Height}, inv {variant.InvW}×{variant.InvH})");
    }
}
