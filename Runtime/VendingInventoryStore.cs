using DrakesItemShop.Data;
using UnityEngine;

namespace DrakesItemShop.Runtime;

/// <summary>ZDO-backed treasury, dispense, and sell-hold inventories on a merchant box.</summary>
public sealed class VendingInventoryStore
{
    private readonly ZNetView _nview;
    private float _saveTimer;
    private bool _dirty;

    internal Inventory Dispense { get; }
    internal Inventory Treasury { get; }
    internal Inventory SellHold { get; }

    internal VendingInventoryStore(ZNetView nview)
    {
        _nview = nview;
        Dispense = new Inventory("$drake_vend_dispense", null!, 2, 2);
        Treasury = new Inventory("$drake_vend_treasury", null!, 4, 4);
        SellHold = new Inventory("$drake_vend_sellhold", null!, 4, 3);
    }

    internal void LoadIfReady()
    {
        if (_nview?.GetZDO() == null)
            return;

        LoadOne(ShopZdoKeys.VendingDispense, Dispense);
        LoadOne(ShopZdoKeys.VendingTreasury, Treasury);
        LoadOne(ShopZdoKeys.VendingSellHold, SellHold);
        _dirty = false;
    }

    internal void MarkDirty() => _dirty = true;

    internal void TickSave(float dt)
    {
        if (!_nview.IsOwner() || _nview.GetZDO() == null)
            return;

        _saveTimer += dt;
        if (!_dirty && _saveTimer < 2f)
            return;

        _saveTimer = 0f;
        _dirty = false;
        SaveOne(ShopZdoKeys.VendingDispense, Dispense);
        SaveOne(ShopZdoKeys.VendingTreasury, Treasury);
        SaveOne(ShopZdoKeys.VendingSellHold, SellHold);
    }

    internal Inventory? Get(VendingSlot slot) => slot switch
    {
        VendingSlot.Dispense => Dispense,
        VendingSlot.Treasury => Treasury,
        VendingSlot.SellHold => SellHold,
        _ => null,
    };

    private void LoadOne(string key, Inventory inv)
    {
        byte[]? data = _nview.GetZDO().GetByteArray(key);
        if (data == null || data.Length == 0)
            return;

        var pkg = new ZPackage(data);
        inv.Load(pkg);
    }

    private void SaveOne(string key, Inventory inv)
    {
        var pkg = new ZPackage();
        inv.Save(pkg);
        _nview.GetZDO().Set(key, pkg.GetArray());
    }
}

public enum VendingSlot
{
    Dispense,
    Treasury,
    SellHold,
}
