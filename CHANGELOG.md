# Changelog

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
