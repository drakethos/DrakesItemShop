using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Pieces;

internal static class MerchantBoxPiece
{
    public static void Register()
    {
        var pieceConfig = new PieceConfig
        {
            Name = "$drake_merchant_box_name",
            Description = "$drake_merchant_box_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 10, 2),
                new RequirementConfig("Bronze", 2, 2),
            },
        };

        var customPiece = new CustomPiece(ShopPieceNames.MerchantBox, ShopVanillaPrefabs.MerchantBox, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError(
                $"Failed to register {ShopPieceNames.MerchantBox} — clone '{ShopVanillaPrefabs.MerchantBox}' has no Piece (use piece_chest, not Chest).");
            return;
        }

        if (customPiece.Piece.gameObject.GetComponent<MerchantBoxBehaviour>() == null)
            customPiece.Piece.gameObject.AddComponent<MerchantBoxBehaviour>();

        PieceManager.Instance.AddPiece(customPiece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {ShopPieceNames.MerchantBox} (clone: {ShopVanillaPrefabs.MerchantBox})");
    }
}
