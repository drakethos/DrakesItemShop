using System;
using System.Linq;
using BepInEx.Configuration;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Mail;

/// <summary>
/// The modular post wall (Forge pack in Forge/Mailbox, models from make_postwall.py): sections with PO boxes built in
/// (1x1, a 1x3 column, a 3x1 row), side trims, roof segments and a roof end. Everything sits on one grid, so pieces snap
/// side by side and on top of each other. A section fills its cells with real PO boxes when it is placed (PostSectionBehaviour).
/// </summary>
internal static class PostWallPiece
{
    internal sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "";
        public Action<GameObject> Dress = _ => { };
        /// <summary>Sections only: where each PO box goes (bottom centre, piece space) and the shelf heights.</summary>
        public Vector3[] Cells = Array.Empty<Vector3>();
        public float[] Shelves = Array.Empty<float>();
        public float Width;
    }

    // Grid and cells come from Forge/Mailbox/make_postwall.py (its POSTWALL line).
    private const float CellW = 0.38f, CellH = 0.36f, BoxY = 0.016f, Depth = 0.38f, Board = 0.028f;

    private static Vector3[] Column(int rows) => Enumerable.Range(0, rows).Select(r => new Vector3(0f, BoxY + r * CellH, 0f)).ToArray();
    private static Vector3[] Row(int cols) => Enumerable.Range(0, cols).Select(c => new Vector3((c - (cols - 1) / 2f) * CellW, BoxY, 0f)).ToArray();
    private static float[] ShelfHeights(int rows) => Enumerable.Range(0, rows + 1).Select(j => j * CellH).ToArray();

    internal static readonly Variant[] Variants =
    {
        new() { Name = "Drake_PostSection1x1", Token = "drake_postwall_1x1", Cost = "FineWood:5,BronzeNails:2", Dress = global::DrakesItemShop.Mailbox.Items.PostWall1x1.Dress,
            Cells = Column(1), Shelves = ShelfHeights(1), Width = CellW },
        new() { Name = "Drake_PostSection1x3", Token = "drake_postwall_1x3", Cost = "FineWood:14,BronzeNails:6", Dress = global::DrakesItemShop.Mailbox.Items.PostWall1x3.Dress,
            Cells = Column(3), Shelves = ShelfHeights(3), Width = CellW },
        new() { Name = "Drake_PostSection3x1", Token = "drake_postwall_3x1", Cost = "FineWood:14,BronzeNails:6", Dress = global::DrakesItemShop.Mailbox.Items.PostWall3x1.Dress,
            Cells = Row(3), Shelves = ShelfHeights(1), Width = 3 * CellW },
        new() { Name = "Drake_PostSide1", Token = "drake_postwall_side1", Cost = "FineWood:1", Dress = global::DrakesItemShop.Mailbox.Items.PostWallSide1Tall.Dress },
        new() { Name = "Drake_PostSide3", Token = "drake_postwall_side3", Cost = "FineWood:2", Dress = global::DrakesItemShop.Mailbox.Items.PostWallSide3Tall.Dress },
        new() { Name = "Drake_PostRoof1", Token = "drake_postwall_roof1", Cost = "FineWood:2", Dress = global::DrakesItemShop.Mailbox.Items.PostWallRoof1Wide.Dress },
        new() { Name = "Drake_PostRoof3", Token = "drake_postwall_roof3", Cost = "FineWood:5", Dress = global::DrakesItemShop.Mailbox.Items.PostWallRoof3Wide.Dress },
        new() { Name = "Drake_PostRoofEnd", Token = "drake_postwall_roofend", Cost = "FineWood:1", Dress = global::DrakesItemShop.Mailbox.Items.PostWallRoofEnd.Dress },
    };

    private static ConfigEntry<bool> _enabled = default!;

    /// <summary>Experimental, off by default: the post wall and its PO boxes (prefab Drake_POBox) still need testing.</summary>
    internal static bool Enabled => _enabled != null && _enabled.Value;

    internal static void Bind(ConfigFile config) =>
        _enabled = config.Bind("08 Experimental", "Post wall and PO boxes", false,
            "EXPERIMENTAL, still being tested: the modular post wall (sections with PO boxes built in, sides, roofs). " +
            "Takes effect on restart; every player and the server should use the same setting. " +
            "Turning it off later hides walls already built (their boxes and mail are kept) until it's turned back on.");

    internal static bool IsPostWall(string? prefabName) => Variants.Any(v => v.Name == prefabName);

    public static void RegisterAll()
    {
        if (!Enabled)
        {
            ItemShopPlugin.LogSource?.LogInfo("[PostWall] Post wall and PO boxes are off (08 Experimental).");
            return;
        }
        foreach (var variant in Variants)
            Register(variant);
    }

    private static void Register(Variant variant)
    {
        var pieceConfig = new PieceConfig
        {
            Name = $"${variant.Token}_name",
            Description = $"${variant.Token}_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = DisplayCasePiece.ParseCost(variant.Cost, variant.Name)
                .Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
        };

        var customPiece = new CustomPiece(variant.Name, ShopVanillaPrefabs.DisplayCase, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {variant.Name}: clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = customPiece.Piece.gameObject;
        variant.Dress(prefab);
        // None of these is a chest: the storage lives in the PO boxes.
        if (prefab.GetComponent<Container>() is { } container)
            UnityEngine.Object.DestroyImmediate(container);

        if (variant.Cells.Length > 0)
        {
            AddShelfColliders(prefab, variant);
            var section = prefab.GetComponent<PostSectionBehaviour>() ?? prefab.AddComponent<PostSectionBehaviour>();
            section.m_cells = variant.Cells;
        }

        PieceManager.Instance.AddPiece(customPiece);
        PieceIcons.Render(customPiece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (clone: {ShopVanillaPrefabs.DisplayCase})");
    }

    /// <summary>
    /// The look's collision is only the back board (so the boxes' doors stay free to hover). The shelves get thin colliders
    /// too: the boxes rest on them for support, and sections or roofs stacked on top touch them.
    /// </summary>
    private static void AddShelfColliders(GameObject prefab, Variant variant)
    {
        var layer = prefab.GetComponentInChildren<Collider>(true) is { } existing ? existing.gameObject.layer : prefab.layer;
        for (var i = 0; i < variant.Shelves.Length; i++)
        {
            var go = new GameObject("drake_shelf_" + i) { layer = layer };
            go.transform.SetParent(prefab.transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, variant.Shelves[i], 0f);
            box.size = new Vector3(variant.Width, Board + 0.012f, Depth);
        }
    }
}
