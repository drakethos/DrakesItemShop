# Vending machines (Forge pack)

Server vendors that stand in for the traders (no NPC, no owner; coins paid are removed from the world). ItemShop only.
Three placeables, one per trader; YAML adds more vendors later.

| Recipe | Trader / biome | Body | Trim | Extras |
|---|---|---|---|---|
| `Drake_VendingHaldor` | Haldor, Black Forest | window_wood | dvergr_copper | lantern hanging off the crest |
| `Drake_VendingBogWitch` | Bog Witch, Swamp | DarkWood_mat | metalwall | roots on the crest, moss (jute tinted green), candles (candle_mat) |
| `Drake_VendingHildir` | Hildir, Meadows | window_wood | metalwall | striped awning (jute tinted red + plain jute) |

## Model

`make_vending_machine.py` builds each from scratch (no .blend needed; Blender 5.1):

```
blender -b --factory-startup --python make_vending_machine.py -- <out.blend> models/vending_<kind>.glb preview/vending_<kind> - <haldor|bogwitch|hildir> textures/vend_<kind>.jpg
```

It writes `preview/vending_<kind>_front.png` / `_side.png` (flat colours) and prints `VENDINFO` (slots, pusher travel,
chute and use point, Blender coordinates; Unity (x, y, z) = Blender (x, z, y)).

- Material slots: `Body`, `Trim`, `Glass`, `Black`, `Glow`, `Art`, plus `Cloth`/`Cloth2` on the Bog Witch and Hildir.
  Try other looks by changing a slot's `from` in the recipe; the model doesn't change.
- `Glow` (runes, lamp, lantern core, candle flames) isn't mapped yet: it needs an emissive dvergr-blue material in code,
  dark when the vendor is out of order.
- **Side posters (the art wrap):** both sides carry a 9:16 poster, 0.60 x 1.07 m, in an iron frame. The `Art` material
  maps the whole image upright on each side (UV 0..1). Replace `textures/vend_<kind>.jpg` with the art (any 9:16 size,
  720x1280 is plenty); the files there now are flat placeholders. The script only writes a placeholder when the file
  is missing, so rebuilding never overwrites real art.
- Front (customer side) is +Y in Blender (+Z in Unity). The window is on the customer's left, the rune plate (use
  point) on the right.
- Meshes the game animates (Forge names renderers after the mesh data name):
  - `Pusher0..8`: one per slot, slide forward by `pushTravel` to shove the item off the shelf front edge.
  - `Rune0..8`: one per slot, on the back wall above the pusher, flash while that slot vends.
  - `Lamp`: crest rune, rune plate dots, lantern core / candle flames.
- Slot order: 0 = top row, left to right as seen from the front.
- Planned vend: rune flashes, pusher slides, a display copy of the item falls behind the glass into the chute tray,
  shows there briefly, then the real item is added to the buyer's inventory (drops at their feet if full).
- Planned gating: a vendor whose trader requirement isn't met (e.g. Swamp not reached) stays dark and says
  "This seems to be out of order" (configurable).
