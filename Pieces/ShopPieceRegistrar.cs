using DrakesItemShop.Data;
using Jotunn.Managers;

namespace DrakesItemShop.Pieces;

internal static class ShopPieceRegistrar
{
    private static bool _categoryRegistered;

    public static void EnsureHammerCategory()
    {
        if (_categoryRegistered)
            return;

        PieceManager.Instance.AddPieceCategory(ShopPieceCategories.DrakeShop);
        _categoryRegistered = true;
        ItemShopPlugin.LogSource?.LogInfo($"Hammer category registered: {ShopPieceCategories.DrakeShop}");
    }

    public static void RegisterAll()
    {
        EnsureHammerCategory();
        DisplayCasePiece.RegisterAll();
        ItemRackPiece.RegisterAll();
        Mail.MailboxPiece.RegisterAll();
        Mats.MatPiece.Register();
        Mail.PostWallPiece.RegisterAll();
        Trade.TradeDummyPiece.Register();
    }

    public static bool IsDrakeShopPiece(string? prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return false;

        return prefabName == ShopPieceNames.DisplayCase ||
               prefabName == ShopPieceNames.DisplayCaseSmall ||
               prefabName == ShopPieceNames.DisplayMat ||
               prefabName == ShopPieceNames.DisplayMatSmall ||
               prefabName == ShopPieceNames.DisplayMatWolf ||
               prefabName == ShopPieceNames.DisplayMatLox ||
               prefabName == ShopPieceNames.DisplayMatWolfSmall ||
               prefabName == ShopPieceNames.DisplayMatLoxSmall ||
               prefabName == Trade.TradeDummyPiece.PieceName ||
               Mail.MailboxPiece.IsMailbox(prefabName) ||
               Mats.MatPiece.IsMat(prefabName) ||
               Mail.PostWallPiece.IsPostWall(prefabName) ||
               System.Array.Exists(ItemRackPiece.Variants, v => v.Name == prefabName);
    }
}
