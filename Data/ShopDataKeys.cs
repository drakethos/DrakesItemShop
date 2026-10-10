namespace DrakesItemShop.Data;

internal static class ShopZdoKeys
{
    public const string ShopId = "drake_shop_id";
    public const string OwnerUid = "drake_owner_uid";
    public const string LinkLedger = "drake_link_ledger";
    public const string VendingDispense = "drake_vend_dispense";
    public const string VendingTreasury = "drake_vend_treasury";
    public const string VendingSellHold = "drake_vend_sellhold";
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
    public const string DisplayCase = "Drake_DisplayCase";
    public const string DisplayCaseSmall = "Drake_DisplayCaseSmall";
    public const string ArmorRack = "Drake_ArmorRack";
    public const string ArmorRackSmall = "Drake_ArmorRackSmall";
    public const string WeaponRack = "Drake_WeaponRack";
    public const string WeaponRackSmall = "Drake_WeaponRackSmall";
    public const string WallRack = "Drake_WallRack";
    public const string WallRackSmall = "Drake_WallRackSmall";
}

internal static class ShopItemNames
{
    public const string Ledger = "Drake_ShopLedger";
}

/// <summary>Default clone targets — resolved at runtime via <see cref="Runtime.ShopVanillaPrefabResolver"/>.</summary>
internal static class ShopVanillaPrefabs
{
    public static string MerchantBox => Runtime.ShopVanillaPrefabResolver.MerchantBox;
    public static string ShopSign => Runtime.ShopVanillaPrefabResolver.ShopSign;
    public static string NpcSpawner => Runtime.ShopVanillaPrefabResolver.NpcSpawner;
    public static string Ledger => Runtime.ShopVanillaPrefabResolver.Ledger;
    /// <summary>Fixed: the Forge pack (Forge/DisplayCase) is dressed for this chest.</summary>
    public const string DisplayCase = "piece_chest_wood";
}

internal enum LedgerMode
{
    Linked,
    Portable,
}
