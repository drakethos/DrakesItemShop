# DrakesItemShop

**DrakesWorkshop**: pieces for shops, stalls and post offices on Valheim RP servers and small groups. Everything is in the **Drake Shop** hammer tab.

## Features

- **Shops**: turn a display case, rack or mat **Open for business** (Shift+E → Shop). Set coin and/or item prices with **OR** alternatives (e.g. 200 coins or 10 Iron + 10 coins). Shoppers press **E** on priced goods; coins land in the till, item payments in the chest.
- **Free-layout mats**: jute, wolf, lox and small deer rugs. Arrange goods on the Layout tab (drag, turn, lift, size).
- **Armor and weapon racks**: finewood racks with iron hooks and pegs. Free-standing armor racks and weapon racks (6 or 4 slots) take only armor or only weapons and shields; wall racks (5 or 3 slots) take both. What's inside hangs on the rack: weapons and shields the way an item stand shows them, armor upright on its hook. Poses can be tuned per weapon type and per item.
- **Display cases**: glass and wood cases (6 or 3 slots) that show what's inside lying on jute. Stock them from the door side: the glass door slides open while you do. From the glass side they're just looked at. Table trays (jute / wolf / lox) for counters.
- **Trading menu (beta)**: press **Shift+T** near another player (a picker appears if several are near). They get a request; if they accept, you both offer items in one trade window and swap when you both confirm. The trade ends if you move too far apart.
- **Store Sign, Open Sign, bells and Trade Counter** (storefront pieces): a hanging **Store Sign** whose text you write in a proper window (rich text, fonts, colours, outline, both faces); a door **Open Sign** that flips OPEN / CLOSED with **E**; a **Door Bell** that jingles and says "A customer has arrived" when a nearby door opens; a **Desk Bell** you ring with **E**; and a two-sided **Trade Counter**: shopkeeper behind, customer in front, **E** starts a trade with whoever is across (the usual trade window, so the swap is simultaneous) and the register chest holds stock and takings.
- **Mailbox and Drop Box**: a post-mounted mailbox and a tabletop box for a shop stall. Anyone can send up to 3 stacks and a note through the front slot (one way: they can't take it back out). The back is a plain 10-slot chest with its own door, opened by whoever the ward or LockSmith allows. The owner can set an allow list, an item filter, and close the slot (**Shift+E**, or the Lock menu with LockSmith).
- **Vending machines** (server traders): Haldor's, Hildir's and the Bog Witch's machines sell that trader's real goods, for servers and RP worlds that would rather not have the NPCs. Admins place them (always admin-only). Each one works once the world has reached its trader's biome (configurable world keys); until then it is dark and "out of order". **E** opens the game's own store window; coins paid leave the world. Behind the crystal glass the shelves (5 x 4) show what you can buy now, and "Sold out" cards stand in for goods not unlocked yet (no spoilers). Buy something and that slot's dvergr rune flashes, a pusher shoves it off the shelf, it drops into the chute, poofs to full size and you grab it (it is already in your bag; nothing is ever dropped). The sides carry trader posters like a real machine.
- **PO boxes and the Post Hall**: a post hall frame with 5x2 sockets that PO boxes snap into. Each PO box has one door: its assigned owner (and anyone they delegate to) opens it like a chest, everyone else gets the send window. The builder can act as postmaster. A flag shows mail waiting (green) or a full box (red).

## Pictures

Taken automatically by the DrakesTestLab picture suite (`Tests/gen_pictures_suite.py`).

**Display cases and mats**

| | |
|---|---|
| ![Display case](docs/images/shop-display-case.png) | ![Small display case](docs/images/shop-display-case-small.png) |
| ![Jute table tray](docs/images/shop-display-mat.png) | ![Small jute table tray](docs/images/shop-display-mat-small.png) |
| ![Free-layout jute mat](docs/images/shop-jute-mat.png) | ![Open crate](docs/images/shop-open-crate.png) |

**Racks**

| | |
|---|---|
| ![Armor rack](docs/images/shop-armor-rack.png) | ![Small armor rack](docs/images/shop-armor-rack-small.png) |
| ![Weapon rack](docs/images/shop-weapon-rack.png) | ![Small weapon rack](docs/images/shop-weapon-rack-small.png) |
| ![Wall rack](docs/images/shop-wall-rack.png) | ![Small wall rack](docs/images/shop-wall-rack-small.png) |

**Storefront, mail and apron**

| | |
|---|---|
| ![Store sign](docs/images/shop-store-sign.png) | ![Open sign and bells](docs/images/shop-signs-bells.png) |
| ![Trade counter](docs/images/shop-trade-counter.png) | ![Mailbox](docs/images/shop-mailbox.png) |
| ![Shopkeeper apron](docs/images/shop-apron.png) | |

**Menus**

| | |
|---|---|
| ![Shop menu](docs/images/menu-shop.png) | ![Layout tab](docs/images/menu-layout.png) |
| ![Buy tab](docs/images/menu-buy.png) | ![Price window](docs/images/menu-price.png) |
| ![Buy window](docs/images/menu-buy-window.png) | ![Mailbox send window](docs/images/menu-mail-send.png) |
| ![Trade window](docs/images/menu-trade.png) | |

## BepInEx config

Synced (server settings):

| Setting | Default | Description |
|---------|---------|-------------|
| `AdminOnlyBuildPieces` | `false` | When `true`, only server admins can place Drake Shop hammer pieces. |
| `DisplayCaseCost` | `FineWood:10,JuteRed:2,Crystal:2` | Display Case build cost: `Item:amount` pairs, comma separated (prefab names). |
| `SmallDisplayCaseCost` | `FineWood:5,JuteRed:1,Crystal:1` | Small Display Case build cost. |
| `ArmorRackCost` / `SmallArmorRackCost` | `FineWood:12,BronzeNails:6` / `FineWood:8,BronzeNails:4` | Free-standing armor racks. |
| `WallRackCost` / `SmallWallRackCost` | `FineWood:8,BronzeNails:5` / `FineWood:5,BronzeNails:3` | Wall racks (armor and weapons). |
| `WeaponRackCost` / `SmallWeaponRackCost` | `FineWood:10,BronzeNails:6` / `FineWood:7,BronzeNails:4` | Free-standing weapon racks. |
| `10 Vending` `Haldor unlock keys` | `defeated_eikthyr` | World keys Haldor's vending machine needs (comma-separated, all of them; empty = always works). |
| `10 Vending` `Hildir unlock keys` | (empty) | Same for Hildir's machine (works from the start). |
| `10 Vending` `Bog Witch unlock keys` | `defeated_gdking` | Same for the Bog Witch's machine. |
| `10 Vending` `Out of order message` | `This seems to be out of order` | What a locked machine says on hover and on E. |
| `10 Vending` `Sold out card` | `Sold out` | The card shown in a slot whose item isn't unlocked yet. |
| `10 Vending` `Bay light` | `Night` | The light behind the glass: `Night`, `Always` or `Off`. A purchase always flashes it. |

Local (per player) settings: `06 Trade` (shortcut, request seconds, cancel distance, nearby range), `07 Mailbox` (skip the send warning), `08 Storefront` (door bell door range, message range, show the "A customer has arrived" message).

Mailbox, drop box, PO box and vending machine costs are fixed in code for now (vending machines: FineWood 10, Iron 4, Crystal 2; admins only).

## Dependencies

- BepInEx, Jotunn
- **DrakeModsLibs**
- Optional: **LockSmith** (Lock menu for mailboxes and chests)

## Build

Open `DrakesItemShop.csproj`, restore NuGet, build. Requires `environment.props` with Valheim paths (see `environment.props.example`).

The display cases' look is a Drakes Asset Forge pack in `Forge/DisplayCase` (models, recipes, the Blender script that makes them); see its README to change them. The racks' look is the pack in `Forge/ItemRack`, the same way.

## Releases

Hexium only. Push a tag matching `<Version>` in `mod.package.props` (`v0.5.0-beta.1`, or `v0.5.0-beta.1-beta.1` for a beta) and `.github/workflows/release.yml` builds, makes the GitHub release from that version's CHANGELOG section, and publishes to https://valheim.hexium.gg/mods/DrakeMods/DrakesItemShop.
