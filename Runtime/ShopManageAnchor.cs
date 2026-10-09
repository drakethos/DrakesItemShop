using DrakesItemShop.Pieces;

namespace DrakesItemShop.Runtime;

internal sealed class ShopManageAnchor
{
    public MerchantBoxBehaviour? Box;
    public ShopSignBehaviour? Sign;

    public string Label => Box != null ? "Merchant Box" : "Shop Sign";

    public string GetShopId() =>
        Box != null ? Box.GetShopId() : Sign?.GetShopId() ?? "";

    public void SetShopId(string shopId)
    {
        if (Box != null)
            Box.SetShopId(shopId);
        else
            Sign?.SetShopId(shopId);
    }

    public long GetOwnerUid() => Box?.GetOwnerUid() ?? 0;

    public void AfterShopIdChanged()
    {
        if (Sign != null)
            Sign.RefreshFromCatalog();
    }
}
