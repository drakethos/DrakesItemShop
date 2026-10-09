# Display cases (Forge pack)

Source of the display cases' looks: `Drake_DisplayCase` (6x1 slots) and `Drake_DisplayCaseSmall` (3x1, half length).
Edit the recipes in Drakes Asset Forge (open this folder as a pack), then regenerate the code:

```
DrakesAssetForge export-code Forge/DisplayCase . --look-only --libs --embed --into --namespace DrakesItemShop.DisplayCase --folder DisplayCase/Generated
```

## Models

`make_display_case.py` builds both .glb files from the base mesh in `Documents\display case.blend` (Blender 5.1):

```
blender -b "display case.blend" --python make_display_case.py -- <out.blend> models/display_case.glb <preview.png> <texture dir> big
blender -b "display case.blend" --python make_display_case.py -- <out.blend> models/display_case_small.glb <preview.png> <texture dir> small
```

It prints `CASEINFO` (bounds and slide distance). If the size or door opening changes, update the recipes' collision
box and snap points, and in code `DisplayCasePiece` (slide distance, floor area).

- The door must stay a mesh named `GlassPanel` (Forge names renderers after the mesh data): `DisplayCaseBehaviour` slides it.
- The door side is local -X (below x = 0.36); the door slides along -Z.
- Items lie on the jute cloth (y = 1.036, x -0.29..1.02).
