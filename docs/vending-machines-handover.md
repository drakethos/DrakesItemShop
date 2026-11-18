# Vending machines: handover (2026-11-03)

ItemShop 0.6.0 in progress: server vending machines that replace the traders (Haldor, Hildir, Bog Witch). Nothing is
committed, tagged or released.

## What it is

- Three admin-only pieces in the Drake Shop tab: `Drake_VendingHaldor` (Black Forest), `Drake_VendingHildir` (Meadows),
  `Drake_VendingBogWitch` (Swamp). They sell the vanilla trader's own goods through the game's `StoreGui`; coins leave
  the world.
- Gated by world keys (config `10 Vending`): Haldor `defeated_eikthyr`, Hildir none, Bog Witch `defeated_gdking`. Locked =
  dark, "This seems to be out of order" (configurable), E does nothing. Removing a key switches it off again.
- 5 x 4 shelves behind clear glass: buyable items first, then "Sold out" tent cards (the shop's DisplayCard) for locked
  ones. An item bought that is not on a shelf is swapped into the last slot first, so every buy is seen.
- Vend: rune flash, pusher, drop into the chute, build-dust poof (`Piece.m_placeEffect`), pops to full size, player plays
  the pickup animation ("interact" trigger) and the item zips into the pocket with `m_pickupEffects`. Purely visual: the
  store already added the item (never dropped). Local only: other players don't see the vend.
- Bay light: config `Bay light` = Night (default) / Always / Off; glow kept low to avoid daytime bloom.
- Side "wrap" posters (user's art, 9:16) on both sides.

## Where things are

| What | Path |
|---|---|
| Model script (Blender 5.1, headless) | `Forge/VendingMachine/make_vending_machine.py` (+ README there) |
| Recipes / models / posters | `Forge/VendingMachine/items`, `models`, `textures/vend_<kind>.jpg` |
| Generated code + assets | `Vending/Generated` (export with the **Debug** AssetForge build: `DrakesAssetForge/Forge/App/bin/Debug/net8.0/DrakesAssetForge.exe export-code Forge/VendingMachine . --look-only --libs --embed --into --namespace DrakesItemShop.Vending --folder Vending/Generated`; the Release build is old and drops the .glb) |
| Piece registration, glass, art tiling fix | `Vending/VendingPiece.cs` |
| Gating, glow, light, store proxy, Trader patches | `Vending/VendingBehaviour.cs` |
| Shelves, sold out cards, vend animation, effects | `Vending/VendingShelf.cs` |
| Settings | `Vending/VendingSettings.cs` (6 synced entries, counted in `ItemShopConfig`) |
| Admin-only gate (+ `TestNotAdmin` for tests) | `Patches/PieceBuildGatePatch.cs` |
| Test hooks (Debug only) | `Testing/VendingTestHooks.cs` (`itemshop.vending.*`) |
| Test suite | `Tests/gen_vending_suite.py` -> `Tests/suites/DrakesItemShop-Vending` (V-01..V-14) |
| Hand-test case | `Tests/cases/vending-machines.json` (copied into both drakeTest profiles; Gale drakeTest `[Case] OnLoad = vending-machines`, was `serverteams-spawns`; it also turns ServerTeams `TeamGlow` off) |
| Release notes | `CHANGELOG.md` 0.6.0, `README.md` (feature + `10 Vending` settings), `Tests/README.md` |

Version is bumped to **0.6.0** in `mod.package.props` (user asked for the minor bump). Libs pin unchanged (1.0.5).

## Gotchas found

- Don't implement Valheim's `Hoverable`/`Interactable` directly: live `Hoverable` has `GetHoverOffset`, the stubs don't
  -> TypeLoadException. The piece keeps the chest clone's `Container` and implements `Storefront.IStorefrontUse`.
- `EffectList.Create` has an extra `ZDOID` parameter live: call it through reflection (`PlayEffect`). Run
  `scripts/Verify-ValheimApiRefs.ps1` after every build.
- `Character.m_zanim` is protected: reflection (`GrabAnimation`).
- Item shelf copies must drop their `Light` components (a Hildir item flooded the machines red).
- The poster borrows `window_wood`'s shader, which tiles; `VendingPiece` resets `mainTextureScale`.
- `StoreGui.IsVisible()` turns true a frame late; tests check `m_rootPanel.activeSelf`.
- Libs `DrakeMessage` popups are recorded by TestLab as message kind `Card`, not `Center`.
- **Packaging:** an old `Thunderstore/DrakeMods-DrakesItemShop-0.6.0` staging folder (from 2026-10-25) put a stale root
  `DrakesItemShop.dll` in the zip; BepInEx loaded it instead of `plugins/DrakesItemShop.dll`. Cleaned (stale files moved
  to `C:\Modding\_backup\stale-itemshop-dll`), zip now has only `plugins/`. The packager reuses existing staging folders;
  worth a guard.

## DrakesTestLab changes (separate repo, uncommitted, unreleased)

- Case `pieces` stand on the terrain under them by default; a piece with `dy` and no `ground` keeps its height relative to
  you (saved builds); `"ground": true/false` forces it. `Cases/TestCase.cs`, `Cases/CaseRunner.cs`, CHANGELOG Unreleased.
- Those two files also hold an earlier session's uncommitted work; nothing of TestLab is committed.

## Test status

- `DrakesItemShop-Vending`: 14/14 passed once; on the next run V-13 (relog) failed: after a world load the machine could
  Awake before its ZNetView was valid and never set up. Fixed (`TrySetUp` retried from Update), **recheck was running
  at handover** (background task: S-26 alone, AB-05 alone, Vending).
- `DrakesItemShop` main suite: 30/31. S-26 (post wall relog) failed: after the relog the section was gone (world
  reloaded without it). Rerun alone pending; when run together with other suites its `launchConfig` (post wall on) isn't
  applied, so run it on its own.
- `DrakesItemShop-ApronAbilities`: AB-05 failed once (test house not roofed: `apron.sheltered` false); rerun alone pending.
- Apron (fitting run) and Pictures (README screenshots) suites are not regression suites and were not run.

## Open decisions for the user

- `Trade/TradeNet.cs` has an uncommitted fix from another session (combat timer read via `Player.CanSwitchPVP`). It was
  built and tested with everything above. Ship it in 0.6.0 or leave it out?
- Then: commit ItemShop (only the files above, plus TradeNet if wanted), run
  `scripts/Pre-Release-Check.ps1 -Mod DrakesItemShop`, tag only on the user's go.

## Not built yet (designed)

- Trader flags (`ReplaceHaldor` etc.): suppress the NPC at runtime only, never delete locations/ZDOs, so removing the mod
  brings the traders back.
- YAML vendors / custom stock, limited stock and restock, optional coin collection.
- Other players seeing a vend (needs an RPC); per-trader lantern tuning.
