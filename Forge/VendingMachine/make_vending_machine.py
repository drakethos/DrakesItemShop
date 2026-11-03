"""Dvergr vending machine, fully procedural: a server-run vendor cabinet with a crystal glass front, a 3x3 grid of item
slots, a glowing rune behind each slot that pushes the item off its shelf, and a pickup chute below.
Three trader versions share the cabinet; each recipe maps Body/Trim to its biome's vanilla materials:
  haldor    Black Forest: hanging lantern off the crest
  bogwitch  Swamp: twisted roots on the crest, moss hanging from the eaves, a candle ledge over the rune plate
  hildir    Meadows: striped cloth awning over the window
Both sides are wrapped like a real vending machine: one 9:16 image (material Art, UV 0..1, upright) covers each side
edge to edge from the top band down to just above the waist band.
Args after --: out_blend out_glb out_png_prefix texdir kind(haldor|bogwitch|hildir) art_png
(texdir = "-" for flat preview colours; art_png is the side poster, a flat placeholder is written if it's missing)
Blender axes: X = width, Y = depth (+Y = customer front), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y).
Objects (Forge names renderers after the MESH data name, so these stay unique):
  Cabinet          body, shelves, chute, crest, straps (Body, Trim, Black)
  Glass            front crystal pane (Glass)
  Pusher0..19      one wooden pusher plate per slot, slides +Y to shove the item off (Body)
  Rune0..19        one rune disc per slot on the back wall, flashes while vending (Glow)
  Lamp             rune lamp on the crest, the rune plate dots, lantern core / candle flames (Glow)
  Art              the two side posters (Art)
Slot order: row 0 = top, left to right as seen from the front. Prints VENDINFO (slots, chute, bounds)."""
import bpy, bmesh, math, sys, random, json
from mathutils import Vector, Matrix

out_blend, out_glb, out_png, texdir, kind, art_png = sys.argv[sys.argv.index("--") + 1:]
assert kind in ("haldor", "bogwitch", "hildir"), kind
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19        # vanilla Planks5c density: grain along U
rng = random.Random(11)
body = D.materials.new("Body")
trim = D.materials.new("Trim")
glassmat = D.materials.new("Glass")
glow = D.materials.new("Glow")
black = D.materials.new("Black")
cloth = D.materials.new("Cloth")      # awning stripes (Hildir red) / hanging moss (Bog Witch), tinted in game
cloth2 = D.materials.new("Cloth2")    # awning's cream stripes / candle wax
art = D.materials.new("Art")
MATS = [body, trim, black, cloth, cloth2]
BODY, TRIM, BLACK, CLOTH, CLOTH2 = 0, 1, 2, 3, 4

# ---------- dimensions (metres) ----------
W2 = 0.56                         # half width
Y0, Y1 = -0.42, 0.42              # back, front (deep enough that the side wrap is 9:16)
ZS = 1.2                          # everything is built at the old 2 m size, then stretched this much taller
T = 0.05                          # wall thickness
H = 2.0                           # top of the cabinet box
PLINTH = 0.12
WX0, WX1 = -0.24, 0.47            # glass window (+X = the customer's left, so the window is on the left as seen)
WZ0, WZ1 = 0.78, 1.86
PX0, PX1 = -0.49, -0.30           # rune plate column (right of the window as seen from the front)
CZ0, CZ1 = 0.26, 0.60             # chute opening
GY = Y1 - 0.03                    # glass plane
SHELF_Y1 = GY - 0.10              # shelf front edge: items fall in the gap behind the glass
NC, NR = 5, 4                     # slots: 5 across, 4 shelves (20)
ROWS = [WZ0 + (WZ1 - WZ0) / NR * k for k in reversed(range(NR))]   # shelf tops, row 0 = top
COLS = [WX1 - (WX1 - WX0) * (i + 0.5) / NC for i in range(NC)]     # left to right as seen from the front
SLOT_W = (WX1 - WX0) / NC
BACK_IN = Y0 + T                  # inside face of the back wall
RUNE_Y = BACK_IN + 0.004
PUSH_Y0 = BACK_IN + 0.03          # pusher rest position (back face)


def wood_uv(uv, faces, long_axis):
    ou, ov = rng.random(), rng.random()
    for f in faces:
        n = f.normal
        normal_axis = max(range(3), key=lambda i: abs(n[i]))
        plane = [i for i in range(3) if i != normal_axis]
        ua = long_axis if long_axis in plane else plane[0]
        va = plane[1] if ua == plane[0] else plane[0]
        for l in f.loops:
            l[uv].uv = (l.vert.co[ua] / WOOD_U + ou, l.vert.co[va] / WOOD_V + ov)


def box_uv(uv, faces):
    for f in faces:
        ax = max(range(3), key=lambda i: abs(f.normal[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for l in f.loops:
            l[uv].uv = (l.vert.co[a], l.vert.co[b])


class Mesh:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _finish(self, new, mat, mats, long_axis):
        self.bm.normal_update()
        for f in new:
            f.material_index = mat
        if mats[mat] is body:
            wood_uv(self.uv, new, long_axis)
        else:
            box_uv(self.uv, new)

    def box(self, lo, hi, mat=BODY, bev=0.006, mats=MATS):
        lo, hi = Vector(lo), Vector(hi)
        before = set(self.bm.faces)
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector(((lo.x + hi.x) / 2 + v.co.x * (hi.x - lo.x),
                           (lo.y + hi.y) / 2 + v.co.y * (hi.y - lo.y),
                           (lo.z + hi.z) / 2 + v.co.z * (hi.z - lo.z)))
        dims = hi - lo
        if bev:
            edges = list({e for v in r["verts"] for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=min(bev, min(dims) * 0.3), segments=2, profile=0.5,
                            affect="EDGES", clamp_overlap=True)
        self._finish([f for f in self.bm.faces if f not in before], mat, mats, max(range(3), key=lambda i: dims[i]))

    def box_rot(self, c, size, rot, mat=BODY, bev=0.004, mats=MATS):
        """A box of size (x, y, z) centred on c, turned by the matrix rot (grain along its z)."""
        before = set(self.bm.faces)
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bev:
            edges = list({e for v in r["verts"] for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=min(bev, min(size) * 0.3), segments=1, affect="EDGES",
                            clamp_overlap=True)
        verts = list({v for f in self.bm.faces if f not in before for v in f.verts})
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(c)) @ rot.to_4x4(), verts=verts)
        self._finish([f for f in self.bm.faces if f not in before], mat, mats, 2)

    def quad(self, pts, uvs, mat=0):
        f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        f.material_index = mat
        for l, uv in zip(f.loops, uvs):
            l[self.uv].uv = uv

    def prism_x(self, x0, x1, profile, mat=BODY, mats=MATS):
        """A shape extruded along X from a (y, z) outline (counter-clockwise seen from +X)."""
        before = set(self.bm.faces)
        a = [self.bm.verts.new((x0, y, z)) for y, z in profile]
        b = [self.bm.verts.new((x1, y, z)) for y, z in profile]
        n = len(profile)
        self.bm.faces.new(list(reversed(a)))
        self.bm.faces.new(b)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((a[i], a[j], b[j], b[i]))
        self._finish([f for f in self.bm.faces if f not in before], mat, mats, 0)

    def disc(self, c, r, depth, axis="Y", mat=TRIM, seg=12, mats=MATS):
        before = set(self.bm.faces)
        rot = {"Y": Matrix.Rotation(math.radians(-90), 4, "X"), "Z": Matrix.Identity(4),
               "X": Matrix.Rotation(math.radians(90), 4, "Y")}[axis]
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r, radius2=r * 0.9, depth=depth,
                              matrix=Matrix.Translation(Vector(c)) @ rot)
        self._finish([f for f in self.bm.faces if f not in before], mat, mats, 0)

    def make(self, mats=MATS, sharp=35):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = D.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(sharp))
        ob = D.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


# ---------- Cabinet ----------
cab = Mesh("Cabinet")
cab.box((-W2 - 0.04, Y0 - 0.04, 0), (W2 + 0.04, Y1 + 0.04, PLINTH), TRIM, 0.015)          # iron plinth
Z0 = PLINTH
cab.box((-W2, Y0, Z0), (-W2 + T, Y1, H), BODY)                                           # sides
cab.box((W2 - T, Y0, Z0), (W2, Y1, H), BODY)
cab.box((-W2 + T, Y0, Z0), (W2 - T, Y0 + T, H), BODY)                                    # back
cab.box((-W2, Y0, H - T), (W2, Y1, H), BODY)                                             # top
cab.box((-W2 + T, Y0 + T, Z0), (W2 - T, Y1 - T, Z0 + T), BODY)                           # floor
# side panels: raised stiles and rails around an inset field, reads as a framed wood door or a riveted iron plate
for sx in (-1, 1):
    x0, x1 = (sorted((sx * W2, sx * (W2 + 0.02))))
    for z0, z1 in ((Z0, Z0 + 0.09), (H - 0.09, H), (1.02, 1.10)):
        cab.box((x0, Y0, z0), (x1, Y1, z1), BODY, 0.008)
    for y0, y1 in ((Y0, Y0 + 0.08), (Y1 - 0.08, Y1)):
        cab.box((x0, y0, Z0), (x1, y1, H), BODY, 0.008)
# front face: frame around the window, plate column and chute
FY0, FY1 = Y1 - T, Y1
for lo, hi in (((WX1, FY0, Z0), (W2, FY1, H)),                # left stile (as seen)
               ((PX1, FY0, Z0), (WX0, FY1, H)),               # mullion between window and plate
               ((-W2, FY0, Z0), (PX0, FY1, H)),               # right stile
               ((WX0, FY0, WZ1), (WX1, FY1, H)),              # head over the window
               ((WX0, FY0, CZ1), (WX1, FY1, WZ0)),            # rail between window and chute
               ((WX0, FY0, Z0), (WX1, FY1, CZ0)),             # kick rail under the chute
               ((PX0, FY0, Z0), (PX1, FY1, 0.66)),            # under the plate
               ((PX0, FY0, 1.80), (PX1, FY1, H))):            # over the plate
    cab.box(lo, hi, BODY, 0.01)
# raised moulding around the window
for lo, hi in (((WX0 - 0.03, FY1, WZ0 - 0.03), (WX1 + 0.03, FY1 + 0.025, WZ0)),
               ((WX0 - 0.03, FY1, WZ1), (WX1 + 0.03, FY1 + 0.025, WZ1 + 0.03)),
               ((WX0 - 0.03, FY1, WZ0), (WX0, FY1 + 0.025, WZ1)),
               ((WX1, FY1, WZ0), (WX1 + 0.03, FY1 + 0.025, WZ1))):
    cab.box(lo, hi, BODY, 0.008)
# interior: dark back field behind the runes, shelves, side cheeks of the item bay
cab.box((WX0, BACK_IN, WZ0), (WX1, BACK_IN + 0.002, WZ1), BLACK, 0)
for z in ROWS:
    cab.box((WX0, BACK_IN, z - 0.03), (WX1, SHELF_Y1, z), BODY, 0.004)
    cab.box((WX0, SHELF_Y1 - 0.012, z), (WX1, SHELF_Y1, z + 0.015), TRIM, 0.002)        # iron lip
for z in ROWS:                                                                         # slot dividers
    for k in range(1, NC):
        x = WX0 + k * SLOT_W
        cab.box((x - 0.005, BACK_IN, z), (x + 0.005, SHELF_Y1 - 0.04, z + 0.09), TRIM, 0.002)
cab.box((PX1, BACK_IN, CZ0), (WX0, FY0, WZ1), BODY, 0)                                  # bay partition
cab.box((WX1, BACK_IN, WZ0 - 0.06), (W2 - T, FY0, WZ1), BODY, 0)
# drop shaft and chute: a dark well behind the glass that ends in an open tray at the front
cab.box((WX0, SHELF_Y1 + 0.01, CZ1), (WX1, SHELF_Y1 + 0.012, WZ0 - 0.02), BLACK, 0)      # shaft back
cab.box((WX0, Y1 - 0.30, CZ0), (WX1, Y1 - 0.29, CZ1), BLACK, 0)                         # chute back
cab.box((WX0, Y1 - 0.30, CZ1 - 0.01), (WX1, FY0, CZ1), BLACK, 0)                        # chute roof
cab.prism_x(WX0, WX1, [(Y1 - 0.30, CZ0 + 0.06), (Y1 - 0.30, CZ0), (Y1 + 0.06, CZ0), (Y1 + 0.06, CZ0 + 0.05),
                       (Y1 + 0.04, CZ0 + 0.06), (Y1 + 0.04, CZ0 + 0.025), (Y1 - 0.10, CZ0 + 0.02)], TRIM)  # sloped tray
for x in (WX0, WX1):                                                                   # tray cheeks
    cab.box((x - 0.012, Y1 - 0.30, CZ0), (x + 0.012, Y1 + 0.06, CZ0 + 0.09), TRIM, 0.003)
# rune plate: iron plate with a coin slot (the use point)
cab.box((PX0, FY1 - 0.01, 0.66), (PX1, FY1 + 0.012, 1.80), TRIM, 0.006)
cab.box((PX0 + 0.06, FY1 + 0.012, 1.58), (PX1 - 0.06, FY1 + 0.016, 1.70), BLACK, 0.002)
# iron straps: corners, waist and top band, with rivets
SO = W2 + 0.032                   # outside of the side straps (over the side panel frames)
for sx in (-1, 1):                # corner angle irons: a strip on the face and one on the side
    for sy, yy in ((-1, Y0), (1, Y1)):
        fy0, fy1 = sorted((yy, yy + sy * 0.012))
        x0, x1 = sorted((sx * (W2 - 0.07), sx * SO))
        cab.box((x0, fy0, Z0), (x1, fy1, H), TRIM, 0.003)
        sx0, sx1 = sorted((sx * (W2 + 0.02), sx * SO))
        y0, y1 = sorted((yy + sy * 0.012, yy - sy * 0.07))
        cab.box((sx0, y0, Z0), (sx1, y1, H), TRIM, 0.003)
for z0, z1 in ((Z0, Z0 + 0.05), (0.64, 0.69), (H - 0.06, H)):
    cab.box((-SO, Y1, z0), (SO, Y1 + 0.012, z1), TRIM, 0.003)
    cab.box((-SO, Y0 - 0.012, z0), (SO, Y0, z1), TRIM, 0.003)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (W2 + 0.02), sx * SO))
        cab.box((x0, Y0, z0), (x1, Y1, z1), TRIM, 0.003)
rivets = []
for x in (-W2 + 0.035, W2 - 0.035):
    for z in (Z0 + 0.2, 0.92, 1.35, 1.75):
        rivets.append((x, Y1 + 0.018, z))
for x in (-0.4, -0.12, 0.16, 0.42):
    for z in (Z0 + 0.025, 0.665, H - 0.03):
        rivets.append((x, Y1 + 0.016, z))
for c in rivets:
    cab.disc(c, 0.011, 0.012, "Y", TRIM, 8)
# crest: a gabled roof across the width with iron ridge cap and end boards
RO = 0.05
cab.prism_x(-W2 - RO, W2 + RO, [(Y0 - RO, H), (Y1 + RO, H), (Y1 + RO, H + 0.04), (0.0, H + 0.26),
                                 (Y0 - RO, H + 0.04)], BODY)
cab.prism_x(-W2 - RO - 0.01, W2 + RO + 0.01, [(-0.035, H + 0.235), (0.035, H + 0.235), (0.0, H + 0.285)], TRIM)
for sx in (-1, 1):                                                                    # carved end posts
    cab.box((sx * (W2 + RO) - 0.025, -0.025, H + 0.18), (sx * (W2 + RO) + 0.025, 0.025, H + 0.36), BODY, 0.01)
cab.box((-0.16, Y1 + RO - 0.01, H + 0.04), (0.16, Y1 + RO + 0.02, H + 0.15), TRIM, 0.005)   # lamp mount
glow_boxes = []                   # (lo, hi) glow pieces for the Lamp mesh
glow_discs = []                   # (centre, radius, depth, axis)
if kind == "haldor":
    # a lantern hanging from an iron arm off the crest, over the window's outer corner
    LX, LY = W2 - 0.09, Y1 + 0.20
    cab.box((LX - 0.015, Y1 - 0.05, H + 0.02), (LX + 0.015, LY + 0.03, H + 0.05), TRIM, 0.004)
    cab.box((LX - 0.006, LY - 0.006, H - 0.07), (LX + 0.006, LY + 0.006, H + 0.02), TRIM, 0.002)
    cab.box((LX - 0.075, LY - 0.075, H - 0.10), (LX + 0.075, LY + 0.075, H - 0.07), TRIM, 0.006)
    cab.prism_x(LX - 0.06, LX + 0.06, [(LY - 0.06, H - 0.07), (LY + 0.06, H - 0.07), (LY, H - 0.03)], TRIM)
    cab.box((LX - 0.07, LY - 0.07, H - 0.35), (LX + 0.07, LY + 0.07, H - 0.32), TRIM, 0.006)
    for dx in (-0.055, 0.055):
        for dy in (-0.055, 0.055):
            cab.box((LX + dx - 0.008, LY + dy - 0.008, H - 0.32), (LX + dx + 0.008, LY + dy + 0.008, H - 0.10), TRIM, 0.002)
    glow_boxes.append(((LX - 0.042, LY - 0.042, H - 0.31), (LX + 0.042, LY + 0.042, H - 0.11)))
elif kind == "bogwitch":
    # twisted roots growing up out of the ridge, like the branches on her poster
    for k in range(9):
        bx = -W2 + 0.08 + k * (2 * W2 - 0.16) / 8 + rng.uniform(-0.04, 0.04)
        p = Vector((bx, rng.uniform(-0.03, 0.03), H + 0.25))
        lean = rng.uniform(0.25, 0.6) * (1 if bx > 0 else -1)
        fwd = rng.uniform(-0.5, 0.5)
        th = 0.036
        for seg in range(3):
            ln = rng.uniform(0.14, 0.22) * (1 - seg * 0.15)
            rot = Matrix.Rotation(lean, 3, "Y") @ Matrix.Rotation(fwd, 3, "X")
            d = rot @ Vector((0, 0, 1))
            cab.box_rot(p + d * ln / 2, (th, th, ln), rot, BODY, 0.006)
            p = p + d * ln * 0.9
            th *= 0.72
            lean *= 1.4
            fwd += rng.uniform(-0.4, 0.4)
    # moss hanging from the front and side eaves
    for k in range(26):
        x = -W2 - 0.03 + k * (2 * W2 + 0.06) / 25 + rng.uniform(-0.015, 0.015)
        ln = rng.uniform(0.06, 0.24)
        cab.box((x - 0.012, Y1 + 0.05, H + 0.01 - ln), (x + 0.012, Y1 + 0.058, H + 0.03), CLOTH, 0)
    for sx in (-1, 1):
        for k in range(10):
            y = Y0 - 0.03 + k * (Y1 - Y0 + 0.06) / 9
            ln = rng.uniform(0.05, 0.2)
            x0, x1 = sorted((sx * (W2 + 0.055), sx * (W2 + 0.063)))
            cab.box((x0, y - 0.012, H + 0.01 - ln), (x1, y + 0.012, H + 0.03), CLOTH, 0)
    # candle ledge over the rune plate
    CX = (PX0 + PX1) / 2
    cab.box((PX0, FY1, 1.815), (PX1, FY1 + 0.09, 1.83), TRIM, 0.003)
    for dx, hgt in ((-0.04, 0.09), (0.035, 0.06)):
        cab.disc((CX + dx, FY1 + 0.05, 1.83 + hgt / 2), 0.018, hgt, "Z", CLOTH2, 10)
        glow_discs.append(((CX + dx, FY1 + 0.05, 1.83 + hgt + 0.012), 0.008, 0.024, "Z"))
else:
    # Hildir's striped awning over the window, with a valance
    AX0, AX1 = WX0 - 0.04, WX1 + 0.04
    zt, zb, ay = 1.965, 1.80, Y1 + 0.30
    n = 8
    for k in range(n):
        x0 = AX0 + (AX1 - AX0) * k / n
        x1 = AX0 + (AX1 - AX0) * (k + 1) / n
        m = CLOTH if k % 2 == 0 else CLOTH2
        cab.prism_x(x0, x1, [(Y1 + 0.01, zt - 0.012), (ay, zb - 0.012), (ay, zb), (Y1 + 0.01, zt)], m)
        cab.prism_x(x0, x1, [(ay - 0.008, zb - 0.075), (ay, zb - 0.075), (ay, zb), (ay - 0.008, zb)], m)
    for x in (AX0 + 0.02, AX1 - 0.02):                                                 # iron stays, face to front edge
        a, b = Vector((x, Y1 + 0.02, zb - 0.14)), Vector((x, ay - 0.01, zb - 0.01))
        rot = (b - a).to_track_quat("Z", "Y").to_matrix()
        cab.box_rot((a + b) / 2, (0.014, 0.014, (b - a).length), rot, TRIM, 0.003)
cabinet = cab.make()

# ---------- Glass ----------
g = Mesh("Glass")
g.box((WX0, GY - 0.006, WZ0), (WX1, GY, WZ1), 0, 0, [glassmat])
g.make([glassmat])

# ---------- Runes and pushers, one per slot ----------
SLOTS = []
for r, z in enumerate(ROWS):
    for c, x in enumerate(COLS):
        i = r * NC + c
        cz = z + 0.175                      # rune above the pusher, in view
        rn = Mesh(f"Rune{i}")
        rn.disc((x, RUNE_Y, cz), 0.036, 0.004, "Y", 0, 16, [glow])
        rn.make([glow])
        p = Mesh(f"Pusher{i}")
        p.box((x - SLOT_W / 2 + 0.015, PUSH_Y0, z + 0.006), (x + SLOT_W / 2 - 0.015, PUSH_Y0 + 0.035, z + 0.085), 0, 0.005, [body])
        p.box((x - 0.01, BACK_IN + 0.004, z + 0.035), (x + 0.01, PUSH_Y0, z + 0.055), 0, 0.002, [body])
        p.make([body])
        SLOTS.append({"i": i, "center": [x, (PUSH_Y0 + SHELF_Y1) / 2 + 0.03, z], "width": SLOT_W - 0.03,
                      "depth": SHELF_Y1 - PUSH_Y0 - 0.04})
PUSH_TRAVEL = SHELF_Y1 - PUSH_Y0 - 0.06

# ---------- Lamp: crest rune and the rune plate dots ----------
lp = Mesh("Lamp")
lp.disc((0, Y1 + RO + 0.024, H + 0.095), 0.04, 0.006, "Y", 0, 16, [glow])
for z in (1.42, 1.30, 1.18, 1.06, 0.94):
    lp.disc(((PX0 + PX1) / 2, FY1 + 0.014, z), 0.018, 0.004, "Y", 0, 12, [glow])
for lo, hi in glow_boxes:
    lp.box(lo, hi, 0, 0.004, [glow])
for c, r, dpt, ax in glow_discs:
    lp.disc(c, r, dpt, ax, 0, 8, [glow])
lp.make([glow])

CHUTE = [(WX0 + WX1) / 2, Y1 - 0.06, (CZ0 + 0.04) * ZS]

# ---------- taller: stretch everything built so far, then wrap the sides at the final size ----------
for o in list(bpy.context.scene.objects):
    if o.type == "MESH":
        for v in o.data.vertices:
            v.co.z *= ZS
for s_ in SLOTS:
    s_["center"][2] *= ZS
# the side wrap: the whole side, edge to edge and over the straps, from the top band down; 9:16 (UV 0..1 = the image)
ART_W = (Y1 - Y0) + 0.028
ART_H = ART_W * 16 / 9
AZ1 = (H - 0.06) * ZS
AZ0 = AZ1 - ART_H
AX = SO + 0.004
art_mesh = Mesh("Art")
for sx in (-1, 1):
    x = sx * AX
    # seen from outside, the image's right runs +Y on the +X side and -Y on the -X side
    ya, yb = (-ART_W / 2, ART_W / 2) if sx > 0 else (ART_W / 2, -ART_W / 2)
    art_mesh.quad([(x, ya, AZ0), (x, yb, AZ0), (x, yb, AZ1), (x, ya, AZ1)], [(0, 0), (1, 0), (1, 1), (0, 1)])
art_obj = art_mesh.make([art], sharp=1)

bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)
for o in bpy.context.scene.objects:
    o.select_set(o.type == "MESH")
bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB", use_selection=True,
                          export_cameras=False, export_lights=False, export_apply=True)

mn = Vector((9, 9, 9)); mx = Vector((-9, -9, -9))
for o in bpy.context.scene.objects:
    if o.type == "MESH":
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
print("VENDINFO " + json.dumps({"kind": kind, "art": [ART_W, ART_H],"min": list(mn), "max": list(mx), "slots": SLOTS, "pushTravel": PUSH_TRAVEL,
                                "chute": CHUTE, "use": [(PX0 + PX1) / 2, FY1, 1.3 * ZS]}))


# ---------- Previews: wood body and iron body ----------
def principled(mat, color, metal=0.0, rough=0.7, emit=None, alpha=1.0):
    mat.use_nodes = True
    b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    b.inputs["Alpha"].default_value = alpha
    if emit:
        b.inputs["Emission Color"].default_value = emit
        b.inputs["Emission Strength"].default_value = 3.0
    return b


def textured(mat, tex):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = D.images.load(tex)
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])


principled(trim, (0.16, 0.16, 0.17, 1), 0.8, 0.5)
principled(black, (0.015, 0.012, 0.01, 1), 0, 0.9)
principled(glassmat, (0.7, 0.9, 1.0, 1), 0, 0.05, alpha=0.12)
glassmat.surface_render_method = "BLENDED"
principled(glow, (0.3, 0.8, 1.0, 1), 0, 0.4, emit=(0.35, 0.85, 1.0, 1))

# stand-in items on the shelves, preview only (not exported)
stand = D.materials.new("Stand")
principled(stand, (0.55, 0.2, 0.12, 1), 0, 0.5)
for s in SLOTS:
    x, y, z = s["center"]
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.05, location=(x, y + 0.04, z + 0.05))
    bpy.context.object.data.materials.append(stand)

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 760, 900
w = D.worlds.new("w")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.42, 0.3, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
ld = D.lights.new("sun", "SUN")
ld.energy = 3.0
lo = D.objects.new("sun", ld)
lo.rotation_euler = (math.radians(50), 0, math.radians(150))
sc.collection.objects.link(lo)
pl = D.lights.new("bay", "POINT")
pl.energy = 15
pl.color = (0.4, 0.85, 1.0)
po = D.objects.new("bay", pl)
po.location = ((WX0 + WX1) / 2, 0.0, 1.35)
sc.collection.objects.link(po)
cd = D.cameras.new("cam")
cam = D.objects.new("cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam


def shoot(path, loc):
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector((0, 0, 1.35)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


import os
BIOME = {"haldor": ((0.36, 0.24, 0.14, 1), (0.55, 0.38, 0.16, 1), 0.9, (0.5, 0.15, 0.1, 1)),
         "bogwitch": ((0.13, 0.11, 0.09, 1), (0.12, 0.12, 0.13, 1), 0.8, (0.22, 0.3, 0.12, 1)),
         "hildir": ((0.40, 0.27, 0.15, 1), (0.16, 0.16, 0.17, 1), 0.8, (0.6, 0.08, 0.06, 1))}[kind]
principled(body, BIOME[0], 0, 0.8)
principled(trim, BIOME[1], BIOME[2], 0.45)
principled(cloth, BIOME[3], 0, 0.9)
principled(cloth2, (0.85, 0.78, 0.62, 1), 0, 0.9)
if not os.path.exists(art_png):
    ph = D.images.new("art_placeholder", 360, 640)
    col = {"haldor": (0.12, 0.14, 0.2, 1), "bogwitch": (0.1, 0.16, 0.1, 1), "hildir": (0.65, 0.1, 0.08, 1)}[kind]
    ph.pixels[:] = list(col) * (360 * 640)
    ph.filepath_raw = art_png
    ph.file_format = "PNG"
    ph.save()
textured(art, art_png)
art.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6
shoot(out_png + "_side.png", (3.8, 1.8, 1.9))
shoot(out_png + "_front.png", (-1.8, 4.0, 2.0))
