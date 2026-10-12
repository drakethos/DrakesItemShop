# Changelog

## 0.4.0
- **Open for business**: display cases, racks and mats can be shops. Owners set prices in Shift+E → Shop; buyers press **E** on priced goods (no chest access needed). Coins go to a till on the piece.
- **Prices**: coins and/or real item trades. Multiple **OR** payment options (e.g. 200 coins **or** 10 Iron + 10 coins). Buyer Gets amount per deal (lot). Search picks real ObjectDB items so names don't mistype.
- **Free-layout mats**: Jute (12), Wolf / Lox (6), Deer (2, half size). Leather mats use the real vanilla rug meshes. Shift+E → Layout to drag, turn, lift and size goods.
- **Display trays**: jute trays plus wolf / lox leather trays (fewer slots).
- Ward / builder manage rights for Shop settings; live stock checks so prices stick and buys don't false "sold out".
- Requires **DrakeModsLibs 1.0.0-beta.2**.

## 0.3.0
- Requires **DrakeModsLibs 1.0.0-beta.2** (the new windows use its UI kit and `DockRight`).
- **Trading menu (beta)**: Shift+T near another player to ask them to trade. WoW-style trade window, both sides confirm, ends if you drift too far apart.
- **Mailbox and Drop Box** (Drake Shop tab): anyone can send up to 3 stacks and a note through the front slot; the back is a 10-slot chest for the owner. Opening flap and door animate, and the cavity is black inside. The send window sits to the right so you can watch the box open. Allow list, item filter and open/closed switch in the Mail settings (Shift+E, or the Lock menu with LockSmith).
- **PO boxes and Post Hall**: PO boxes snap into a post hall. One door per box: the assigned owner and their delegates open it, everyone else sends mail. Name plate, mail flag, postmaster access for the builder.
- **Item rack poses**: weapon poses are tunable per weapon type and per item; common tuned items are built in.
- Display cases and racks no longer reuse the wood chest's icon.
- **Removed the old shop system**: Merchant Box, Shop Sign, Shop Ledger, NPC Spawner and the silent trader, the JSON catalogs/shops/npcs, ward modes, price tags, the `shop_*` console commands and the shop buy/sell windows are gone. Pieces already placed from them disappear. Old files in `BepInEx/config/com.drakesworkshop.itemshop/ItemShop/` are no longer read and can be deleted. Racks, display cases, trading and mail are unchanged.
- README and description rewritten to cover only what the mod offers now (racks, display cases, trading, mail).

## 0.2.0-beta.1
- Beta. Requires **DrakeModsLibs 1.0.0-beta.1** (the trade window uses its new UI kit).
- **Armor, weapon and wall racks** (Drake Shop hammer tab): finewood racks with iron hooks and pegs: free-standing armor racks and weapon racks (6 or 4 slots), which take only armor or only weapons and shields, and wall racks (5 or 3 slots) that take both. What's inside hangs on the rack; weapons and shields are posed like on an item stand. Costs are FineWood plus one BronzeNails per slot, configurable (`03 Pieces`, synced).
- Display cases and racks have their own hammer icons, rendered from the piece, instead of the wood chest's.
- Display cases now have real default costs: big FineWood 10, Red jute 2, Crystal 2; small FineWood 5, Red jute 1, Crystal 1. Configs still on the old placeholder wood costs are moved over automatically; custom costs are kept.

## 0.1.0
First release.
- **Display cases** (Drake Shop hammer tab): a big case (6 slots) and a small one (3 slots), glass and wood in Valheim style.
  - Stock them from the door side: the glass door slides open while the chest is open. From the glass side they're just looked at.
  - What's inside lies on the jute, one spot per slot, flat and sized to fit. Everyone sees the same.
  - Build costs are configurable (`03 Pieces`, synced), wood by default.
- JSON catalogs, shops and NPC definitions (`BepInEx/config/com.drakesworkshop.itemshop/ItemShop/`), created with examples on first run.
- Merchant Box (vending: stock, treasury, sell hold, dispense slot), Shop Sign, Shop Ledger, NPC Spawner.
- Shop UI for buying and selling; server-authoritative transactions (coins plus optional item currencies).
- Silent configurable trader NPC.
- Per-shop ward modes and permissions (none, vanilla, BetterWards, WardIsLove).
- Price tags on purchased items through DrakeModsLibs' display layer.
