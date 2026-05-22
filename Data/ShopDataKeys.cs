namespace DrakesItemShop.Data;

internal static class ShopZdoKeys
{
    public const string ShopId = "drake_shop_id";
    public const string OwnerUid = "drake_owner_uid";
    public const string LinkLedger = "drake_link_ledger";
}

internal static class ShopItemKeys
{
    public const string ShopId = "drake_shop_id";
    public const string Portable = "drake_portable";
    public const string LinkBox = "drake_link_box";
    public const string LedgerMode = "drake_ledger_mode";
}

internal static class ShopPieceNames
{
    public const string MerchantBox = "Drake_MerchantBox";
    public const string ShopSign = "Drake_ShopSign";
    public const string NpcSpawner = "Drake_ShopNpcSpawner";
}

internal static class ShopItemNames
{
    public const string Ledger = "Drake_ShopLedger";
}

internal enum LedgerMode
{
    Linked,
    Portable,
}
