# Armor and weapon racks (Forge pack)

Source of the racks' looks: armor racks (`Drake_ArmorRack`, `Drake_ArmorRackSmall`), weapon racks (`Drake_WeaponRack`,
`Drake_WeaponRackSmall`) and wall racks for both (`Drake_WallRack`, `Drake_WallRackSmall`).
Regenerate the code after changing a recipe (use a Drakes Asset Forge build that supports model export):

```
DrakesAssetForge export-code Forge/ItemRack . --look-only --libs --embed --into --namespace DrakesItemShop.ItemRack --folder ItemRack/Generated
```

## Models

`make_item_rack.py` builds every rack from scratch (no .blend needed; Blender 5.1):

```
blender -b --factory-startup --python make_item_rack.py -- <out.blend> models/rack_<type>_<mount>_<size>.glb <preview.png> <texture dir> <armor|weapon|any> <stand|wall> <big|small>
```

The stands are `armor stand` / `weapon stand`; the wall racks are `any wall` (iron hooks), saved as `models/rack_wall_<size>.glb`.

The texture dir needs `wood_window_window_wood.tga` (preview only). Stand-in items in the preview are not exported.
It prints `RACKINFO` (bounds and hang points, Blender coordinates; Unity (x, y, z) = Blender (x, z, y)). If sizes or
slots change, update the recipes' collision box, snap points and container size, and in code the hang points in
`Pieces/ItemRackPiece.cs`.

- One mesh, `Rack`, with material slots `Wood` (wood_window / window_wood) and `Metal` (iron_grate / metalwall).
- Front (the item side) is +Z. Wall racks: the origin is the back centre of the board.
- Armor stands: container row 0 = the top hooks, row 1 = the pegs on the middle bar.
