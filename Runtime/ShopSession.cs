using DrakesItemShop.Schema;
using UnityEngine;

namespace DrakesItemShop.Runtime;

internal enum ShopAnchorType
{
    MerchantBox,
    Ledger,
    NpcTrader,
}

internal sealed class ShopSession
{
    public ShopDefinitionDto Shop = null!;
    public CatalogDto Catalog = null!;
    public ShopAnchorType AnchorType;
    public Vector3 AnchorPosition;
    public ZNetView? NetView;
    public Container? StockContainer;
    public long OwnerUid;
    public string ShopIdOverride = "";
}

internal static class ShopSessionState
{
    public static ShopSession? Current { get; set; }
}
