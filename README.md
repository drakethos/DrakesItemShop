# DrakesItemShop

**DrakesWorkshop** — JSON-driven shops for Valheim RP servers and small groups.

## Features

- **Merchant Box** — placeable chest clone; **E** opens shop UI, **Alt+E** opens container stock
- **Shop Ledger** — portable or linkable book item; use from inventory to open shop
- **Shop Sign** — displays catalog prices from linked `shopId`
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

## Console commands

| Command | Description |
|---------|-------------|
| `shop_reload` | Reload JSON configs and refresh signs |
| `shop_spawn_npc <npcId>` | Spawn silent trader at player |
| `shop_give_ledger <shopId>` | Give portable ledger for a shop |

## Dependencies

- BepInEx, Jotunn
- **DrakesCustomizeLibs** (DrakesWorkshopLibs)

## Build

Open `DrakesItemShop.csproj`, restore NuGet, build. Requires `environment.props` with Valheim paths (see `environment.props.example`).
