# Changelog

## 0.2.0-beta.1
- Beta. Requires DrakeModsLibs 0.11.0+.
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
