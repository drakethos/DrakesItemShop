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
            Category = ShopPieceCategories.DrakeShop,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 5, 2),
            },
        };

        var customPiece = new CustomPiece(ShopPieceNames.ShopSign, ShopVanillaPrefabs.ShopSign, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError(
                $"Failed to register {ShopPieceNames.ShopSign} — clone '{ShopVanillaPrefabs.ShopSign}' invalid.");
            return;
        }

        if (customPiece.Piece.gameObject.GetComponent<ShopSignBehaviour>() == null)
            customPiece.Piece.gameObject.AddComponent<ShopSignBehaviour>();

        PieceManager.Instance.AddPiece(customPiece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {ShopPieceNames.ShopSign} (clone: {ShopVanillaPrefabs.ShopSign})");
    }
}
