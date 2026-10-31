using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrakesItemShop.Mail;
using HarmonyLib;
using DrakesItemShop.Pricing;
using UnityEngine;

namespace DrakesItemShop.Testing;

/// <summary>DrakesTestLab finds methods marked with an attribute named <c>TestHookAttribute</c>. No reference to DrakesTestLab.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TestHookAttribute : Attribute
{
    public TestHookAttribute(string name) => Name = name;
    public string Name { get; }
}

/// <summary>
/// Test hooks for automated ItemShop tests. They run the same calls the Shop tab, price editor and buy window make, as the current
/// player. The shop is a tagged display case / rack / mat (args "target"), else the nearest shop piece.
/// </summary>
internal static class ItemShopTestHooks
{
    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static string S(Dictionary<string, object?> a, string key, string fallback = "") =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    static int I(Dictionary<string, object?> a, string key, int fallback = 0) =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

    static bool B(Dictionary<string, object?> a, string key, bool fallback = false)
    {
        if (!a.TryGetValue(key, out var v) || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static Container Shop(GameObject? target)
    {
        var c = target != null ? target.GetComponentInChildren<Container>() ?? target.GetComponentInParent<Container>() : null;
        if (c != null && ShopState.IsPiece(c))
            return c;
        Container? best = null;
        var bestD = 30f;
        foreach (var cand in UnityEngine.Object.FindObjectsOfType<Container>())
        {
            if (!ShopState.IsPiece(cand))
                continue;
            var d = Vector3.Distance(cand.transform.position, P.transform.position);
            if (d < bestD)
            {
                best = cand;
                bestD = d;
            }
        }

        return best ?? throw new InvalidOperationException("no shop piece (display case, rack or mat) within 30 m");
    }

    static string PrefabOf(ItemDrop.ItemData i) => i.m_dropPrefab != null ? i.m_dropPrefab.name : i.m_shared?.m_name ?? "";

    static ItemDrop.ItemData? Stock(Container shop, string prefab) =>
        shop.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase) && PayOptions.Has(i))
        ?? shop.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase));

    static List<PayOption> Decode(string raw) =>
        raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(PayOption.Decode).Where(o => o != null).Select(o => o!).ToList();

    /// <summary>May the current player run this shop (price, open, collect)? Builder, or ward-permitted and able to open it.</summary>
    [TestHook("itemshop.canManage")]
    static bool CanManage(GameObject? target = null) => ShopState.CanManage(Shop(target), P);

    /// <summary>
    /// Put goods on the shelf and price them, as the price editor does: prefab, count, price ("c:5", "Wood:10", "c:5|Iron:2" = or),
    /// lot (items per deal, default 1). False when the current player may not manage the shop.
    /// </summary>
    [TestHook("itemshop.stock")]
    static bool StockShop(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var prefab = S(args, "prefab");
        var go = ObjectDB.instance.GetItemPrefab(prefab) ?? throw new InvalidOperationException($"unknown item '{prefab}'");
        var data = go.GetComponent<ItemDrop>().m_itemData.Clone();
        data.m_dropPrefab = go;
        data.m_stack = Math.Max(1, I(args, "count", 1));
        data.m_durability = data.GetMaxDurability();
        if (!shop.GetInventory().AddItem(data))
            throw new InvalidOperationException("the shop has no room");

        var price = S(args, "price");
        if (price.Length > 0)
        {
            var live = Stock(shop, prefab)!;
            PayOptions.Set(live, Decode(price));
            ItemLot.Set(live, Math.Max(1, I(args, "lot", 1)));
        }

        ShopState.NotifyChanged(shop);
        return true;
    }

    /// <summary>
    /// Put a stack from the current player's inventory on the shelf and price it (dragging it into the case, then the price editor):
    /// prefab, index (nth stack), count (default all), price. Keeps everything on the item (custom name, look, crafter).
    /// </summary>
    [TestHook("itemshop.putIn")]
    static bool PutIn(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var prefab = S(args, "prefab");
        var stacks = P.GetInventory().GetAllItems().Where(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase)).ToList();
        var index = I(args, "index");
        if (stacks.Count <= index)
            throw new InvalidOperationException($"no {prefab} in the inventory");
        var item = stacks[index];
        var count = Math.Min(I(args, "count", item.m_stack), item.m_stack);
        var moved = item.Clone();
        moved.m_stack = count;
        if (!shop.GetInventory().AddItem(moved))
            throw new InvalidOperationException("the shop has no room");
        P.GetInventory().RemoveItem(item, count);
        var live = shop.GetInventory().GetAllItems().LastOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase) && !PayOptions.Has(i)) ?? moved;
        var price = S(args, "price");
        if (price.Length > 0)
            PayOptions.Set(live, Decode(price));
        ShopState.NotifyChanged(shop);
        return true;
    }

    /// <summary>Change the price of a stocked item (manager only): prefab, price.</summary>
    [TestHook("itemshop.setPrice")]
    static bool SetPrice(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var item = Stock(shop, S(args, "prefab")) ?? throw new InvalidOperationException("not in stock");
        PayOptions.Set(item, Decode(S(args, "price")));
        ShopState.NotifyChanged(shop);
        return true;
    }

    [TestHook("itemshop.price")]
    static string Price(Dictionary<string, object?> args, GameObject? target = null) =>
        Stock(Shop(target), S(args, "prefab")) is { } item ? PayOptions.Raw(item) : "";

    /// <summary>Open / close for business (the Shop tab's switch). False when the current player may not manage it.</summary>
    [TestHook("itemshop.open")]
    static bool Open(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        ShopState.SetOpen(shop, B(args, "on", true));
        return true;
    }

    [TestHook("itemshop.isOpen")]
    static bool IsOpen(GameObject? target = null) => ShopState.IsOpen(Shop(target));

    /// <summary>Buy as the current player through the buy window's own purchase code: prefab, index (nth priced stack), deals (default 1), option (pay option index), pay (the price the buyer saw; default the current one).
    /// Returns "" when it went through, else the message the buyer sees.</summary>
    [TestHook("itemshop.buy")]
    static string Buy(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        // index: the nth priced stack of that prefab (renamed and plain stacks are separate goods)
        var priced = shop.GetInventory().GetAllItems().Where(i => PrefabOf(i).Equals(S(args, "prefab"), StringComparison.OrdinalIgnoreCase) && PayOptions.Has(i)).ToList();
        var hint = args.ContainsKey("index") ? (I(args, "index") < priced.Count ? priced[I(args, "index")] : null) : Stock(shop, S(args, "prefab"));
        if (hint == null)
            return "Sold out or changed. Look again.";
        return ShopPurchase.Purchase(shop, hint, I(args, "deals", 1), I(args, "option", 0), S(args, "pay", PayOptions.Raw(hint)), P) ?? "";
    }

    /// <summary>How many of the item the shop still has.</summary>
    [TestHook("itemshop.stockCount")]
    static int StockCount(Dictionary<string, object?> args, GameObject? target = null) =>
        Shop(target).GetInventory().GetAllItems().Where(i => PrefabOf(i).Equals(S(args, "prefab"), StringComparison.OrdinalIgnoreCase)).Sum(i => i.m_stack);

    /// <summary>Coins waiting in the till.</summary>
    [TestHook("itemshop.till")]
    static int Till(GameObject? target = null) => ShopState.Till(Shop(target));

    /// <summary>Collect the till into the inventory (manager only, as the Shop tab's Collect). Returns the coins collected, -1 when not allowed.</summary>
    [TestHook("itemshop.collect")]
    static int Collect(GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return -1;
        var coins = ShopState.TakeTill(shop);
        if (coins > 0 && !ShopState.GiveCoins(P.GetInventory(), coins))
            return 0;
        return coins;
    }

    /// <summary>The sales log, newest first: "Buyer: 2 x Item for 10".</summary>
    [TestHook("itemshop.sales")]
    static string Sales(GameObject? target = null) =>
        string.Join(" | ", ShopState.Sales(Shop(target)).Select(s => $"{s.Buyer}: {s.Quantity} x {s.Item} for {s.Coins}"));

    // ---------- mail (mailbox, drop box, PO boxes)

    static MailboxBehaviour Mail(GameObject? target)
    {
        var box = target != null ? target.GetComponentInChildren<MailboxBehaviour>() ?? target.GetComponentInParent<MailboxBehaviour>() : null;
        if (box != null)
            return box;
        MailboxBehaviour? best = null;
        var bestD = 30f;
        foreach (var b in UnityEngine.Object.FindObjectsOfType<MailboxBehaviour>())
        {
            var d = Vector3.Distance(b.transform.position, P.transform.position);
            if (d < bestD) { best = b; bestD = d; }
        }

        return best ?? throw new InvalidOperationException("no mailbox within 30 m");
    }

    /// <summary>The player id DrakesTestLab's playAs gives a name (its fixed Alice/Bob/Carol/Amy, else a stable hash).</summary>
    static long PlayAsId(string name) => name.ToLowerInvariant() switch
    {
        "alice" => 100001,
        "bob" => 100002,
        "carol" => 100003,
        "amy" => 100004,
        _ => 200000 + Math.Abs(name.GetStableHashCode() % 100000)
    };

    /// <summary>Post items as the current player, as the send window does: prefab, count, note. Returns "" when sent, else why not.</summary>
    [TestHook("mail.send")]
    static string MailSend(Dictionary<string, object?> args, GameObject? target = null)
    {
        var box = Mail(target);
        if (!box.MaySend(P))
            return "may not send";
        var prefab = S(args, "prefab");
        var item = P.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"no {prefab} in the inventory");
        if (!box.Accepts(item))
            return "not accepted";
        var count = Math.Min(I(args, "count", item.m_stack), item.m_stack);
        return box.Deposit(P.GetInventory(), new List<(ItemDrop.ItemData, int)> { (item, count) }, S(args, "note")) ? "" : "deposit refused (full?)";
    }

    [TestHook("mail.maySend")] static bool MaySend(GameObject? target = null) => Mail(target).MaySend(P);
    [TestHook("mail.mayOpen")] static bool MayOpen(GameObject? target = null) => Mail(target).MayOpen(P.GetPlayerID());
    [TestHook("mail.canOpen")] static bool CanOpenMail(GameObject? target = null) => Mail(target).CanOpen(P);
    /// <summary>Is the box inside an active ward (so its back door is not open to anyone)?</summary>
    [TestHook("mail.isProtected")] static bool IsProtected(GameObject? target = null) => Mail(target).IsProtected;
    [TestHook("mail.canConfigure")] static bool CanConfigure(GameObject? target = null) => Mail(target).CanConfigure(P);

    [TestHook("mail.accepts")]
    static bool Accepts(Dictionary<string, object?> args, GameObject? target = null)
    {
        var go = ObjectDB.instance.GetItemPrefab(S(args, "prefab")) ?? throw new InvalidOperationException("unknown item");
        return Mail(target).Accepts(go.GetComponent<ItemDrop>().m_itemData);
    }

    /// <summary>Give the box to a player (owner side, as the settings window does): who = me, Alice, Bob...; "" clears it.</summary>
    [TestHook("mail.assign")]
    static bool Assign(Dictionary<string, object?> args, GameObject? target = null)
    {
        var box = Mail(target);
        if (!box.CanConfigure(P))
            return false;
        var who = S(args, "who");
        if (who.Length == 0)
            box.ClearAssignee();
        else if (who.Equals("me", StringComparison.OrdinalIgnoreCase))
            box.Assign(P.GetPlayerID(), P.GetPlayerName());
        else
            box.Assign(args.ContainsKey("id") ? I(args, "id") : PlayAsId(who), who);
        return true;
    }

    [TestHook("mail.setSign")]
    static bool SetSign(Dictionary<string, object?> args, GameObject? target = null)
    {
        var box = Mail(target);
        if (!box.CanConfigure(P))
            return false;
        box.SetSign(S(args, "text"));
        return true;
    }

    /// <summary>The name the box shows (sign / assignee).</summary>
    [TestHook("mail.signName")]
    static string SignName(Dictionary<string, object?> args, GameObject? target = null) => Mail(target).SignName(S(args, "plain"));

    /// <summary>Items inside the box: count of prefab (or every item when prefab is empty).</summary>
    [TestHook("mail.count")]
    static int MailCount(Dictionary<string, object?> args, GameObject? target = null)
    {
        var inv = Mail(target).GetComponent<Container>()?.GetInventory() ?? throw new InvalidOperationException("the box has no inventory");
        var prefab = S(args, "prefab");
        return inv.GetAllItems().Where(i => prefab.Length == 0 || PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase)).Sum(i => i.m_stack);
    }

    /// <summary>Take everything out of the box into the inventory (Take all), when the current player may open it. Returns stacks taken, -1 when not allowed.</summary>
    [TestHook("mail.takeAll")]
    static int TakeAll(GameObject? target = null)
    {
        var box = Mail(target);
        if (!box.CanOpen(P))
            return -1;
        var inv = box.GetComponent<Container>()?.GetInventory() ?? throw new InvalidOperationException("the box has no inventory");
        var taken = 0;
        foreach (var item in inv.GetAllItems().ToList())
        {
            if (!P.GetInventory().AddItem(item.Clone()))
                break;
            inv.RemoveItem(item);
            taken++;
        }

        return taken;
    }

    /// <summary>The mail log: "From: N stacks (note)" newest first.</summary>
    [TestHook("mail.recent")]
    static string Recent(GameObject? target = null) =>
        string.Join(" | ", Mail(target).RecentMail().Select(m => $"{m.From}: {m.Stacks} ({m.Note})"));

    /// <summary>PO boxes a post wall section spawned (target = the section).</summary>
    [TestHook("mail.sectionBoxes")]
    static int SectionBoxes(GameObject? target = null)
    {
        var section = (target != null ? target.GetComponentInChildren<PostSectionBehaviour>() ?? target.GetComponentInParent<PostSectionBehaviour>() : null)
                      ?? UnityEngine.Object.FindObjectsOfType<PostSectionBehaviour>().OrderBy(s => Vector3.Distance(s.transform.position, P.transform.position)).FirstOrDefault()
                      ?? throw new InvalidOperationException("no post wall section nearby");
        return section.Boxes().Count();
    }

    /// <summary>Tag the nth PO box of the nearest section for later steps (returns it; use "as").</summary>
    [TestHook("mail.sectionBox")]
    static GameObject SectionBox(Dictionary<string, object?> args, GameObject? target = null)
    {
        var section = (target != null ? target.GetComponentInChildren<PostSectionBehaviour>() : null)
                      ?? UnityEngine.Object.FindObjectsOfType<PostSectionBehaviour>().OrderBy(s => Vector3.Distance(s.transform.position, P.transform.position)).FirstOrDefault()
                      ?? throw new InvalidOperationException("no post wall section nearby");
        var boxes = section.Boxes().ToList();
        var i = I(args, "index");
        if (boxes.Count <= i)
            throw new InvalidOperationException($"the section has {boxes.Count} PO boxes");
        return boxes[i].gameObject;
    }

    // ---------- storefront

    /// <summary>Store sign text (the sign window's Save): returns the text stored afterwards.</summary>
    [TestHook("store.signText")]
    static string StoreSignText(Dictionary<string, object?> args, GameObject? target = null)
    {
        var sign = (target != null ? target.GetComponentInChildren<Storefront.TextSignBehaviour>() : null)
                   ?? UnityEngine.Object.FindObjectsOfType<Storefront.TextSignBehaviour>().OrderBy(s => Vector3.Distance(s.transform.position, P.transform.position)).FirstOrDefault()
                   ?? throw new InvalidOperationException("no store sign nearby");
        if (args.ContainsKey("text"))
        {
            var style = sign.Stored.Clone();
            AccessTools.Field(style.GetType(), "Text")?.SetValue(style, S(args, "text"));
            sign.Save(style);
        }

        return AccessTools.Field(sign.Stored.GetType(), "Text")?.GetValue(sign.Stored) as string ?? "";
    }

    [TestHook("store.signIsOpen")]
    static bool OpenSignIsOpen(GameObject? target = null)
    {
        var sign = (target != null ? target.GetComponentInChildren<Storefront.OpenSignBehaviour>() : null)
                   ?? UnityEngine.Object.FindObjectsOfType<Storefront.OpenSignBehaviour>().FirstOrDefault()
                   ?? throw new InvalidOperationException("no open sign nearby");
        return sign.IsOpen;
    }

    // ---------- buying from customers (buy offers, sell window)

    static ZDO ShopZdo(Container shop) => shop.GetComponent<ZNetView>()?.GetZDO() ?? throw new InvalidOperationException("shop has no ZDO");

    /// <summary>Add a buy offer (manager only): want (prefab), amount per deal, pay (coins), cap (0 = no limit), budget (coins added).</summary>
    [TestHook("buy.addOffer")]
    static bool AddOffer(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        if (!ShopState.CanManage(shop, P))
            return false;
        var zdo = ShopZdo(shop);
        global::DrakesItemShop.DisplayCard.BuyOffers.Add(zdo, new global::DrakesItemShop.DisplayCard.BuyOfferEntry(
            S(args, "want"), Math.Max(1, I(args, "amount", 1)), I(args, "pay", 1), I(args, "cap", 0)));
        global::DrakesItemShop.DisplayCard.BuyOffers.SetEnabled(zdo, true);
        if (I(args, "budget", 0) > 0)
            global::DrakesItemShop.DisplayCard.BuyOffers.AddBudget(zdo, I(args, "budget", 0));
        return true;
    }

    /// <summary>Sell to the shop as the current player, through the sell window's own code: offer (index), deals. "" = sold, else the refusal.</summary>
    [TestHook("buy.sell")]
    static string Sell(Dictionary<string, object?> args, GameObject? target = null) =>
        global::DrakesItemShop.DisplayCard.ShopSell.Sell(Shop(target), I(args, "offer", 0), I(args, "deals", 1), P) ?? "";

    [TestHook("buy.budget")]
    static int Budget(GameObject? target = null) => global::DrakesItemShop.DisplayCard.BuyOffers.Budget(ShopZdo(Shop(target)));

    [TestHook("buy.held")]
    static int Held(Dictionary<string, object?> args, GameObject? target = null) =>
        global::DrakesItemShop.DisplayCard.BuyOffers.HeldOf(ShopZdo(Shop(target)), S(args, "prefab"));

    /// <summary>The owner collects bought goods into the inventory (manager only); returns how many.</summary>
    [TestHook("buy.collect")]
    static int CollectBought(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        return ShopState.CanManage(shop, P) ? global::DrakesItemShop.DisplayCard.ShopSell.Collect(shop, S(args, "prefab"), P) : -1;
    }

    /// <summary>Is the hammer piece placeable (in the menu and switched on)?</summary>
    [TestHook("itemshop.pieceEnabled")]
    static bool PieceEnabled(Dictionary<string, object?> args)
    {
        var go = ZNetScene.instance.GetPrefab(S(args, "prefab"));
        return go != null && go.GetComponent<global::Piece>() is { } piece && piece.m_enabled;
    }

    /// <summary>May the current player open the shop's chest itself (wards / LockSmith)?</summary>
    [TestHook("itemshop.canOpenChest")]
    static bool CanOpenChest(GameObject? target = null) => ShopState.CanOpen(Shop(target), P);

    // ---------- windows (for the README pictures)

    /// <summary>Do-nothing trade partner for the picture of the trade window.</summary>
    sealed class PicturePartner : global::DrakesItemShop.Trade.ITradePartner
    {
        public string Name { get; }
        public PicturePartner(string name) => Name = name;
        public void SendOffer(IReadOnlyList<global::DrakesItemShop.Trade.TradeEntry> mine, int version) { }
        public void SendAccept(int mineVersion, int theirsVersion) { }
        public void SendCancel() { }
        public void Receive(IReadOnlyList<global::DrakesItemShop.Trade.TradeEntry> items) { }
    }

    /// <summary>Close every open toolkit window (they are not the game's own GUI, so closeUi leaves them).</summary>
    [TestHook("itemshop.ui.closeAll")]
    static bool UiCloseAll()
    {
        var field = typeof(DrakeModsLibs.UI.Toolkit.UkWindow).GetField("OpenWindows", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (field?.GetValue(null) is System.Collections.IEnumerable list)
            foreach (var w in list.Cast<object>().ToList())
                ((DrakeModsLibs.UI.Toolkit.UkWindow)w).CloseSilent();
        return true;
    }

    /// <summary>Open the Shift+E shop menu on a tab: tab = shop | buy | layout.</summary>
    [TestHook("itemshop.ui.shopMenu")]
    static bool UiShopMenu(Dictionary<string, object?> args, GameObject? target = null)
    {
        ShopSettingsWindow.Open(Shop(target), P, S(args, "tab"));
        return true;
    }

    /// <summary>Open the buy window for a stack of the shop.</summary>
    [TestHook("itemshop.ui.buy")]
    static bool UiBuy(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        BuyWindow.Open(shop, Stock(shop, S(args, "prefab")) ?? throw new InvalidOperationException("not in the shop"));
        return true;
    }

    /// <summary>Open the price editor for a stack of the shop.</summary>
    [TestHook("itemshop.ui.price")]
    static bool UiPrice(Dictionary<string, object?> args, GameObject? target = null)
    {
        var shop = Shop(target);
        PriceEditWindow.Open(shop, Stock(shop, S(args, "prefab")) ?? throw new InvalidOperationException("not in the shop"));
        return true;
    }

    /// <summary>Open the mailbox send window.</summary>
    [TestHook("itemshop.ui.mailSend")]
    static bool UiMailSend(GameObject? target = null)
    {
        global::DrakesItemShop.Mail.MailboxSendWindow.Open(Mail(target));
        return true;
    }

    /// <summary>Open the trade window with a stand-in partner: mine = prefab:count list from the inventory, theirs likewise (made up).</summary>
    [TestHook("itemshop.ui.trade")]
    static bool UiTrade(Dictionary<string, object?> args)
    {
        var session = new global::DrakesItemShop.Trade.TradeSession(new PicturePartner(S(args, "partner", "Bjornita")));
        foreach (var part in S(args, "mine").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var item = P.GetInventory().GetAllItems().FirstOrDefault(i => PrefabOf(i).Equals(part.Trim(), StringComparison.OrdinalIgnoreCase));
            if (item != null)
                session.ToggleMine(item);
        }

        var theirs = new List<global::DrakesItemShop.Trade.TradeEntry>();
        foreach (var part in S(args, "theirs").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':');
            var prefab = ZNetScene.instance.GetPrefab(bits[0].Trim())?.GetComponent<ItemDrop>();
            if (prefab == null)
                continue;
            var data = prefab.m_itemData.Clone();
            data.m_dropPrefab = prefab.gameObject;
            data.m_stack = bits.Length > 1 ? int.Parse(bits[1], CultureInfo.InvariantCulture) : 1;
            theirs.Add(new global::DrakesItemShop.Trade.TradeEntry(data, data.m_stack));
        }

        session.SetTheirs(theirs);
        if (B(args, "theyAccept"))
            session.PartnerAccepts();
        global::DrakesItemShop.Trade.TradeWindow.Open(session);
        return true;
    }
}
