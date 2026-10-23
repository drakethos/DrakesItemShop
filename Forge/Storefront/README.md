# Forge/Storefront

Models and recipes for the shopfront pieces (Store Sign, Open Sign, Door Bell, Desk Bell, Trade Counter), copied from the
`house-DrakesIS` test pack. The models are also embedded from `Storefront/Generated/Assets/models`.

These pieces are wired by hand in `Storefront/StorefrontPieces.cs` (materials, collision, snap points, cost), not by the Forge
generator, because their moving parts (sign flip, bell swing, counter lids and drawers) are driven from code using the model's
node pivots. If you change a model here, copy the .glb to `Storefront/Generated/Assets/models` too.

Sounds are `Storefront/Sounds/*.wav`. The register and sign sounds were made for this mod. The door bell and desk bell are built by `Sounds/make_bells.py` from one real bell recording (`Sounds/source/bell_strike.wav`: "bell" by freesound_community on Pixabay, free to use in projects, no credit needed).
