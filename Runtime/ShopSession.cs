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

    public Inventory? DispenseInventory;

    public Inventory? TreasuryInventory;

    public Inventory? SellHoldInventory;

    public long OwnerUid;

    public string ShopIdOverride = "";



    public bool UsesVendingMachine =>

        AnchorType == ShopAnchorType.MerchantBox &&

        StockContainer != null &&

        TreasuryInventory != null &&

        DispenseInventory != null &&

        string.Equals(Shop.StockSource, "container", System.StringComparison.OrdinalIgnoreCase);

}



internal static class ShopSessionState

{

    public static ShopSession? Current { get; set; }

}


