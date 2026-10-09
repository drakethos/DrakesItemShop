using DrakesItemShop.Data;

using DrakesItemShop.Runtime;

using DrakesItemShop.Schema;

using UnityEngine;



namespace DrakesItemShop;



public class MerchantBoxBehaviour : MonoBehaviour

{

    public ZNetView m_nview = null!;

    private Container? _stockContainer;

    private VendingInventoryStore? _vending;

    private Container? _dispenseFacade;

    private Container? _treasuryFacade;

    private Container? _sellHoldFacade;

    private bool _vendingLoaded;



    private void Awake()

    {

        m_nview = GetComponent<ZNetView>();

        _stockContainer = GetComponent<Container>();

        _vending = new VendingInventoryStore(m_nview);

        EnsureFacades();

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



        TryLoadVending();

    }



    private void Update()

    {

        TryLoadVending();

        _vending?.TickSave(Time.deltaTime);

    }



    public string GetShopId() =>

        m_nview?.GetZDO()?.GetString(ShopZdoKeys.ShopId) ?? "market_stall";



    public long GetOwnerUid() =>

        m_nview?.GetZDO()?.GetLong(ShopZdoKeys.OwnerUid) ?? 0;



    public Container? GetStockContainer() => _stockContainer;



    public Inventory? GetDispenseInventory() => _vending?.Dispense;



    public Inventory? GetTreasuryInventory() => _vending?.Treasury;



    public Inventory? GetSellHoldInventory() => _vending?.SellHold;



    public Inventory? GetVendingInventory(VendingSlot slot) => _vending?.Get(slot);



    public VendingInventoryStore? GetVendingStore() => _vending;



    public bool UsesVendingMachine()

    {

        if (_stockContainer == null || _vending == null)

            return false;



        if (!ShopRegistry.Instance.TryGetShop(GetShopId(), out var shop))

            return true;



        return string.Equals(shop.StockSource, "container", System.StringComparison.OrdinalIgnoreCase);

    }



    public bool CanManageStock(Player player)

    {

        if (player == null)

            return false;



        if (ShopPermissions.IsElevated(player))

            return true;



        long owner = GetOwnerUid();

        if (owner != 0 && player.GetPlayerID() == owner)

            return true;



        if (ShopRegistry.Instance.TryGetShop(GetShopId(), out var shop))

            return ShopPermissions.CanManage(player, shop, owner);



        return owner == 0;

    }



    public void OpenStock(Player player) =>

        _stockContainer?.Interact(player, false, false);



    public void OpenDispense(Player player) =>

        _dispenseFacade?.Interact(player, false, false);



    public void OpenTreasury(Player player) =>

        _treasuryFacade?.Interact(player, false, false);



    public void OpenSellHold(Player player) =>

        _sellHoldFacade?.Interact(player, false, false);



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



    internal void NotifyVendingChanged() => _vending?.MarkDirty();



    private void TryLoadVending()

    {

        if (_vendingLoaded || _vending == null || m_nview?.GetZDO() == null)

            return;



        _vending.LoadIfReady();

        _vendingLoaded = true;

    }



    private void EnsureFacades()

    {

        _dispenseFacade = CreateFacade("Drake_DispenseFacade", VendingSlot.Dispense, "$drake_vend_dispense");

        _treasuryFacade = CreateFacade("Drake_TreasuryFacade", VendingSlot.Treasury, "$drake_vend_treasury");

        _sellHoldFacade = CreateFacade("Drake_SellHoldFacade", VendingSlot.SellHold, "$drake_vend_sellhold");

    }



    private Container CreateFacade(string name, VendingSlot slot, string panelName)

    {

        var existing = transform.Find(name);

        if (existing != null && existing.TryGetComponent(out Container existingContainer))

            return existingContainer;



        var go = new GameObject(name);

        go.transform.SetParent(transform, false);

        go.SetActive(true);



        var container = go.AddComponent<Container>();

        GameAccess.SetView(container, m_nview);

        var facade = go.AddComponent<ShopInventoryFacade>();

        facade.Parent = this;

        facade.Slot = slot;

        facade.PanelName = panelName;

        return container;

    }

}


