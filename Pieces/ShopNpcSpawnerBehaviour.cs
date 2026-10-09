using DrakeModsLibs.API;
using DrakesItemShop.Data;
using DrakesItemShop.Npc;
using DrakesItemShop.Runtime;
using UnityEngine;

namespace DrakesItemShop.Pieces;

public class ShopNpcSpawnerBehaviour : MonoBehaviour
{
    public ZNetView m_nview = null!;

    private void Awake() => m_nview = GetComponent<ZNetView>();

    private void Start()
    {
        if (m_nview == null || !m_nview.IsOwner())
            return;

        if (string.IsNullOrEmpty(m_nview.GetZDO()?.GetString(ShopZdoKeys.ShopId)))
            m_nview.GetZDO()?.Set(ShopZdoKeys.ShopId, "silent_haldor");
    }

    public void Interact(Player player)
    {
        if (player == null)
            return;

        string npcId = m_nview?.GetZDO()?.GetString(ShopZdoKeys.ShopId) ?? "silent_haldor";
        if (!ShopRegistry.Instance.TryGetNpc(npcId, out var npc))
        {
            CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, $"Unknown NPC id '{npcId}' in spawner.");
            return;
        }

        NpcTraderSpawner.SpawnAtPlayer(npc, player);
        CustomizeLibsAPI.ShowHudMessage(player, MessageHud.MessageType.Center, $"Spawned trader '{npcId}'.");
    }
}
