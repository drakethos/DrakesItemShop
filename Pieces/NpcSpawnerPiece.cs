using DrakesItemShop.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace DrakesItemShop.Pieces;

internal static class NpcSpawnerPiece
{
    public static void Register()
    {
        var pieceConfig = new PieceConfig
        {
            Name = "$drake_shop_npc_spawner_name",
            Description = "$drake_shop_npc_spawner_desc",
            PieceTable = PieceTables.Hammer,
            Category = ShopPieceCategories.DrakeShop,
            Requirements = new[]
            {
                new RequirementConfig("Wood", 5, 2),
                new RequirementConfig("Bronze", 1, 2),
            },
        };

        var customPiece = new CustomPiece(ShopPieceNames.NpcSpawner, ShopVanillaPrefabs.NpcSpawner, pieceConfig);
        if (!customPiece.IsValid())
        {
            ItemShopPlugin.LogSource?.LogError(
                $"Failed to register {ShopPieceNames.NpcSpawner} — clone '{ShopVanillaPrefabs.NpcSpawner}' invalid.");
            return;
        }

        if (customPiece.Piece.gameObject.GetComponent<ShopNpcSpawnerBehaviour>() == null)
            customPiece.Piece.gameObject.AddComponent<ShopNpcSpawnerBehaviour>();

        PieceManager.Instance.AddPiece(customPiece);
        ItemShopPlugin.LogSource?.LogInfo($"Registered piece {ShopPieceNames.NpcSpawner} (clone: {ShopVanillaPrefabs.NpcSpawner})");
    }
}
