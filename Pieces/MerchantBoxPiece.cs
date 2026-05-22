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
            Category = PieceCategories.Misc,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 10, 2),
                new RequirementConfig("Bronze", 2, 2),
            },
        };

        var customPiece = new CustomPiece(ShopPieceNames.MerchantBox, "piece_chest", pieceConfig);
        if (customPiece.Piece != null)
        {
            var existing = customPiece.Piece.gameObject.GetComponent<MerchantBoxBehaviour>();
            if (existing == null)
                customPiece.Piece.gameObject.AddComponent<MerchantBoxBehaviour>();
        }

        PieceManager.Instance.AddPiece(customPiece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {ShopPieceNames.MerchantBox}");
    }
}
