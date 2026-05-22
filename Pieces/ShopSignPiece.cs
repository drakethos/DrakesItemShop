using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace DrakesItemShop.Pieces;

internal static class ShopSignPiece
{
    public static void Register()
    {
        var pieceConfig = new PieceConfig
        {
            Name = "$drake_shop_sign_name",
            Description = "$drake_shop_sign_desc",
            PieceTable = PieceTables.Hammer,
            Category = PieceCategories.Misc,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 5, 2),
            },
        };

        var customPiece = new CustomPiece(ShopPieceNames.ShopSign, "piece_sign", pieceConfig);
        if (customPiece.Piece != null && customPiece.Piece.gameObject.GetComponent<ShopSignBehaviour>() == null)
            customPiece.Piece.gameObject.AddComponent<ShopSignBehaviour>();

        PieceManager.Instance.AddPiece(customPiece);
    }
}
