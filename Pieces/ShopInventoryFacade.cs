using DrakesItemShop.Runtime;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>Opens a bound vending inventory through vanilla Container UI without its own ZDO.</summary>
public class ShopInventoryFacade : MonoBehaviour
{
    public MerchantBoxBehaviour Parent = null!;
    public VendingSlot Slot;
    public string PanelName = "";

    public Inventory? BoundInventory => Parent != null ? Parent.GetVendingInventory(Slot) : null;
}
