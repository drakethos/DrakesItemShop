"""Drop box mailboxes, fully procedural, Valheim-ready: one mesh "Mailbox" with two material slots, Wood and Metal
(mapped in-game to wood_window / iron_grate like the racks). Two models:
  post     a Norse mailbox on a post: public send slot on the front (+Y), owner door on the back (-Y)
  tabletop a small drop box that sits on any table or counter (a shop stall), same public/owner sides
Args after --: out_blend out_glb out_png_prefix kind(post|tabletop)
Blender axes: X = width, Y = depth (+Y = public front), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y).
Three objects, so the game can animate the two openings: "Mailbox" (the body), "Flap" (front send flap, origin on its hinge,
open = turn about X) and "Door" (back owner door, origin on its hinge, open = turn about Z) and, on the post, "Flag" (a third material, Cloth: raised
when there is mail, lowered when empty; tinted green / red in game). The .blend also holds a 40 frame
"open" animation on both. Writes <prefix>_front/back(_open).png previews; reference/<name>_anim.glb carries the clips (kept out of models/ so only the closed models ship)."""
import bpy, bmesh, math, sys, random, json
from mathutils import Vector, Matrix

out_blend, out_glb, out_png, kind = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19
rng = random.Random(7)
wood = D.materials.new("Wood")
metal = D.materials.new("Metal")
cloth = D.materials.new("Cloth")      # the flag: tinted green / red in game
black = D.materials.new("Black")      # plain black for the inside of the cavities


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


def build(name, parts):
    """parts: (lo, hi, material 0 wood / 1 metal, bevel[, (rx, ry, rz) degrees about the part's centre])."""
    me = D.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for part in parts:
        lo, hi, mat, bev = part[:4]
        rot = part[4] if len(part) > 4 else (0, 0, 0)
        lo, hi = Vector(lo), Vector(hi)
        before = set(bm.faces)
        r = bmesh.ops.create_cube(bm, size=1.0)
        centre = (lo + hi) / 2
        turn = Matrix.Rotation(math.radians(rot[2]), 3, "Z") @ Matrix.Rotation(math.radians(rot[1]), 3, "Y") @ Matrix.Rotation(math.radians(rot[0]), 3, "X")
        dims = hi - lo
        for v in r["verts"]:
            local = Vector((v.co.x * dims.x, v.co.y * dims.y, v.co.z * dims.z))
            v.co = centre + turn @ local
        if bev:
            edges = list({e for v in r["verts"] for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=edges, offset=min(bev, min(dims) * 0.3), segments=2, profile=0.5,
                            affect="EDGES", clamp_overlap=True)
        new = [f for f in bm.faces if f not in before]
        bm.normal_update()
        for f in new:
            f.material_index = mat
        if mat == 0:
            wood_uv(uv, new, max(range(3), key=lambda i: dims[i]))
        else:
            for f in new:
                for l in f.loops:
                    l[uv].uv = (l.vert.co.x + l.vert.co.y, l.vert.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(wood)
    me.materials.append(metal)
    me.materials.append(cloth)
    me.materials.append(black)
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))
    return me


W, M = 0, 1
parts = []        # the body
flap_parts = []   # front send flap
flag_parts = []   # the mail flag on the post mailbox: up when there is mail
door_parts = []   # back owner door
HINGES = {}       # name -> (world position of the hinge, rotation axis index, open angle in degrees)


def hollow_box(parts, x0, x1, y0_, y1_, z0, z1, hole, hole_side, t=0.03, bev=0.01):
    """A box of boards with a rectangular opening in one long face, so the door reveals a cavity.
    hole = (x_lo, x_hi, z_lo, z_hi) on the face hole_side ('back' = -Y, 'front' = +Y)."""
    parts.append(((x0, y0_, z0), (x1, y1_, z0 + t), W, bev))                    # floor
    parts.append(((x0, y0_, z1 - t), (x1, y1_, z1), W, bev))                    # top
    parts.append(((x0, y0_, z0), (x0 + t, y1_, z1), W, bev))                    # left wall
    parts.append(((x1 - t, y0_, z0), (x1, y1_, z1), W, bev))                    # right wall
    solid = (y1_ - t, y1_) if hole_side == "back" else (y0_, y0_ + t)           # the face with no opening
    parts.append(((x0, solid[0], z0), (x1, solid[1], z1), W, bev))
    face = (y0_, y0_ + t) if hole_side == "back" else (y1_ - t, y1_)            # the face with the opening: four strips round it
    xl, xh, zl, zh = hole
    parts.append(((x0, face[0], z0), (xl, face[1], z1), W, bev))
    parts.append(((xh, face[0], z0), (x1, face[1], z1), W, bev))
    parts.append(((xl, face[0], z0), (xh, face[1], zl), W, bev))
    parts.append(((xl, face[0], zh), (xh, face[1], z1), W, bev))
    # a plain black lining on every inner surface, so an open door shows a deep dark cavity, not bare wood
    e = 0.005
    ix0, ix1, iy0, iy1, iz0, iz1 = x0 + t, x1 - t, y0_ + t, y1_ - t, z0 + t, z1 - t
    parts.append(((ix0, iy0, iz0), (ix1, iy1, iz0 + e), 3, 0.001))                  # floor
    parts.append(((ix0, iy0, iz1 - e), (ix1, iy1, iz1), 3, 0.001))                  # ceiling
    parts.append(((ix0, iy0, iz0), (ix0 + e, iy1, iz1), 3, 0.001))                  # left
    parts.append(((ix1 - e, iy0, iz0), (ix1, iy1, iz1), 3, 0.001))                  # right
    far = (iy1 - e, iy1) if hole_side == "back" else (iy0, iy0 + e)
    parts.append(((ix0, far[0], iz0), (ix1, far[1], iz1), 3, 0.001))                # far wall, straight behind the opening


def drop_box(cx, y0, z0, w=0.74, d=0.46, h=0.50, horns=True):
    """The box itself: planked body, iron bands, public send slot on +Y, owner door on -Y, flag on the +X side.
    (cx, y0) = centre of the box footprint; z0 = bottom. Returns the top z."""
    x0, x1 = cx - w / 2, cx + w / 2
    yf, yb = y0 + d / 2, y0 - d / 2
    z1 = z0 + h
    hollow_box(parts, x0, x1, yb, yf, z0, z1, (cx - 0.26, cx + 0.26, z0 + 0.06, z1 - 0.15), "back")   # body, open at the back door
    # vertical plank seams on both long faces (shallow grooves read as boards)
    for i in range(1, 6):
        x = x0 + w * i / 6
        parts.append(((x - 0.004, yf, z0 + 0.02), (x + 0.004, yf + 0.006, z1 - 0.02), W, 0.002))
        parts.append(((x - 0.004, yb - 0.006, z0 + 0.02), (x + 0.004, yb, z1 - 0.02), W, 0.002))
    for z in (z0 + 0.09, z1 - 0.12):                                                       # iron bands wrap the whole box
        parts.append(((x0 - 0.008, yb - 0.01, z), (x1 + 0.008, yf + 0.01, z + 0.05), M, 0.006))

    # ---- front (public): send slot with a hinged flap, a small plaque under it
    sx0, sx1 = cx - 0.20, cx + 0.20
    sz = z0 + h * 0.58
    parts.append(((sx0 - 0.02, yf, sz - 0.04), (sx1 + 0.02, yf + 0.02, sz + 0.10), M, 0.006))        # slot plate
    parts.append(((sx0, yf + 0.018, sz), (sx1, yf + 0.024, sz + 0.05), M, 0.002))                  # the dark mouth (metal reads black)
    parts.append(((sx0 - 0.015, yf + 0.02, sz + 0.075), (sx1 + 0.015, yf + 0.045, sz + 0.095), M, 0.003))  # hinge rod (fixed)
    flap_parts.append(((sx0 - 0.01, yf + 0.024, sz - 0.01), (sx1 + 0.01, yf + 0.046, sz + 0.085), W, 0.005))  # flap covering the mouth
    flap_parts.append(((cx - 0.03, yf + 0.044, sz - 0.012), (cx + 0.03, yf + 0.056, sz + 0.012), M, 0.003))  # flap pull
    HINGES["Flap"] = ((cx, yf + 0.035, sz + 0.085), 0, 68)
    parts.append(((cx - 0.09, yf, z0 + 0.07), (cx + 0.09, yf + 0.016, z0 + 0.15), W, 0.006))         # plaque
    parts.append(((cx - 0.095, yf + 0.014, z0 + 0.065), (cx + 0.095, yf + 0.022, z0 + 0.155), M, 0.003))  # plaque frame (iron)

    # ---- back (owner): framed door, hinges, hasp and ring
    dx0, dx1 = cx - 0.26, cx + 0.26
    door_parts.append(((dx0, yb - 0.022, z0 + 0.06), (dx1, yb, z1 - 0.15), W, 0.01))                      # door
    for z in (z0 + 0.14, z1 - 0.23):
        door_parts.append(((dx0 - 0.01, yb - 0.03, z), (dx0 + 0.16, yb - 0.018, z + 0.035), M, 0.004))    # strap hinges
    door_parts.append(((dx1 - 0.10, yb - 0.034, (z0 + z1) / 2 - 0.10), (dx1 - 0.01, yb - 0.02, (z0 + z1) / 2 - 0.02), M, 0.004))  # hasp plate
    door_parts.append(((dx1 - 0.075, yb - 0.05, (z0 + z1) / 2 - 0.085), (dx1 - 0.035, yb - 0.03, (z0 + z1) / 2 - 0.045), M, 0.004))  # lock
    door_parts.append(((cx - 0.02, yb - 0.05, (z0 + z1) / 2 - 0.02), (cx + 0.02, yb - 0.03, (z0 + z1) / 2 + 0.06), M, 0.004))      # pull
    HINGES["Door"] = ((dx0, yb - 0.011, (z0 + 0.06 + z1 - 0.15) / 2), 2, -105)

    # ---- flag on the +X side (up = mail waiting)
    parts.append(((x1, y0 - 0.012, z0 + 0.12), (x1 + 0.03, y0 + 0.012, z0 + 0.42), M, 0.004))      # pivot post
    flag_parts.append(((x1 + 0.03, y0 - 0.008, z0 + 0.36), (x1 + 0.20, y0 + 0.008, z0 + 0.46), 2, 0.004))  # flag cloth
    flag_parts.append(((x1 + 0.17, y0 - 0.012, z0 + 0.34), (x1 + 0.20, y0 + 0.012, z0 + 0.48), 2, 0.004))  # flag tip
    HINGES["Flag"] = ((x1 + 0.03, y0, z0 + 0.41), 1, 90)    # lowered = turned down 90 degrees about the depth axis

    # ---- Norse roof: two steep boards, ridge beam, carved gable horns
    rise = 22
    ov = 0.08
    slope = (d / 2 + ov) / math.cos(math.radians(rise))
    zc = z1 + 0.06
    for sign in (1, -1):
        yc = y0 + sign * (d / 4 + ov / 2)
        parts.append(((x0 - 0.07, yc - slope / 2, zc - 0.022), (x1 + 0.07, yc + slope / 2, zc + 0.022), W, 0.01,
                      (-sign * rise, 0, 0)))
    parts.append(((x0 - 0.06, y0 - 0.05, zc + d / 2 * math.tan(math.radians(rise)) - 0.02),
                  (x1 + 0.06, y0 + 0.05, zc + d / 2 * math.tan(math.radians(rise)) + 0.05), W, 0.012))   # ridge beam
    zr = zc + d / 2 * math.tan(math.radians(rise)) + 0.05                                              # ridge top
    for x in ((x0 - 0.06, x1 + 0.06) if horns else ()):                                                  # crossed gable horns
        for lean in (-24, 24):
            parts.append(((x - 0.018, y0 - 0.018, zr - 0.03), (x + 0.018, y0 + 0.018, zr + 0.22), W, 0.006, (0, lean, 0)))
    return z1 + 0.30


if kind == "post":
    parts.append(((-0.07, -0.07, 0.0), (0.07, 0.07, 1.00), W, 0.012))                    # post
    parts.append(((-0.13, -0.13, 0.0), (0.13, 0.13, 0.10), W, 0.012))                    # foot block
    parts.append(((-0.145, -0.145, 0.08), (0.145, 0.145, 0.12), M, 0.006))               # foot band
    top = drop_box(0.0, 0.0, 1.0)
    BOUNDS = [-0.62, -0.30, 0.0, 0.62, 0.30, top]
else:
    # sits on a table: a squat iron-banded ballot box, 0.36 wide, 0.28 deep, 0.30 tall; a wide slot in the lid on the public
    # side, owner door with a lock on the back; origin = centre of its underside
    bw, bd, bh = 0.18, 0.14, 0.27                                                      # half width, half depth, body height
    hollow_box(parts, -bw, bw, -bd, bd, 0.02, bh, (-0.13, 0.13, 0.06, bh - 0.03), "back", t=0.025, bev=0.008)   # body, open at the back door
    for sx in (-1, 1):                                                                 # feet
        for sy in (-1, 1):
            parts.append(((sx * (bw - 0.04) - 0.03, sy * (bd - 0.04) - 0.03, 0.0), (sx * (bw - 0.04) + 0.03, sy * (bd - 0.04) + 0.03, 0.03), W, 0.005))
    parts.append(((-bw - 0.02, -bd - 0.02, bh), (bw + 0.02, bd + 0.02, bh + 0.035), W, 0.01))   # lid, overhanging
    parts.append(((-bw - 0.025, -bd - 0.025, bh + 0.03), (bw + 0.025, bd + 0.025, bh + 0.045), M, 0.004))  # lid rim
    # the slot: raised iron frame round a black mouth, a hinged flap on the public side
    parts.append(((-0.11, -0.025, bh + 0.045), (0.11, 0.085, bh + 0.052), M, 0.002))   # mouth (dark)
    for x0_, x1_, y0_, y1_ in ((-0.125, 0.125, 0.085, 0.100), (-0.125, 0.125, -0.040, -0.025),
                                (-0.125, -0.110, -0.040, 0.100), (0.110, 0.125, -0.040, 0.100)):
        parts.append(((x0_, y0_, bh + 0.045), (x1_, y1_, bh + 0.068), M, 0.003))        # frame bars
    flap_parts.append(((-0.118, -0.034, bh + 0.052), (0.118, 0.092, bh + 0.066), W, 0.004))   # lid flap over the mouth, hinged at the back
    flap_parts.append(((-0.03, 0.078, bh + 0.064), (0.03, 0.098, bh + 0.074), M, 0.003))       # flap pull
    HINGES["Flap"] = ((0.0, -0.034, bh + 0.059), 0, 72)
    # iron: vertical straps front and back, corner brackets, a lower band
    for x in (-0.11, 0.11):
        parts.append(((x - 0.022, bd, 0.03), (x + 0.022, bd + 0.012, bh), M, 0.004))
        parts.append(((x - 0.022, -bd - 0.012, 0.03), (x + 0.022, -bd, bh), M, 0.004))
    parts.append(((-bw - 0.012, -bd - 0.012, 0.07), (bw + 0.012, bd + 0.012, 0.11), M, 0.004))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(((sx * bw - 0.02 + sx * 0.012, sy * bd - 0.02 + sy * 0.012, 0.02), (sx * bw + 0.02 + sx * 0.012, sy * bd + 0.02 + sy * 0.012, bh + 0.02), M, 0.004))
    # public front: plaque with an iron frame
    parts.append(((-0.07, bd + 0.012, 0.13), (0.07, bd + 0.026, 0.23), W, 0.005))
    parts.append(((-0.075, bd + 0.022, 0.125), (0.075, bd + 0.030, 0.235), M, 0.003))
    # owner back: door, hinges, hasp, lock, pull
    door_parts.append(((-0.13, -bd - 0.024, 0.06), (0.13, -bd, bh - 0.03), W, 0.008))
    for z in (0.09, 0.20):
        door_parts.append(((-0.14, -bd - 0.034, z), (-0.03, -bd - 0.022, z + 0.03), M, 0.004))
    door_parts.append(((0.07, -bd - 0.036, 0.13), (0.13, -bd - 0.024, 0.20), M, 0.004))
    door_parts.append(((0.085, -bd - 0.055, 0.145), (0.115, -bd - 0.034, 0.18), M, 0.004))
    door_parts.append(((-0.01, -bd - 0.05, 0.12), (0.01, -bd - 0.03, 0.19), M, 0.004))
    HINGES["Door"] = ((-0.13, -bd - 0.012, (0.06 + bh - 0.03) / 2), 2, -105)
    top = bh + 0.07
    BOUNDS = [-bw - 0.04, -bd - 0.06, 0.0, bw + 0.04, bd + 0.03, top]

mailbox = D.objects.new("Mailbox", build("Mailbox", parts))
bpy.context.scene.collection.objects.link(mailbox)
movers = {}
for part_name, plist in (("Flap", flap_parts), ("Door", door_parts), ("Flag", flag_parts)):
    if not plist:
        continue
    hinge = Vector(HINGES[part_name][0])
    me = build(part_name, plist)
    me.transform(Matrix.Translation(-hinge))          # origin on the hinge line, so rotating it swings it open
    obj = D.objects.new(part_name, me)
    obj.location = hinge
    bpy.context.scene.collection.objects.link(obj)
    movers[part_name] = obj

# the "open" clip: closed at 1, open at 20, held, closed again at 40
sc = bpy.context.scene
sc.frame_start, sc.frame_end = 1, 40
for part_name, obj in movers.items():
    _, axis, degrees = HINGES[part_name]
    for frame, amount in ((1, 0.0), (20, 1.0), (28, 1.0), (40, 0.0)):
        angle = [0.0, 0.0, 0.0]
        angle[axis] = math.radians(degrees) * amount
        obj.rotation_euler = angle
        obj.keyframe_insert("rotation_euler", frame=frame)
    obj.animation_data.action.name = part_name + "_open"
sc.frame_set(1)

bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)


def export(path, animations):
    for o in bpy.context.scene.objects:
        o.select_set(o.type == "MESH")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_cameras=False, export_lights=False, export_apply=True,
                              export_animations=animations)


export(out_glb, False)                                    # what the game uses: three parts, closed
export(out_glb.replace("models/", "reference/").replace(".glb", "_anim.glb"), True)   # the same with the open clip, for reference
print("MAILINFO " + json.dumps({"kind": kind, "bounds": BOUNDS}))


# ---------- preview ----------
def textured(mat, color, rough, metallic=0.0):
    mat.use_nodes = True
    b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metallic


if kind == "tabletop":
    mailbox.location.z = 0.80
    for mover in movers.values():
        mover.location.z += 0.80
    tab = build("Table", [((-0.7, -0.4, 0.74), (0.7, 0.4, 0.80), W, 0.01)] +
                [((sx * 0.62 - 0.04, sy * 0.32 - 0.04, 0.0), (sx * 0.62 + 0.04, sy * 0.32 + 0.04, 0.74), W, 0.01) for sx in (-1, 1) for sy in (-1, 1)])
    table = D.objects.new("PreviewTable", tab)
    sc0 = bpy.context.scene
    sc0.collection.objects.link(table)
    BOUNDS[5] += 0.80
textured(wood, (0.22, 0.12, 0.06, 1), 0.8)
textured(metal, (0.10, 0.10, 0.11, 1), 0.5, 0.8)
textured(cloth, (0.70, 0.10, 0.08, 1), 0.9)
textured(black, (0.0, 0.0, 0.0, 1), 1.0)

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 900, 760
w = D.worlds.new("w")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.30, 0.38, 0.26, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.4
ld = D.lights.new("sun", "SUN")
ld.energy = 3.2
sun = D.objects.new("sun", ld)
sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
sc.collection.objects.link(sun)
ground = D.meshes.new("g")
gm = bmesh.new()
bmesh.ops.create_grid(gm, x_segments=1, y_segments=1, size=3)
gm.to_mesh(ground)
gm.free()
gmat = D.materials.new("Grass")
textured(gmat, (0.15, 0.24, 0.10, 1), 1.0)
ground.materials.append(gmat)
go = D.objects.new("ground", ground)
sc.collection.objects.link(go)

cd = D.cameras.new("cam")
cam = D.objects.new("cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam
cz = BOUNDS[5] * 0.5
far = max(2.4, BOUNDS[5] * 2.0, (BOUNDS[3] - BOUNDS[0]) * 2.0)
if kind == "tabletop":                    # close in on the box (the table under it is only for scale)
    cz, far = 0.80 + 0.17, 0.95
for state, frame in (("", 1), ("_open", 20)):
    sc.frame_set(frame)
    for name, sign in (("front", 1), ("back", -1)):
        cam.location = Vector((far * 0.55, sign * far, cz + (0.45 if kind == "tabletop" else 0.35)))
        cam.rotation_euler = (Vector((0, 0, cz)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = f"{out_png}_{name}{state}.png"
        bpy.ops.render.render(write_still=True)
