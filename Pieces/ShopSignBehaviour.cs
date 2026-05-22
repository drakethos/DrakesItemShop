using DrakesItemShop.Data;
using DrakesItemShop.Runtime;
using UnityEngine;

namespace DrakesItemShop.Pieces;

public class ShopSignBehaviour : MonoBehaviour
{
    public ZNetView m_nview = null!;

    private void Awake() => m_nview = GetComponent<ZNetView>();

    private void Start()
    {
        if (m_nview != null && m_nview.IsOwner() &&
            string.IsNullOrEmpty(m_nview.GetZDO()?.GetString(ShopZdoKeys.ShopId)))
        {
            m_nview.GetZDO()?.Set(ShopZdoKeys.ShopId, "market_stall");
        }

        RefreshFromCatalog();
    }

    public string GetShopId() =>
        m_nview?.GetZDO()?.GetString(ShopZdoKeys.ShopId) ?? "";

    public void RefreshFromCatalog()
    {
        string shopId = GetShopId();
        if (string.IsNullOrWhiteSpace(shopId) ||
            !ShopRegistry.Instance.TryGetShop(shopId, out var shop))
            return;

        var catalog = ShopRegistry.Instance.ResolveCatalogForShop(shop);
        if (catalog == null)
            return;

        SignCatalogRenderer.ApplyToSign(gameObject, shop, catalog);
    }
}
