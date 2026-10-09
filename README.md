# DrakesItemShop

**DrakesWorkshop** — JSON-driven shops for Valheim RP servers and small groups.

## Features

- **Drake Shop** hammer tab — Display Case, Small Display Case, Merchant Box, Shop Sign, NPC Spawner
- **Display cases** — glass and wood cases (6 or 3 slots) that show what's inside lying on jute. Stock them from the door side: the glass door slides open while you do. From the glass side they're just looked at.
- **Merchant Box** — vending machine: owner **E** stock, **Shift+E** shop, **Alt+E** manage/restock; customer **E** dispense pickup, **Shift+E** buy/sell
- **Shop Ledger** — use from inventory; link at merchant box via **Alt+E** manage, or `shop_give_ledger`
- **Shop Sign** — catalog prices; **Shift+E** to set `shopId`
- **Silent trader NPC** — configurable Haldor-style clone without dialogue (`npcs/*.json`)
- **Coins** buy/sell with optional item currencies in catalog JSON
- **Ward rules** per shop: `none`, `vanilla`, `betterwards`, `wardislove`
- **Server-authoritative** buy/sell via ZNet RPC
- **Price tags** on purchased items via `Drake_MarketPrice` and WorkshopLibs `ShopOverlay` display layer

## Config layout

`BepInEx/config/com.drakesworkshop.itemshop/ItemShop/`

| Folder | Purpose |
|--------|---------|
| `catalogs/` | Items, buy/sell prices, stock |
| `shops/` | Shop name, catalog ref, ward, permissions |
| `npcs/` | Silent trader prefab + catalog |

Example files are created on first run if missing.

## BepInEx config (synced)

| Setting | Default | Description |
|---------|---------|-------------|
| `AdminOnlyBuildPieces` | `false` | When `true`, only server admins can place Drake Shop hammer pieces. |
| `DisplayCaseCost` | `Wood:10` | Display Case build cost: `Item:amount` pairs, comma separated (prefab names). |
| `SmallDisplayCaseCost` | `Wood:5` | Small Display Case build cost. |

## Console commands

| Command | Description |
|---------|-------------|
| `shop_reload` | Reload JSON configs and refresh signs |
| `shop_spawn_npc <npcId>` | Spawn silent trader at player |
| `shop_give_ledger <shopId>` | Give portable ledger pre-bound to a shop |
| `shop_link_ledger` | Link inventory ledger to the merchant box you are looking at (admin) |

## Quick setup (ledger → shop)

1. Define shops in `ItemShop/shops/*.json` (example `market_stall` on first run).
2. Place a **Merchant Box**, press **Alt+E**, choose the shop id, press **Link ledger in inventory** (ledger must be in your inventory).
3. **Use** the ledger from inventory to open that shop. Stock comes from the linked box; portable ledgers use their own 8×4 stock.

Admin shortcut: `shop_give_ledger market_stall` gives a portable ledger without placing a box.

## Vending machine flow (Merchant Box)

Shops with `"stockSource": "container"` use vending mode:

1. **Owner** puts sellable items in the **stock chest** (**E**).
2. **Owner** puts coins in the **treasury** (**Alt+E** → Treasury) so the shop can pay sellers.
3. **Customer** uses **Shift+E** to buy/sell from the catalog.
4. **Purchases** go to the **dispense slot** (2×2) — customer collects with **E**.
5. **Sold items** go to **sell hold**; payment comes from **treasury**.

Owner restock shortcuts (**Alt+E** manage panel): Stock chest, Treasury, Sell hold, Dispense.

## Dependencies

- BepInEx, Jotunn
- **DrakeModsLibs**

## Build

Open `DrakesItemShop.csproj`, restore NuGet, build. Requires `environment.props` with Valheim paths (see `environment.props.example`).

The display cases' look is a Drakes Asset Forge pack in `Forge/DisplayCase` (models, recipes, the Blender script that makes them); see its README to change them.

## Releases

Hexium only. Push a tag matching `<Version>` in `mod.package.props` (`v0.1.0`, or `v0.1.0-beta.1` for a beta) and `.github/workflows/release.yml` builds, makes the GitHub release from that version's CHANGELOG section, and publishes to https://valheim.hexium.gg/mods/DrakeMods/DrakesItemShop.
