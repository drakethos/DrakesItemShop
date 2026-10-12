using System;
using System.Linq;
using DrakeModsLibs.Forge;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Mats;

/// <summary>
/// Free-layout display mats. Jute keeps the woven tray mesh; wolf / lox / deer use the real vanilla rug models.
/// </summary>
internal static class MatPiece
{
    /// <summary>Kept for older call sites and world saves that already placed the jute mat.</summary>
    public const string PieceName = "Drake_JuteMat";

    private sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "";
        /// <summary>Vanilla rug prefab to show as the mat (null = keep jute glb from Dress).</summary>
        public string? RugPrefab;
        public int Width = 6;
        public int Height = 2;
        public float Half = MatBehaviour.DefaultHalf;
        public float Footprint = 2f;
    }

    private static readonly Variant[] Variants =
    {
        new()
        {
            Name = PieceName, Token = "drake_jutemat", Cost = "JuteRed:4,FineWood:1",
            RugPrefab = null, Width = 6, Height = 2,
        },
        new()
        {
            Name = "Drake_WolfMat", Token = "drake_wolfmat", Cost = "WolfPelt:3,FineWood:1",
            RugPrefab = "rug_wolf", Width = 3, Height = 2,
        },
        new()
        {
            Name = "Drake_LoxMat", Token = "drake_loxmat", Cost = "LoxPelt:2,FineWood:1",
            RugPrefab = "rug_fur", Width = 3, Height = 2,
        },
        new()
        {
            Name = "Drake_DeerMat", Token = "drake_deermat", Cost = "DeerHide:2,FineWood:1",
            RugPrefab = "rug_deer", Width = 2, Height = 1,
            Half = 0.45f, Footprint = 1f,
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
        var edge = Mathf.Max(0.4f, variant.Footprint);

        if (variant.RugPrefab != null)
        {
            // Real wolf / lox / deer rug mesh (not the jute tray with a tint).
            ApplyMatSettings(prefab, variant);
            ForgeLook.BorrowMesh(prefab, variant.RugPrefab);
            FitVisualToFootprint(prefab, edge);
        }
        else
        {
            global::DrakesItemShop.JuteMat.Items.JuteMat.Dress(prefab);
            ApplyMatSettings(prefab, variant);
        }

        var mat = prefab.GetComponent<MatBehaviour>() ?? prefab.AddComponent<MatBehaviour>();
        mat.Half = variant.Half;

        if (prefab.GetComponent<Piece>() is { } pieceComp)
        {
            pieceComp.m_allowRotatedOverlap = true;
            pieceComp.m_notOnTiltingSurface = false;
            TrySetGroundPiece(pieceComp, true);
        }

        ForgeLook.CollisionBox(prefab, new Vector3(0f, 0.04f, 0f), new Vector3(edge, 0.08f, edge));
        if (variant.RugPrefab == null && Mathf.Abs(edge - 2f) > 0.01f)
        {
            var scale = edge / 2f;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "forge_visual")
                    t.localScale = Vector3.one * scale;
            }
        }

        var snap = edge * 0.5f;
        ForgeLook.SnapPoints(prefab, true,
            new Vector3(-snap, 0f, -snap), new Vector3(-snap, 0f, snap),
            new Vector3(snap, 0f, -snap), new Vector3(snap, 0f, snap));

        PieceManager.Instance.AddPiece(piece);
        PieceIcons.Render(piece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo(
            $"Registered piece {variant.Name} ({variant.Width}x{variant.Height}, {edge:0.#}m, look={variant.RugPrefab ?? "jutemat"})");
    }

    private static void ApplyMatSettings(GameObject prefab, Variant variant)
    {
        if (prefab.GetComponent<Container>() is { } container)
        {
            container.m_name = $"${variant.Token}_name";
            container.m_width = variant.Width;
            container.m_height = variant.Height;
        }
        if (prefab.GetComponent<WearNTear>() is { } wear)
        {
            wear.m_burnable = true;
            wear.m_supports = false;
            wear.m_health = 200f;
        }
    }

    /// <summary>Scale the borrowed rug so its longest ground axis matches <paramref name="edge"/> metres.</summary>
    private static void FitVisualToFootprint(GameObject prefab, float edge)
    {
        Transform? visual = null;
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "forge_visual")
            {
                visual = t;
                break;
            }
        }
        if (visual == null)
            return;

        var rends = visual.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return;
        var world = rends[0].bounds;
        foreach (var r in rends)
            world.Encapsulate(r.bounds);
        var size = Mathf.Max(world.size.x, world.size.z);
        if (size < 0.05f)
            return;
        visual.localScale *= edge / size;

        rends = visual.GetComponentsInChildren<Renderer>(true);
        world = rends[0].bounds;
        foreach (var r in rends)
            world.Encapsulate(r.bounds);
        visual.localPosition += new Vector3(0f, -world.min.y + 0.002f, 0f);
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
