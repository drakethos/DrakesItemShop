using System;
using System.Linq;
using DrakeModsLibs.Forge;
using DrakesItemShop.Data;
using DrakesItemShop.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Mail;

/// <summary>
/// The drop boxes: a Norse post Mailbox and a tabletop Drop Box (Forge pack in Forge/Mailbox). One chest, two sides: the
/// public (+Z) side has a send slot for up to 3 stacks that go into the box and can't be taken back out from there; the back
/// is an ordinary 10 slot chest door, so wards and LockSmith decide who may open it.
/// </summary>
internal static class MailboxPiece
{
    internal sealed class Variant
    {
        public string Name = "";
        public string Token = "";
        public string Cost = "";
        public Action<GameObject> Dress = _ => { };
        /// <summary>Hinge of the front send flap and the back door, in the piece's own space; angles in degrees (flap about X, door about Y).</summary>
        public Vector3 FlapHinge;
        public float FlapAngle;
        public Vector3 DoorHinge;
        public float DoorAngle;
        /// <summary>The mail flag (post mailbox only): hinge point and how far it is turned down (about Z) when the box is empty.</summary>
        public Vector3 FlagHinge;
        public float FlagAngle;
        /// <summary>A PO box: one door at the front is both the mail slot and the owner's door (no separate back).</summary>
        public bool SingleDoor;
        /// <summary>Where the label goes (the plate), in the piece's space.</summary>
        public Vector3 PlatePosition;
        /// <summary>Not in the build menu: PO boxes only come built into post wall sections (PostWallPiece).</summary>
        public bool Hidden;
    }

    // Hinge points come from Forge/Mailbox/make_mailbox.py (Blender (x, y, z) -> Unity (x, z, y)).
    internal static readonly Variant[] Variants =
    {
        new()
        {
            Name = "Drake_Mailbox", Token = "drake_mailbox", Cost = "FineWood:10,BronzeNails:6",
            Dress = global::DrakesItemShop.Mailbox.Items.Mailbox.Dress,
            FlapHinge = new Vector3(0f, 1.375f, 0.265f), FlapAngle = -68f,
            DoorHinge = new Vector3(-0.26f, 1.205f, -0.241f), DoorAngle = 105f,
            FlagHinge = new Vector3(0.40f, 1.41f, 0f), FlagAngle = -90f,
        },
        new()
        {
            Name = "Drake_DropBox", Token = "drake_dropbox", Cost = "FineWood:5,BronzeNails:4",
            Dress = global::DrakesItemShop.Mailbox.Items.DropBox.Dress,
            FlapHinge = new Vector3(0f, 0.329f, -0.034f), FlapAngle = -72f,
            DoorHinge = new Vector3(-0.13f, 0.15f, -0.152f), DoorAngle = 105f,
        },
        new()
        {
            Name = "Drake_POBox", Token = "drake_pobox", Cost = "FineWood:4,BronzeNails:2",
            Dress = global::DrakesItemShop.Mailbox.Items.POBox.Dress,
            SingleDoor = true, Hidden = true, PlatePosition = new Vector3(0f, 0.235f, 0.18f),
            DoorHinge = new Vector3(-0.17f, 0.15f, 0.162f), DoorAngle = -110f,
            FlagHinge = new Vector3(0.166f, 0.19f, 0.172f), FlagAngle = -90f,
        },
    };

    internal static bool IsMailbox(string? prefabName) => Variants.Any(v => v.Name == prefabName);

    public static void RegisterAll()
    {
        foreach (var variant in Variants)
        {
            // The PO box only exists inside post wall sections, which are experimental (PostWallPiece.Enabled).
            if (variant.Hidden && !PostWallPiece.Enabled)
                continue;
            Register(variant);
        }
    }

    private static void Register(Variant variant)
    {
        var pieceConfig = new PieceConfig
        {
            Name = $"${variant.Token}_name",
            Description = $"${variant.Token}_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Enabled = !variant.Hidden,
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
        // The inside of the cavities: plain black (no wood grain or grate showing through when a door is open).
        ForgeLook.Material(prefab, "Black", null, "wood_window", "window_wood", null, material =>
        {
            material.color = Color.black;
            foreach (var texture in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" })
                if (material.HasProperty(texture))
                    material.SetTexture(texture, null);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
        });
        if (prefab.GetComponent<Container>() is { } container)
            container.m_name = $"${variant.Token}_name";

        var behaviour = prefab.GetComponent<MailboxBehaviour>() ?? prefab.AddComponent<MailboxBehaviour>();
        behaviour.m_singleDoor = variant.SingleDoor;
        behaviour.m_platePosition = variant.PlatePosition;
        behaviour.m_flapHinge = variant.FlapHinge;
        behaviour.m_flapAngle = variant.FlapAngle;
        behaviour.m_doorHinge = variant.DoorHinge;
        behaviour.m_doorAngle = variant.DoorAngle;
        behaviour.m_flagHinge = variant.FlagHinge;
        behaviour.m_flagAngle = variant.FlagAngle;

        PieceManager.Instance.AddPiece(customPiece);
        PieceIcons.Render(customPiece.Piece, 25f);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {variant.Name} (clone: {ShopVanillaPrefabs.DisplayCase})");
    }
}
