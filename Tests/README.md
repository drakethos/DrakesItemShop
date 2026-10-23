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

- A test shop next to the house: display case, weapon rack and display mat (one built with the hammer)
- Buying for coins, barter, alternative payments, lots; till, sales log and collecting
- Refusals: closed, cannot afford, no room, sold out, price changed, coins for coins
- Who may run a shop: builder, ward-permitted player, not strangers

Owned pieces and items are built or crafted through the game; other players are simulated with play-as (Alice, Bob, Carol, Nacho-VIP).
Every test also checks the mod logged no errors.

## Not covered (manual or later)

- The buy / price-edit / barter windows themselves (purchases run through `ShopPurchase.Purchase`, the buy window's own code).
- Player-to-player trade, mailboxes / post wall, open crates, aprons and the fitting room: not covered yet.
- How goods lie in a case or hang on a rack: screenshots only.
- The server-side purchase check with a dedicated server: manual.
