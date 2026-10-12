using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.Forge;
using DrakesItemShop.Config;
using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Pieces;

/// <summary>
/// Glass display cases (big 7x1, small 3x1) and display mats (6x1, 3x1, to lay on a table): chests dressed from the Forge pack in Forge/DisplayCase
/// (code generated into DisplayCase/Generated). Costs come from config and follow it live.
/// </summary>
internal static class DisplayCasePiece
{
    private sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public float Slide;
        public float FloorZ0, FloorZ1;
        public Action<GameObject> Dress = _ => { };
        public Func<string> Cost = () => "";
        // The case's floor by default; the mats lie low and open from any side.
        public float FloorY = 1.038f, FloorX = 0.365f, MaxAcross = 1.15f, MaxHeight = 0.5f;
        public bool AnySide;
        public float IconYaw = -60f;
    }

    // As built by Forge/DisplayCase/make_display_case.py: slide = door opening + 4 cm, floor = the jute cloth's length.
    private static readonly Variant[] Variants =
    {
        new() { Name = ShopPieceNames.DisplayCase, Token = "drake_display_case", Slide = 1.182f, FloorZ0 = -2.224f, FloorZ1 = 1.821f,
                Dress = DisplayCase.Items.DisplayCase.Dress, Cost = () => ItemShopConfig.DisplayCaseCost },
        new() { Name = ShopPieceNames.DisplayCaseSmall, Token = "drake_display_case_small", Slide = 0.79f, FloorZ0 = -1.1715f, FloorZ1 = 0.7685f,
                Dress = DisplayCase.Items.SmallDisplayCase.Dress, Cost = () => ItemShopConfig.SmallDisplayCaseCost },
        // Display mats (Forge/DisplayCase): jute trays hold more; leather trays are the same size but fewer slots.
        new() { Name = ShopPieceNames.DisplayMat, Token = "drake_display_mat", FloorZ0 = -0.845f, FloorZ1 = 0.845f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DisplayCase.Items.DisplayMat.Dress, Cost = () => "FineWood:2,JuteRed:2" },
        new() { Name = ShopPieceNames.DisplayMatSmall, Token = "drake_display_mat_small", FloorZ0 = -0.405f, FloorZ1 = 0.405f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DisplayCase.Items.SmallDisplayMat.Dress, Cost = () => "FineWood:1,JuteRed:1" },
        new() { Name = ShopPieceNames.DisplayMatWolf, Token = "drake_display_mat_wolf", FloorZ0 = -0.845f, FloorZ1 = 0.845f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DressLeatherTray(DisplayCase.Items.DisplayMat.Dress, "rug_wolf", width: 4, height: 1),
                Cost = () => "FineWood:2,WolfPelt:2" },
        new() { Name = ShopPieceNames.DisplayMatLox, Token = "drake_display_mat_lox", FloorZ0 = -0.845f, FloorZ1 = 0.845f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DressLeatherTray(DisplayCase.Items.DisplayMat.Dress, "rug_fur", width: 4, height: 1),
                Cost = () => "FineWood:2,LoxPelt:2" },
        new() { Name = ShopPieceNames.DisplayMatWolfSmall, Token = "drake_display_mat_wolf_small", FloorZ0 = -0.405f, FloorZ1 = 0.405f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DressLeatherTray(DisplayCase.Items.SmallDisplayMat.Dress, "rug_wolf", width: 2, height: 1),
                Cost = () => "FineWood:1,WolfPelt:1" },
        new() { Name = ShopPieceNames.DisplayMatLoxSmall, Token = "drake_display_mat_lox_small", FloorZ0 = -0.405f, FloorZ1 = 0.405f,
                FloorY = 0.026f, FloorX = 0f, MaxAcross = 0.51f, MaxHeight = 0.3f, AnySide = true, IconYaw = 30f,
                Dress = DressLeatherTray(DisplayCase.Items.SmallDisplayMat.Dress, "rug_fur", width: 2, height: 1),
                Cost = () => "FineWood:1,LoxPelt:1" },
    };

    /// <summary>Same tray mesh as jute, then swap the cloth for a vanilla rug look and shrink the inventory.</summary>
    private static Action<GameObject> DressLeatherTray(Action<GameObject> baseDress, string fabricPrefab, int width, int height) =>
        prefab =>
        {
            baseDress(prefab);
            ForgeLook.Material(prefab, "Fabric", null, fabricPrefab, null, null, null);
            if (prefab.GetComponent<Container>() is { } container)
            {
                container.m_width = width;
                container.m_height = height;
            }
        };

    public static void RegisterAll()
    {
        foreach (var variant in Variants)
            Register(variant);
        ItemShopConfig.DisplayCaseCostChanged += ApplyCosts;
    }

    private static void Register(Variant variant)
    {
        var pieceConfig = new PieceConfig
        {
            Name = $"${variant.Token}_name",
            Description = $"${variant.Token}_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = ParseCost(variant.Cost(), variant.Name).Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
        };

        var customPiece = new CustomPiece(variant.Name, ShopVanillaPrefabs.DisplayCase, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {variant.Name} — clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = customPiece.Piece.gameObject;
        variant.Dress(prefab);
        if (prefab.GetComponent<Container>() is { } container)
            container.m_name = $"${variant.Token}_name";
        var behaviour = prefab.GetComponent<DisplayCaseBehaviour>() ?? prefab.AddComponent<DisplayCaseBehaviour>();
        behaviour.m_slideDistance = variant.Slide;
        behaviour.m_floorZ0 = variant.FloorZ0;
        behaviour.m_floorZ1 = variant.FloorZ1;
        behaviour.m_floorY = variant.FloorY;
        behaviour.m_floorX = variant.FloorX;
        behaviour.m_maxAcross = variant.MaxAcross;
        behaviour.m_maxHeight = variant.MaxHeight;
        behaviour.m_anySide = variant.AnySide;

        PieceManager.Instance.AddPiece(customPiece);
        PieceIcons.Render(customPiece.Piece, variant.IconYaw);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (clone: {ShopVanillaPrefabs.DisplayCase})");
    }

    /// <summary>Config changed (or synced from the server): rewrite the registered pieces' costs.</summary>
    private static void ApplyCosts()
    {
        if (ObjectDB.instance == null)
            return;
        foreach (var variant in Variants)
        {
            if (PieceManager.Instance.GetPiece(variant.Name)?.Piece is not { } piece)
                continue;
            var resources = new List<Piece.Requirement>();
            foreach (var (item, amount) in ParseCost(variant.Cost(), variant.Name))
            {
                if (ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() is not { } drop)
                {
                    ItemShopPlugin.LogSource?.LogWarning($"{variant.Name} cost: no item '{item}' (use its prefab name, e.g. Wood).");
                    continue;
                }
                resources.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_recover = true });
            }
            piece.m_resources = resources.ToArray();
        }
    }

    /// <summary>"Wood:10, FineWood:2" → (item, amount) pairs; bad entries are skipped with a warning.</summary>
    internal static List<(string Item, int Amount)> ParseCost(string text, string pieceName)
    {
        var result = new List<(string, int)>();
        foreach (var entry in (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            var item = parts[0].Trim();
            var amount = 1;
            if (item.Length == 0 || (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out amount)) || amount < 1)
            {
                ItemShopPlugin.LogSource?.LogWarning($"{pieceName} cost: can't read '{entry.Trim()}' (write Item:amount, e.g. Wood:10).");
                continue;
            }
            result.Add((item, amount));
        }
        if (result.Count == 0)
            result.Add(("Wood", 10));
        return result;
    }
}
