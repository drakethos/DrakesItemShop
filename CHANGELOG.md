# Changelog

## 0.5.2

Hexium only. Requires **DrakeModsLibs 1.0.0**.

- **Fixed:** the apron fitting-room helper (F8 overlay and Auto-fit) was never meant to ship and is no longer part of release builds. It is kept in the source for Debug builds only.

## 0.5.1

Hexium only. Requires **DrakeModsLibs 1.0.0**.

- **Fixed (mailboxes):** a mailbox outside a ward never warned that anyone can open its back door; it does now ("Not in a ward: anyone can open this back", and the Mail tab says so).
- **Fixed (PO boxes):** assigning a PO box to a nearby player from the Mail tab saved the wrong id, so that player could not open their own box in multiplayer.
- **Fixed:** `AdminOnlyBuildPieces` did nothing on the current game version; non-admins are now stopped from building Drake Shop pieces when it is on.
- **Fixed (chest access):** who may open a shop's or mailbox's chest now follows the ward, LockSmith public and LockSmith guests, the same way pressing E does. Running a shop still needs the ward (a public chest or a guest does not get the Shop controls).
- Comprehensive DrakesTestLab suite in `Tests/` (not part of the release).

## 0.5.0

Release of the 0.5.0 beta, Hexium only. Requires **DrakeModsLibs 1.0.0**.

- **Storefront pieces:** Store Sign (hanging sign; text edited in a rich-text window with fonts, colours, outline, size and both faces), Open Sign (E flips OPEN / CLOSED), Door Bell (jingles and shows "A customer has arrived" when a nearby door opens), Desk Bell (E rings it for everyone nearby) and Trade Counter (two-sided counter: E starts a trade with the person across it, the register chest holds stock and takings, a sale rings the till). New local settings in `08 Storefront`.
- **Shopkeeper's Apron:** a cape-slot cosmetic (worn over armor, other players see it too) with a linen bib and skirt, leather waistband and crossed straps, built on your body when you wear it. Gives "Open for business" (+50 carry weight). Workbench: Linen Thread 6, Leather Scraps 4, Bronze Nails 2.
- Bells use a real bell recording; Open Sign letters no longer stretch.
- Automated tests (DrakesTestLab suite in `Tests/`, test hooks in Debug builds only); not part of the release.

## 0.5.0-beta.1

Hexium beta (no Thunderstore release). Still under heavy testing. Requires **DrakeModsLibs 1.0.0-beta.3**.

- **Buy side:** stand can buy from customers (offers, budget, buy cards on the case, optional open buy crates / layout). Sell from cards back into the case.
- **Open crates:** placeable chest clones (standard / small low / big low / tall) with stock visuals.
- **Smart Lock-menu tabs:** Shop is the root; Buy and Layout are teal children while you’re in the Shop group; Mail stays its own root. Works with LockSmith’s Lock group.
- Leather mats (Deer / Wolf / Lox) and DisplayCard forge assets.

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
