using System;
using System.Linq;
using DrakeModsLibs.Forge;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The shopfront pieces (Drake Shop hammer tab): a hanging Store Sign with text you edit in a window, an OPEN / CLOSED door sign,
/// a door bell and a desk bell, and the Trade Counter. Models are in Storefront/Generated/Assets/models (the Forge pack in
/// Forge/Storefront has the recipes they were set up with); sounds are in Storefront/Sounds. Each piece is a chest clone dressed
/// the same way the other Drake Shop pieces are.
/// </summary>
internal static class StorefrontPieces
{
    internal const string StoreSign = "Drake_StoreSign";
    internal const string OpenSign = "Drake_OpenSign";
    internal const string DoorBell = "Drake_DoorBell";
    internal const string DeskBell = "Drake_DeskBell";
    internal const string TradeCounter = "Drake_TradeCounter";

    private sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "";
        public float IconYaw = 30f;
        public Action<GameObject, int> Dress = (_, _) => { };
        public Action<GameObject> Behave = _ => { };
    }

    private static readonly Variant[] Variants =
    {
        new()
        {
            Name = StoreSign, Token = "drake_storesign", Cost = "Wood:10,Coal:1,Chain:2", IconYaw = 150f,
            Dress = DressStoreSign,
            Behave = p => Ensure<TextSignBehaviour>(p),
        },
        new()
        {
            Name = OpenSign, Token = "drake_opensign", Cost = "Wood:2,Coal:1,BronzeNails:1", IconYaw = 180f,
            Dress = DressOpenSign,
            Behave = p => Ensure<OpenSignBehaviour>(p),
        },
        new()
        {
            Name = DoorBell, Token = "drake_doorbell", Cost = "Wood:10,Bronze:1", IconYaw = 140f,
            Dress = DressDoorBell,
            Behave = p => Ensure<DoorBellBehaviour>(p),
        },
        new()
        {
            Name = DeskBell, Token = "drake_deskbell", Cost = "Wood:2,Bronze:1", IconYaw = 30f,
            Dress = DressDeskBell,
            Behave = p => Ensure<DeskBellBehaviour>(p),
        },
        new()
        {
            Name = TradeCounter, Token = "drake_tradecounter", Cost = "FineWood:20,BronzeNails:4,Coins:10", IconYaw = 160f,
            Dress = DressTradeCounter,
            Behave = p => Ensure<TradeCounterBehaviour>(p),
        },
    };

    internal static bool IsStorefrontPiece(string? prefabName) =>
        !string.IsNullOrEmpty(prefabName) && Array.Exists(Variants, v => v.Name == prefabName);

    internal static void RegisterAll()
    {
        foreach (var variant in Variants)
        {
            try
            {
                Register(variant);
            }
            catch (Exception ex)
            {
                ItemShopPlugin.LogSource?.LogError($"[Storefront] couldn't register {variant.Name}: {ex}");
            }
        }
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
            ItemShopPlugin.LogSource?.LogError($"[Storefront] {variant.Name}: clone '{ShopVanillaPrefabs.DisplayCase}' has no Piece.");
            return;
        }

        var prefab = piece.Piece.gameObject;
        // The base chest's solid colliders go off (the layer they were on is kept for ours). Every piece keeps the chest's Container: StorefrontUse hands its hover and E to the piece.
        var layer = StorefrontModel.DisableBaseSolids(prefab);
        variant.Dress(prefab, layer);
        // The chest clone refuses sloped and vertical surfaces, which is every wall a sign or bell hangs on (red / invalid).
        if (piece.Piece != null)
        {
            piece.Piece.m_notOnTiltingSurface = false;
            piece.Piece.m_allowRotatedOverlap = true;
        }
        if (prefab.GetComponent<WearNTear>() is { } wear)
            wear.m_supports = false;
        variant.Behave(prefab);

        PieceManager.Instance.AddPiece(piece);
        PieceIcons.Render(piece.Piece, variant.IconYaw);
        ItemShopPlugin.LogSource?.LogInfo($"[Storefront] Registered {variant.Name}");
    }

    private static T Ensure<T>(GameObject prefab) where T : Component => prefab.GetComponent<T>() ?? prefab.AddComponent<T>();

    private static void Tint(GameObject prefab, string target, string fromPrefab, string fromMaterial, string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        ForgeLook.Material(prefab, target, null, fromPrefab, fromMaterial, null, m => m.color = color);
    }

    // ---- Store Sign (sign_runes.glb): the bracket and the board on chains; the board's runes are hidden, text takes their place ----

    private static void DressStoreSign(GameObject prefab, int layer)
    {
        ForgeLook.Model(Owner, prefab, "models/sign_runes.glb");
        Tint(prefab, "Metal_mat", "bar_iron_stack", "iron", "#8A5A28");
        Tint(prefab, "Chain_mat", "bar_iron_stack", "iron", "#B0B8C0");
        ForgeLook.Material(prefab, "Crate_mat", null, "dvergrprops_crate", "Crate_mat", null, null);
        StorefrontModel.ProxiesToColliders(prefab, layer);
        // Custom text replaces the runes on the board.
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            if (StorefrontModel.MeshName(renderer.name) is "glyphs_open" or "glyphs_closed")
                renderer.enabled = false;
        // Snap points as set up in the Forge recipe: the bracket's mounting plate.
        ForgeLook.SnapPoints(prefab, true, new Vector3(0f, 0.3f, -0.045f), new Vector3(0f, 0.3f, 0.045f), new Vector3(0f, -0.3f, 0.045f), new Vector3(0f, -0.3f, -0.045f));
    }

    // ---- Open Sign (sign_text.glb): flips OPEN / CLOSED ----

    private static void DressOpenSign(GameObject prefab, int layer)
    {
        var lit = StorefrontModel.LitTemplate(prefab);
        ForgeLook.Model(Owner, prefab, "models/sign_text.glb");
        ForgeLook.Material(prefab, "Crate_mat", null, "dvergrprops_crate", "Crate_mat", null, null);
        ForgeLook.Material(prefab, "Rope_mat", null, "shipwreck_vikingship_rear", "ship_mat", null, null);
        ForgeLook.Material(prefab, "Metal_mat", null, "BronzeNails", "bronzenails", null, null);
        // OPEN glows (cool white-blue); CLOSED is a dull carving. These are built from the chest's own lit material: left alone they
        // would copy the chest's snow-overlay material, whose shader pushes vertices up and stretches the thin letters into tall slabs.
        StorefrontModel.Solid(prefab, "Rune_glow_mat", lit, new Color(0.7f, 0.9f, 1f), new Color(0.35f, 0.85f, 1.7f));
        StorefrontModel.Solid(prefab, "Rune_dark_mat", lit, new Color(0.12f, 0.1f, 0.08f), null);
        StorefrontModel.ProxiesToColliders(prefab, layer);
        // The collision proxy of the board turns with it. It is symmetrical about the peg, so it also stands in for the whole sign.
        ForgeLook.SnapPoints(prefab, true, new Vector3(-0.25f, -0.37f, 0f), new Vector3(0.25f, -0.37f, 0f), new Vector3(0.25f, 0.03f, 0f), new Vector3(-0.25f, 0.03f, 0f));
    }

    // ---- Door Bell (door_bell.glb) ----

    private static void DressDoorBell(GameObject prefab, int layer)
    {
        ForgeLook.Model(Owner, prefab, "models/door_bell.glb");
        ForgeLook.Material(prefab, "Bronze_mat", null, "BronzeNails", "bronzenails", null, null);
        ForgeLook.Material(prefab, "Crate_mat", null, "dvergrprops_crate", "Crate_mat", null, null);
        StorefrontModel.AddBox(prefab, "drake_collision", Gl.P(0f, 0f, 0.18f), new Vector3(0.08f, 0.32f, 0.36f), layer);
    }

    // ---- Desk Bell (desk_bell.glb) ----

    private static void DressDeskBell(GameObject prefab, int layer)
    {
        ForgeLook.Model(Owner, prefab, "models/desk_bell.glb");
        ForgeLook.Material(prefab, "Bronze_mat", null, "BronzeNails", "bronzenails", null, null);
        ForgeLook.Material(prefab, "Crate_mat", null, "dvergrprops_crate", "Crate_mat", null, null);
        StorefrontModel.AddBox(prefab, "drake_collision", new Vector3(0f, 0.07f, 0f), new Vector3(0.162f, 0.14f, 0.17f), layer);
        ForgeLook.SnapPoints(prefab, true, new Vector3(-0.081f, 0f, -0.085f), new Vector3(0.081f, 0f, -0.085f), new Vector3(0.081f, 0f, 0.085f), new Vector3(-0.081f, 0f, 0.085f));
    }

    // ---- Trade Counter (tradebox.glb) ----

    private static void DressTradeCounter(GameObject prefab, int layer)
    {
        ForgeLook.Model(Owner, prefab, "models/tradebox.glb");
        ForgeLook.Material(prefab, "Bead_mat", null, "AncientGemstoneGreen", "ancient_gemstone_green_mat", null, null);
        ForgeLook.Material(prefab, "Crate_mat", null, "dvergrprops_crate", "Crate_mat", null, null);
        ForgeLook.Material(prefab, "Iron_mat", null, "IronNails", "ironnails", null, null);
        ForgeLook.Material(prefab, "Bronze_mat", null, "BronzeNails", "bronzenails", null, null);
        ForgeLook.Material(prefab, "Gold_mat", null, "Coins", "coin", null, null);

        // One collision box per side (the customer side is -Z, the shop side +Z) so each side has its own hover and E.
        StorefrontModel.AddBox(prefab, "drake_counter_customer", new Vector3(0f, 0.48f, -0.169f), new Vector3(1.2f, 0.96f, 0.338f), layer);
        StorefrontModel.AddBox(prefab, "drake_counter_shop", new Vector3(0f, 0.48f, 0.169f), new Vector3(1.2f, 0.96f, 0.338f), layer);

        if (prefab.GetComponent<Container>() is { } register)
        {
            register.m_name = "$drake_tradecounter_name";
            register.m_width = 6;
            register.m_height = 3;
        }
        ForgeLook.SnapPoints(prefab, true, new Vector3(-0.6f, 0f, -0.338f), new Vector3(0.6f, 0f, -0.338f), new Vector3(0.6f, 0f, 0.338f), new Vector3(-0.6f, 0f, 0.338f));
    }

    private static System.Reflection.Assembly Owner => typeof(StorefrontPieces).Assembly;
}
