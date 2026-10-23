# ItemShop automated tests

DrakesTestLab suite for ItemShop. It runs unattended; results go to `<profile>\BepInEx\config\DrakesTestLab\results\latest.html`.

- `suites/DrakesItemShop/`: the tests (generated, do not edit by hand).
- `gen_itemshop_suite.py`: writes the suite (`python gen_itemshop_suite.py`). The build runs it and copies the suite into the drakeTest profiles; `Tests/` is never packaged.
- `../Testing/ItemShopTestHooks.cs`: the `itemshop.*` test hooks the suite calls. They use the mod's real code paths; no reference to DrakesTestLab, and without it installed they are never called.

## Run

```powershell
..\DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites DrakesItemShop -Screenshots Always
```

or everything at once: `..\scripts\Test-All.ps1`. In game: F9 > Tests.

## Covered

- Every shop piece builds from the hammer (cases, mats, racks, mailboxes, open crate, signs); build costs are charged; the hidden PO box can't be built on its own
- Buying for coins, barter, alternative payments, lots; till, sales log and collecting
- Refusals: closed, cannot afford, no room, sold out, price changed, coins for coins
- Who may run a shop: builder, ward-permitted player, not strangers
- Buying from customers: offers, budget, selling to the shop, refusals (out of budget, not enough goods); goods go into the chest
- Mailbox and drop box: anyone posts, the note and sender are logged; only the owner opens the back (front = send slot, back = chest, checked by walking round); a box outside a ward warns it is unprotected
- Post wall (experimental, switched on for this suite with `launchConfig`): sections spawn PO boxes, assigning a box to a player, delegates and the postmaster, boxes survive a relog, removing a section removes its boxes and drops their mail
- Open Sign, Store Sign text, bells; aprons crafted at a workbench with the real cost, worn, Open for business carry weight
- Racks, mats and cases: screenshots of how goods lie

Owned pieces and items are built or crafted through the game; other players are simulated with play-as (Alice, Bob, Carol, Nacho-VIP).
Every test also checks the mod logged no errors.

Cross-mod tests (renamed or reskinned goods in shops and mail, the Shop and Mail tabs in LockSmith's Lock menu, LockSmith guests and public chests on shops) live in `DrakeModsLibs/Tests`.

## Not covered (manual or later)

- The buy / price-edit / barter windows themselves (purchases run through `ShopPurchase.Purchase`, the buy window's own code).
- Player-to-player trade (needs two clients), the fitting room.
- AdminOnlyBuildPieces refusing a non-admin: the single-player host is always admin; needs a dedicated server.
- The server-side purchase check with a dedicated server: manual.
- Assigning a PO box to a nearby player from the Mail tab (lists real nearby players): manual with two clients.
