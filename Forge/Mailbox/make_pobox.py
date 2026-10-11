"""PO boxes: a cubbyhole wall of lockable post boxes, fully procedural, Valheim-ready (Wood, Metal, Cloth).
  pobox        one box: a small front door with a mail slot and a name plate, a flag lever on the door
               (three objects: Box, Door, Flag; Door and Flag have their origin on the hinge)
  pobox_frame  the cubby hall that PO boxes slot into: 5 wide x 2 high, divider boards, a Norse roof
Args after --: out_blend out_dir(models folder) out_png_prefix
Blender axes: X = width, Y = depth (+Y = front), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y).
Also writes <prefix>_box*.png and <prefix>_wall*.png previews (the wall preview fills every socket with a box)."""
import bpy, bmesh, math, sys, random, json
from mathutils import Vector, Matrix

out_blend, out_dir, out_png = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19
rng = random.Random(7)
wood = D.materials.new("Wood")
metal = D.materials.new("Metal")
cloth = D.materials.new("Cloth")      # the flag: tinted green / red in game
black = D.materials.new("Black")      # plain black for the inside of the cubby


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



W, M, C = 0, 1, 2
BW, BH, BD = 0.34, 0.30, 0.32            # one box: width, height, depth (a socket is a little bigger)
PITCH_X, PITCH_Z = 0.37, 0.33


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
    # a plain black lining on every inner surface, so an open door shows a dark cavity
    e = 0.005
    ix0, ix1, iy0, iy1, iz0, iz1 = x0 + t, x1 - t, y0_ + t, y1_ - t, z0 + t, z1 - t
    parts.append(((ix0, iy0, iz0), (ix1, iy1, iz0 + e), 3, 0.001))
    parts.append(((ix0, iy0, iz1 - e), (ix1, iy1, iz1), 3, 0.001))
    parts.append(((ix0, iy0, iz0), (ix0 + e, iy1, iz1), 3, 0.001))
    parts.append(((ix1 - e, iy0, iz0), (ix1, iy1, iz1), 3, 0.001))
    far = (iy1 - e, iy1) if hole_side == "back" else (iy0, iy0 + e)
    parts.append(((ix0, far[0], iz0), (ix1, far[1], iz1), 3, 0.001))


def pobox_parts():
    """Returns (box_parts, door_parts, flag_parts, door_hinge, flag_hinge), box centred on x = 0, bottom at z = 0, front +Y."""
    hw, d = BW / 2, BD / 2
    box, door, flag = [], [], []
    hollow_box(box, -hw, hw, -d, d - 0.02, 0.0, BH, (-hw + 0.03, hw - 0.03, 0.035, BH - 0.035), "front", t=0.025, bev=0.006)   # cubby body, open at the door
    box.append(((-hw - 0.01, d - 0.03, -0.0), (hw + 0.01, d - 0.015, 0.02), M, 0.003))         # lip under the door
    box.append(((-hw - 0.01, d - 0.03, BH - 0.02), (hw + 0.01, d - 0.015, BH), M, 0.003))      # lip over the door
    # door: wood panel with a mail slot, a name plate, hinge straps and a pull
    door.append(((-hw + 0.012, d - 0.02, 0.022), (hw - 0.012, d + 0.006, BH - 0.022), W, 0.006))
    door.append(((-0.095, d + 0.004, 0.115), (0.095, d + 0.012, 0.150), M, 0.003))               # slot plate
    door.append(((-0.080, d + 0.011, 0.126), (0.080, d + 0.016, 0.138), M, 0.001))               # the black mouth
    door.append(((-0.090, d + 0.004, 0.205), (0.090, d + 0.011, 0.265), M, 0.003))               # name plate frame
    door.append(((-0.080, d + 0.010, 0.213), (0.080, d + 0.016, 0.257), W, 0.003))               # name plate (the label goes here)
    for z in (0.05, 0.22):
        door.append(((-hw - 0.004, d + 0.002, z), (-hw + 0.085, d + 0.010, z + 0.026), M, 0.003))  # hinge straps
    door.append(((hw - 0.075, d + 0.004, 0.060), (hw - 0.045, d + 0.020, 0.090), M, 0.003))     # pull
    hinge = (-hw, d + 0.002, BH / 2)
    # flag lever on the right edge of the door: out = mail waiting, hanging = empty
    flag.append(((hw - 0.004, d + 0.006, 0.17), (hw + 0.075, d + 0.018, 0.205), C, 0.003))
    flag_hinge = (hw - 0.004, d + 0.012, 0.19)
    return box, door, flag, hinge, flag_hinge


def frame_parts(cols=5, rows=2):
    """The cubby hall: plinth, back, dividers, roof. Sockets are PITCH apart; the first socket centre is at x = -(cols-1)/2 * PITCH."""
    parts = []
    t = 0.028                                           # board thickness
    inner_w = cols * PITCH_X
    inner_h = rows * PITCH_Z
    fw = inner_w + 2 * t
    z0 = 0.10                                           # plinth height
    d = BD / 2 + 0.03
    parts.append(((-fw / 2 - 0.03, -d, 0.0), (fw / 2 + 0.03, d, z0), W, 0.012))               # plinth
    parts.append(((-fw / 2 - 0.035, -d - 0.005, z0 - 0.02), (fw / 2 + 0.035, d + 0.005, z0), M, 0.004))   # plinth band
    parts.append(((-fw / 2, -d, z0), (fw / 2, -d + 0.03, z0 + inner_h + 2 * t), W, 0.008))      # back board
    for i in range(cols + 1):                                                                    # verticals (and the two sides)
        x = -inner_w / 2 + i * PITCH_X
        parts.append(((x - t / 2, -d, z0), (x + t / 2, d, z0 + inner_h + 2 * t), W, 0.008))
    for j in range(rows + 1):                                                                    # shelves (floor, between rows, top)
        z = z0 + j * PITCH_Z + t * (j > 0)
        parts.append(((-fw / 2, -d, z), (fw / 2, d, z + t), W, 0.008))
    top = z0 + inner_h + 2 * t
    for x in (-fw / 2, fw / 2):                                                                  # iron corner caps
        parts.append(((x - 0.02, -d - 0.006, z0), (x + 0.02, d + 0.006, z0 + 0.04), M, 0.003))
        parts.append(((x - 0.02, -d - 0.006, top - 0.04), (x + 0.02, d + 0.006, top), M, 0.003))
    # Norse roof: two boards, ridge and crossed horns
    import math as _m
    rise, over = 24, 0.10
    slope = (d + over) / _m.cos(_m.radians(rise))
    zc = top + 0.05
    for sign in (1, -1):
        yc = sign * (d / 2 + over / 2)
        parts.append(((-fw / 2 - 0.08, yc - slope / 2, zc - 0.02), (fw / 2 + 0.08, yc + slope / 2, zc + 0.02), W, 0.01, (-sign * rise, 0, 0)))
    zr = zc + d * _m.tan(_m.radians(rise)) + 0.04
    parts.append(((-fw / 2 - 0.07, -0.05, zr - 0.05), (fw / 2 + 0.07, 0.05, zr + 0.02), W, 0.012))
    for x in (-fw / 2 - 0.07, fw / 2 + 0.07):
        for lean in (-24, 24):
            parts.append(((x - 0.016, -0.016, zr - 0.02), (x + 0.016, 0.016, zr + 0.20), W, 0.006, (0, lean, 0)))
    return parts, z0, top, fw


def make_object(name, plist, hinge=None):
    me = build(name, plist)
    obj = D.objects.new(name, me)
    if hinge is not None:
        h = Vector(hinge)
        me.transform(Matrix.Translation(-h))
        obj.location = h
    bpy.context.scene.collection.objects.link(obj)
    return obj


sc = bpy.context.scene
box_parts, door_parts, flag_parts, door_hinge, flag_hinge = pobox_parts()
frame, z0, ztop, fw = frame_parts()

# ---- the objects: one box (3 parts) and the frame
obj_box = make_object("Box", box_parts)
obj_door = make_object("Door", door_parts, door_hinge)
obj_flag = make_object("Flag", flag_parts, flag_hinge)
obj_frame = make_object("Frame", frame)
box_objects = [obj_box, obj_door, obj_flag]

# the open clip: door swings out, flag goes from hanging to out
sc.frame_start, sc.frame_end = 1, 40
for obj, axis, degrees in ((obj_door, 2, -110), (obj_flag, 1, 90)):
    for frame_no, amount in ((1, 0.0), (20, 1.0), (28, 1.0), (40, 0.0)):
        angle = [0.0, 0.0, 0.0]
        angle[axis] = math.radians(degrees) * amount
        obj.rotation_euler = angle
        obj.keyframe_insert("rotation_euler", frame=frame_no)
    obj.animation_data.action.name = obj.name + "_open"
sc.frame_set(1)
obj_frame.hide_set(True)
obj_frame.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)
obj_frame.hide_set(False)
obj_frame.hide_render = False


def export(path, objects, animations=False):
    for o in sc.objects:
        o.select_set(o in objects)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_cameras=False,
                              export_lights=False, export_apply=True, export_animations=animations)


import os
os.makedirs(out_dir, exist_ok=True)
export(os.path.join(out_dir, "pobox.glb"), box_objects)
export(os.path.join(out_dir, "pobox_frame.glb"), [obj_frame])
export(os.path.join(out_dir, "pobox_anim.glb").replace("models", "reference"), box_objects, True)
print("POBOXINFO " + json.dumps({"box": [BW, BD, BH], "pitch": [PITCH_X, PITCH_Z], "frame_width": fw, "frame_top": ztop, "plinth": z0}))


# ---------- preview ----------
def paint(mat, color, rough, metallic=0.0):
    mat.use_nodes = True
    b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metallic


paint(wood, (0.22, 0.12, 0.06, 1), 0.8)
paint(metal, (0.10, 0.10, 0.11, 1), 0.5, 0.8)
paint(cloth, (0.70, 0.10, 0.08, 1), 0.9)
paint(black, (0.0, 0.0, 0.0, 1), 1.0)
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1000, 760
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
paint(gmat, (0.15, 0.24, 0.10, 1), 1.0)
ground.materials.append(gmat)
sc.collection.objects.link(D.objects.new("ground", ground))
cd = D.cameras.new("cam")
cam = D.objects.new("cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam


def shoot(name, target, location):
    cam.location = Vector(location)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = f"{out_png}_{name}.png"
    bpy.ops.render.render(write_still=True)


# a wall: a box in every socket (preview copies; the first two are the real objects)
cols, rows = 5, 2
base_locations = [Vector(o.location) for o in box_objects]
placed = []
for r in range(rows):
    for c in range(cols):
        x = (c - (cols - 1) / 2) * PITCH_X
        z = z0 + 0.028 + r * PITCH_Z + 0.01
        if r == 0 and c == 0:
            group = box_objects
        else:
            group = []
            for o in box_objects:
                copy = D.objects.new(o.name, o.data)
                sc.collection.objects.link(copy)
                group.append(copy)
        for k, o in enumerate(group):
            o.location = base_locations[k] + Vector((x, 0, z))
        placed.append(group)
# lowered flags on most boxes, a few raised, a few doors ajar
for idx, group in enumerate(placed):
    if idx % 3 == 0:
        group[2].rotation_euler = (0, 0, 0)
    else:
        group[2].rotation_euler = (0, math.radians(90), 0)
for idx in (2, 7):
    placed[idx][1].rotation_euler = (0, 0, math.radians(-95))

shoot("wall", (0, 0, 0.5), (1.6, 3.0, 1.5))
shoot("wall_side", (0, 0, 0.5), (-2.4, 2.2, 1.3))
# close-up of one box, door open
for idx, group in enumerate(placed):
    if idx != 0:
        for o in group:
            o.hide_render = True
obj_frame.hide_render = True
bx = placed[0][0].location.x
shoot("box", (bx, 0.1, z0 + 0.2), (bx + 0.55, 0.95, z0 + 0.55))
