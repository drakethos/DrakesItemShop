using UnityEngine;



namespace DrakesItemShop.Runtime;



internal static class VendingInventoryHelper

{

    internal static int CountItems(Inventory? inventory, string itemName)

    {

        if (inventory == null)

            return 0;

        return inventory.CountItems(itemName);

    }



    internal static bool CanAdd(Inventory? inventory, ItemDrop.ItemData template, int quantity)

    {

        if (inventory == null)

            return false;

        return inventory.CanAddItem(template, quantity);

    }



    internal static bool TryAdd(Inventory? inventory, ItemDrop.ItemData template, int quantity)

    {

        if (inventory == null)

            return false;



        for (int i = 0; i < quantity; i++)

        {

            if (!inventory.AddItem(template.Clone()))

                return false;

        }



        return true;

    }



    internal static void RemoveItems(Inventory? inventory, string itemName, int amount)

    {

        if (inventory == null)

            return;



        var drop = ItemResolver.GetItemDrop(itemName);

        if (drop == null)

            return;



        for (int i = 0; i < amount; i++)

            RemoveOneStack(inventory, drop.m_itemData);

    }



    internal static void RemoveOneStack(Inventory inv, ItemDrop.ItemData template)

    {

        foreach (var item in inv.GetAllItems())

        {

            if (item.m_shared.m_name == template.m_shared.m_name)

            {

                inv.RemoveItem(item, 1);

                return;

            }

        }

    }

}


