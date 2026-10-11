# DrakesItemShop

**DrakesWorkshop**: pieces for shops, stalls and post offices on Valheim RP servers and small groups. Everything is in the **Drake Shop** hammer tab.

## Features

- **Armor and weapon racks**: finewood racks with iron hooks and pegs. Free-standing armor racks and weapon racks (6 or 4 slots) take only armor or only weapons and shields; wall racks (5 or 3 slots) take both. What's inside hangs on the rack: weapons and shields the way an item stand shows them, armor upright on its hook. Poses can be tuned per weapon type and per item.
- **Display cases**: glass and wood cases (6 or 3 slots) that show what's inside lying on jute. Stock them from the door side: the glass door slides open while you do. From the glass side they're just looked at.
- **Trading menu (beta)**: press **Shift+T** near another player (a picker appears if several are near). They get a request; if they accept, you both offer items in one trade window and swap when you both confirm. The trade ends if you move too far apart.
- **Mailbox and Drop Box**: a post-mounted mailbox and a tabletop box for a shop stall. Anyone can send up to 3 stacks and a note through the front slot (one way: they can't take it back out). The back is a plain 10-slot chest with its own door, opened by whoever the ward or LockSmith allows. The owner can set an allow list, an item filter, and close the slot (**Shift+E**, or the Lock menu with LockSmith).
- **PO boxes and the Post Hall**: a post hall frame with 5x2 sockets that PO boxes snap into. Each PO box has one door: its assigned owner (and anyone they delegate to) opens it like a chest, everyone else gets the send window. The builder can act as postmaster. A flag shows mail waiting (green) or a full box (red).

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

Local (per player) settings: `06 Trade` (shortcut, request seconds, cancel distance, nearby range), `07 Mailbox` (skip the send warning).

Mailbox, drop box and PO box costs are fixed in code for now.

## Dependencies

- BepInEx, Jotunn
- **DrakeModsLibs**
- Optional: **LockSmith** (Lock menu for mailboxes and chests)

## Build

Open `DrakesItemShop.csproj`, restore NuGet, build. Requires `environment.props` with Valheim paths (see `environment.props.example`).

The display cases' look is a Drakes Asset Forge pack in `Forge/DisplayCase` (models, recipes, the Blender script that makes them); see its README to change them. The racks' look is the pack in `Forge/ItemRack`, the same way.

## Releases

Hexium only. Push a tag matching `<Version>` in `mod.package.props` (`v0.1.0`, or `v0.1.0-beta.1` for a beta) and `.github/workflows/release.yml` builds, makes the GitHub release from that version's CHANGELOG section, and publishes to https://valheim.hexium.gg/mods/DrakeMods/DrakesItemShop.
