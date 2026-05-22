using DrakesItemShop.Data;
using UnityEngine;

namespace DrakesItemShop;

public class MerchantBoxBehaviour : MonoBehaviour
{
    public ZNetView m_nview = null!;
    private Container? _container;

    private void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        _container = GetComponent<Container>();
    }

    private void Start()
    {
        if (m_nview == null || !m_nview.IsOwner())
            return;

        if (string.IsNullOrEmpty(m_nview.GetZDO()?.GetString(ShopZdoKeys.ShopId)))
        {
            SetShopId("market_stall");
            if (Player.m_localPlayer != null)
                SetOwnerUid(Player.m_localPlayer.GetPlayerID());
        }
    }

    public string GetShopId() =>
        m_nview?.GetZDO()?.GetString(ShopZdoKeys.ShopId) ?? "market_stall";

    public long GetOwnerUid() =>
        m_nview?.GetZDO()?.GetLong(ShopZdoKeys.OwnerUid) ?? 0;

    public Container? GetContainer() => _container;

    public void SetShopId(string shopId)
    {
        if (m_nview?.GetZDO() == null || !m_nview.IsOwner())
            return;
        m_nview.GetZDO().Set(ShopZdoKeys.ShopId, shopId);
    }

    public void SetOwnerUid(long uid)
    {
        if (m_nview?.GetZDO() == null || !m_nview.IsOwner())
            return;
        m_nview.GetZDO().Set(ShopZdoKeys.OwnerUid, uid);
    }

    public void LinkLedger(ZNetView ledgerView)
    {
        if (m_nview?.GetZDO() == null || ledgerView == null || !m_nview.IsOwner())
            return;
        m_nview.GetZDO().Set(ShopZdoKeys.LinkLedger, ledgerView.GetZDO()?.m_uid.ToString() ?? "");
    }
}
