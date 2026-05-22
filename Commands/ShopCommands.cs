using DrakesItemShop.Data;
using DrakesItemShop.Npc;
using DrakesItemShop.Runtime;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Commands;

internal static class ShopCommands
{
    public static void Register()
    {
        CommandManager.Instance.AddConsoleCommand(new ReloadCommand());
        CommandManager.Instance.AddConsoleCommand(new SpawnNpcCommand());
        CommandManager.Instance.AddConsoleCommand(new GiveLedgerCommand());
    }

    private sealed class ReloadCommand : ConsoleCommand
    {
        public override string Name => "shop_reload";
        public override string Help => "Reload ItemShop JSON configs";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            ShopRegistry.Instance.Reload(ItemShopPlugin.LogSource!);
            SignCatalogRenderer.RefreshAllSigns();
        }
    }

    private sealed class SpawnNpcCommand : ConsoleCommand
    {
        public override string Name => "shop_spawn_npc";
        public override string Help => "Spawn silent trader — shop_spawn_npc <npcId>";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (args.Length < 2 || !ShopRegistry.Instance.TryGetNpc(args[1], out var npc))
            {
                ItemShopPlugin.LogSource?.LogWarning("Usage: shop_spawn_npc <npcId>");
                return;
            }

            var player = Player.m_localPlayer;
            if (player != null)
                NpcTraderSpawner.SpawnAtPlayer(npc, player);
        }
    }

    private sealed class GiveLedgerCommand : ConsoleCommand
    {
        public override string Name => "shop_give_ledger";
        public override string Help => "Give shop ledger — shop_give_ledger <shopId>";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (player == null || ObjectDB.instance == null)
                return;

            string shopId = args.Length >= 2 ? args[1] : "market_stall";
            var prefab = ObjectDB.instance.GetItemPrefab(ShopItemNames.Ledger);
            var drop = prefab?.GetComponent<ItemDrop>();
            if (drop == null)
                return;

            var item = drop.m_itemData.Clone();
            ShopLinkService.SetPortableLedger(item, shopId);
            player.GetInventory().AddItem(item);
        }
    }
}
