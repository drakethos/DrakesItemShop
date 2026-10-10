using System;
using System.Collections.Generic;
using System.Linq;
using DrakesItemShop.Config;
using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Pieces;

/// <summary>
/// Finewood racks: armor and weapon racks (free-standing, big/small) and wall racks (5/3 slots) that take both: chests dressed from the Forge pack in
/// Forge/ItemRack (code generated into ItemRack/Generated). Armor racks only take armor, weapon racks only weapons and
/// shields, wall racks either; what's inside hangs on the rack. Costs come from config and follow it live.
/// </summary>
internal static class ItemRackPiece
{
    internal sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string ConfigKey = "";
        public string DefaultCost = "";
        public ItemRackBehaviour.RackKind Kind;
        /// <summary>Hang points per container row (row 0 first), column 0 = the stocker's left (+X).</summary>
        public Vector3[][] Rows = Array.Empty<Vector3[]>();
        /// <summary>How far below its hang point an item may reach, per row, before it's shrunk.</summary>
        public float[] RowDrop = Array.Empty<float>();
        /// <summary>Front face of the bar or board the hooks sit on (local Z): items never sink behind it.</summary>
        public float Front;
        public Action<GameObject> Dress = _ => { };
    }

    // Hang points as printed by Forge/ItemRack/make_item_rack.py (RACKINFO, Blender (x, y, z) -> Unity (x, z, y)).
    internal static readonly Variant[] Variants =
    {
        new()
        {
            Name = ShopPieceNames.ArmorRack, Token = "drake_armor_rack", ConfigKey = "ArmorRackCost", DefaultCost = "FineWood:12,BronzeNails:6",
            Kind = ItemRackBehaviour.RackKind.Armor, Front = 0.065f, RowDrop = new[] { 0.72f, 0.85f },
            Rows = new[]
            {
                new Vector3[] { new(0.6733f, 1.84f, 0.195f), new(0f, 1.84f, 0.195f), new(-0.6733f, 1.84f, 0.195f) },
                new Vector3[] { new(0.6733f, 1.06f, 0.165f), new(0f, 1.06f, 0.165f), new(-0.6733f, 1.06f, 0.165f) },
            },
            Dress = ItemRack.Items.ArmorRack.Dress,
        },
        new()
        {
            Name = ShopPieceNames.ArmorRackSmall, Token = "drake_armor_rack_small", ConfigKey = "SmallArmorRackCost", DefaultCost = "FineWood:8,BronzeNails:4",
            Kind = ItemRackBehaviour.RackKind.Armor, Front = 0.065f, RowDrop = new[] { 0.58f, 0.85f },
            Rows = new[]
            {
                new Vector3[] { new(0.305f, 1.69f, 0.195f), new(-0.305f, 1.69f, 0.195f) },
                new Vector3[] { new(0.305f, 1.06f, 0.165f), new(-0.305f, 1.06f, 0.165f) },
            },
            Dress = ItemRack.Items.SmallArmorRack.Dress,
        },
        new()
        {
            Name = ShopPieceNames.WallRack, Token = "drake_wall_rack", ConfigKey = "WallRackCost", DefaultCost = "FineWood:8,BronzeNails:5",
            Kind = ItemRackBehaviour.RackKind.Any, Front = 0.04f, RowDrop = new[] { 1.1f },
            Rows = new[] { new Vector3[] { new(1.1f, 0f, 0.18f), new(0.55f, 0f, 0.18f), new(0f, 0f, 0.18f), new(-0.55f, 0f, 0.18f), new(-1.1f, 0f, 0.18f) } },
            Dress = ItemRack.Items.WallRack.Dress,
        },
        new()
        {
            Name = ShopPieceNames.WallRackSmall, Token = "drake_wall_rack_small", ConfigKey = "SmallWallRackCost", DefaultCost = "FineWood:5,BronzeNails:3",
            Kind = ItemRackBehaviour.RackKind.Any, Front = 0.04f, RowDrop = new[] { 1.1f },
            Rows = new[] { new Vector3[] { new(0.55f, 0f, 0.18f), new(0f, 0f, 0.18f), new(-0.55f, 0f, 0.18f) } },
            Dress = ItemRack.Items.SmallWallRack.Dress,
        },
        new()
        {
            Name = ShopPieceNames.WeaponRack, Token = "drake_weapon_rack", ConfigKey = "WeaponRackCost", DefaultCost = "FineWood:10,BronzeNails:6",
            Kind = ItemRackBehaviour.RackKind.Weapon, Front = 0.065f, RowDrop = new[] { 1.1f, 1.1f },
            // Double-sided: row 0 = front forks, row 1 = back forks (z < 0, items face the other way).
            Rows = new[]
            {
                new Vector3[] { new(0.8417f, 1.36f, 0.125f), new(0.505f, 1.36f, 0.125f), new(0.1683f, 1.36f, 0.125f),
                                new(-0.1683f, 1.36f, 0.125f), new(-0.505f, 1.36f, 0.125f), new(-0.8417f, 1.36f, 0.125f) },
                new Vector3[] { new(0.8417f, 1.36f, -0.105f), new(0.505f, 1.36f, -0.105f), new(0.1683f, 1.36f, -0.105f),
                                new(-0.1683f, 1.36f, -0.105f), new(-0.505f, 1.36f, -0.105f), new(-0.8417f, 1.36f, -0.105f) },
            },
            Dress = ItemRack.Items.WeaponRack.Dress,
        },
        new()
        {
            Name = ShopPieceNames.WeaponRackSmall, Token = "drake_weapon_rack_small", ConfigKey = "SmallWeaponRackCost", DefaultCost = "FineWood:7,BronzeNails:4",
            Kind = ItemRackBehaviour.RackKind.Weapon, Front = 0.065f, RowDrop = new[] { 1.1f, 1.1f },
            Rows = new[]
            {
                new Vector3[] { new(0.4575f, 1.36f, 0.125f), new(0.1525f, 1.36f, 0.125f), new(-0.1525f, 1.36f, 0.125f), new(-0.4575f, 1.36f, 0.125f) },
                new Vector3[] { new(0.4575f, 1.36f, -0.105f), new(0.1525f, 1.36f, -0.105f), new(-0.1525f, 1.36f, -0.105f), new(-0.4575f, 1.36f, -0.105f) },
            },
            Dress = ItemRack.Items.SmallWeaponRack.Dress,
        },
    };

    public static void RegisterAll()
    {
        foreach (var variant in Variants)
            Register(variant);
        ItemShopConfig.RackCostChanged += ApplyCosts;
    }

    private static void Register(Variant variant)
    {
        var pieceConfig = new PieceConfig
        {
            Name = $"${variant.Token}_name",
            Description = $"${variant.Token}_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = DisplayCasePiece.ParseCost(ItemShopConfig.RackCost(variant), variant.Name)
                .Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
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

        var behaviour = prefab.GetComponent<ItemRackBehaviour>() ?? prefab.AddComponent<ItemRackBehaviour>();
        behaviour.m_kind = variant.Kind;
        behaviour.m_columns = variant.Rows[0].Length;
        behaviour.m_hangPoints = variant.Rows.SelectMany(row => row).ToArray();
        behaviour.m_rowDrop = variant.RowDrop;
        behaviour.m_front = variant.Front;
        behaviour.m_pitch = variant.Rows[0].Length > 1 ? Mathf.Abs(variant.Rows[0][0].x - variant.Rows[0][1].x) : 0.6f;

        PieceManager.Instance.AddPiece(customPiece);
        PieceIcons.Render(customPiece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (clone: {ShopVanillaPrefabs.DisplayCase})");
    }

    /// <summary>Config changed (or synced from the server): rewrite the registered racks' costs.</summary>
    private static void ApplyCosts()
    {
        if (ObjectDB.instance == null)
            return;
        foreach (var variant in Variants)
        {
            if (PieceManager.Instance.GetPiece(variant.Name)?.Piece is not { } piece)
                continue;
            var resources = new List<Piece.Requirement>();
            foreach (var (item, amount) in DisplayCasePiece.ParseCost(ItemShopConfig.RackCost(variant), variant.Name))
            {
                if (ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() is not { } drop)
                {
                    ItemShopPlugin.LogSource?.LogWarning($"{variant.Name} cost: no item '{item}' (use its prefab name, e.g. FineWood).");
                    continue;
                }
                resources.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_recover = true });
            }
            piece.m_resources = resources.ToArray();
        }
    }
}
