using DrakesItemShop.Data;
using DrakesItemShop.Runtime;
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
        ShopVanillaPrefabResolver.ResolveAll(ItemShopPlugin.LogSource!);
        EnsureHammerCategory();
        MerchantBoxPiece.Register();
        ShopSignPiece.Register();
        NpcSpawnerPiece.Register();
        DisplayCasePiece.RegisterAll();
        ItemRackPiece.RegisterAll();
        Trade.TradeDummyPiece.Register();
    }

    public static bool IsDrakeShopPiece(string? prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return false;

        return prefabName == ShopPieceNames.MerchantBox ||
               prefabName == ShopPieceNames.ShopSign ||
               prefabName == ShopPieceNames.NpcSpawner ||
               prefabName == ShopPieceNames.DisplayCase ||
               prefabName == ShopPieceNames.DisplayCaseSmall ||
               prefabName == Trade.TradeDummyPiece.PieceName ||
               System.Array.Exists(ItemRackPiece.Variants, v => v.Name == prefabName);
    }
}
