using System;
using System.Linq;
using DrakeModsLibs.Forge;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Vending;

/// <summary>
/// Vending machines (Forge pack in Forge/VendingMachine): server vendors that stand in for the traders. Admins place them;
/// using one opens Valheim's own store with that trader's goods, once the world has reached the trader's biome
/// (<see cref="VendingSettings"/>). Until then it stays dark and is "out of order".
/// </summary>
internal static class VendingPiece
{
    internal sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        /// <summary>The vanilla trader whose goods it sells (prefab name).</summary>
        public string Trader = "";
        public Action<GameObject> Dress = _ => { };
        public Func<string> RequiredKeys = () => "";
    }

    internal static readonly Variant[] Variants =
    {
        new()
        {
            Name = "Drake_VendingHaldor", Token = "drake_vending_haldor", Trader = "Haldor",
            Dress = Items.HaldorSVendingMachine.Dress, RequiredKeys = () => VendingSettings.HaldorKeys,
        },
        new()
        {
            Name = "Drake_VendingHildir", Token = "drake_vending_hildir", Trader = "Hildir",
            Dress = Items.HildirSVendingMachine.Dress, RequiredKeys = () => VendingSettings.HildirKeys,
        },
        new()
        {
            Name = "Drake_VendingBogWitch", Token = "drake_vending_bogwitch", Trader = "BogWitch",
            Dress = Items.BogWitchSVendingMachine.Dress, RequiredKeys = () => VendingSettings.BogWitchKeys,
        },
    };

    private const string Cost = "FineWood:10,Iron:4,Crystal:2";

    internal static bool IsVending(string? prefabName) => Variants.Any(v => v.Name == prefabName);

    internal static Variant? Find(string prefabName) => Variants.FirstOrDefault(v => v.Name == prefabName);

    public static void RegisterAll()
    {
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
            Requirements = DisplayCasePiece.ParseCost(Cost, variant.Name)
                .Select(c => new RequirementConfig(c.Item, c.Amount, 0, true)).ToArray(),
        };

        var customPiece = new CustomPiece(variant.Name, ShopVanillaPrefabs.DisplayCase, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError($"Failed to register {variant.Name}: clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = customPiece.Piece.gameObject;
        // A vendor, not storage: the chest clone's Container stays (Valheim's Hoverable can't be implemented against our
        // references), and StorefrontUse hands its hover and E to VendingBehaviour, so it never opens as a chest.
        if (prefab.GetComponent<Container>() is { } container)
        {
            container.m_name = $"${variant.Token}_name";
            container.m_width = 1;
            container.m_height = 1;
        }
        variant.Dress(prefab);
        // The side wrap borrows window_wood's piece shader, whose texture tiling is for planks: show the image once.
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.HasProperty("_MainTex") && material.mainTexture != null && material.mainTexture.name.StartsWith("vend_"))
                {
                    material.mainTextureScale = Vector2.one;
                    material.mainTextureOffset = Vector2.zero;
                }
        // Runes and lamps: an unlit colour the behaviour swaps for a glowing one when the vendor is in service.
        ForgeLook.Material(prefab, "Glow", null, "Candle_resin_bogwitch", "candle_mat", null, VendingBehaviour.MakeGlow);
        // The window: clear glass instead of the recipe's frosted crystal, so the goods behind it can be seen.
        if (PrefabManager.Cache.GetPrefab<Material>("candle_mat") is { } standard)
        {
            var glass = new Material(standard) { name = "drake_vending_glass" };
            VendingBehaviour.MakeClearGlass(glass);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.EndsWith("_Glass"))
                    renderer.sharedMaterial = glass;
        }

        prefab.AddComponent<VendingBehaviour>();
        PieceManager.Instance.AddPiece(customPiece);
        PieceIcons.Render(customPiece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (trader: {variant.Trader})");
    }
}
