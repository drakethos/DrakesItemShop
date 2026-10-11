"""Modular post wall: sections with PO boxes built in, side trims, roof segments and a roof end (Blender 5.1).
  section CxR  C boxes wide, R tall; outer boards are half thickness so neighbours join into normal dividers
  side N       a side board N cells tall (finishes the end of a run; same piece both sides, rotate it)
  roof N       a roof segment N cells wide, sits on top of a section
  roof end     the gable end with the crossed Norse horns
Every piece lives on one grid (CELL_W x CELL_H), so all of them snap together.
Args after --: pack_dir (Forge/Mailbox) preview_png
Writes models/post_*.glb, items/Drake_Post*.json (look, collision, snap points) and prints POSTWALL (cells per section).
Blender axes: X = width, Y = depth (+Y = front), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y)."""
import bpy, bmesh, math, sys, random, json, os
from mathutils import Vector, Matrix

pack_dir, out_png = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19
rng = random.Random(11)
wood = D.materials.new("Wood")
metal = D.materials.new("Metal")
cloth = D.materials.new("Cloth")
W, M, C = 0, 1, 2

CELL_W, CELL_H = 0.38, 0.36    # one socket; a PO box is 0.34 x 0.30 x 0.32
T = 0.028                      # board thickness (half of it on a section's outer edges)
DEPTH = 0.19                   # half depth of the wall
RISE, OVER = 24, 0.10          # roof pitch (degrees) and how far it overhangs front and back
END = 0.08                     # how far the roof end sticks out past the side trim


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
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))
    return me


def section_parts(cols, rows):
    """Origin: bottom centre of the back-to-front middle. Outer boards are half thickness."""
    w, h, d = cols * CELL_W, rows * CELL_H, DEPTH
    parts = [((-w / 2, -d, 0), (w / 2, -d + 0.03, h), W, 0.004)]                               # back board
    for i in range(cols + 1):                                                                  # dividers
        x = -w / 2 + i * CELL_W
        lo = x - (T / 2 if i > 0 else 0)
        hi = x + (T / 2 if i < cols else 0)
        parts.append(((lo, -d, 0), (hi, d, h), W, 0.004))
    for j in range(rows + 1):                                                                  # shelves
        z = j * CELL_H
        lo = z - (T / 2 if j > 0 else 0)
        hi = z + (T / 2 if j < rows else 0)
        parts.append(((-w / 2, -d, lo), (w / 2, d, hi), W, 0.004))
    cells = [(-w / 2 + (c + 0.5) * CELL_W, r * CELL_H + T / 2 + 0.002) for r in range(rows) for c in range(cols)]
    return parts, cells


def side_parts(rows):
    """Origin: bottom, on the face that meets the section; the board goes out along +X."""
    h, d = rows * CELL_H, DEPTH
    parts = [((0, -d - 0.01, 0), (T, d + 0.01, h), W, 0.006)]
    for z0, z1 in ((0, 0.04), (h - 0.04, h)):                                                  # iron caps
        parts.append(((-0.004, -d - 0.016, z0), (T + 0.012, d + 0.016, z1), M, 0.003))
    return parts


def roof_boards(x0, x1):
    d = DEPTH
    slope = (d + OVER) / math.cos(math.radians(RISE))
    zc = T / 2 + 0.05
    parts = []
    for sign in (1, -1):
        yc = sign * (d / 2 + OVER / 2)
        parts.append(((x0, yc - slope / 2, zc - 0.02), (x1, yc + slope / 2, zc + 0.02), W, 0.01, (-sign * RISE, 0, 0)))
    zr = zc + d * math.tan(math.radians(RISE)) + 0.04
    parts.append(((x0, -0.05, zr - 0.05), (x1, 0.05, zr + 0.02), W, 0.012))                    # ridge
    return parts, zc, zr


def roof_parts(cols):
    """Origin: bottom centre (= the top of the section under it). Also tops up the section's half-thick top shelf."""
    w, d = cols * CELL_W, DEPTH
    parts = [((-w / 2, -d, 0), (w / 2, d, T / 2), W, 0.004)]
    boards, _, _ = roof_boards(-w / 2, w / 2)
    return parts + boards


def roof_end_parts():
    """Origin: bottom, on the face that meets the roof; sticks out along +X past the side trim."""
    d = DEPTH
    x1 = T + END
    parts = [((0, -d - 0.01, 0), (T, d + 0.01, T / 2), W, 0.004)]
    boards, zc, zr = roof_boards(0, x1)
    parts += boards
    parts.append(((0, -d + 0.02, T / 2), (T, d - 0.02, zc), W, 0.004))                        # gable board under the roof
    parts.append(((0, -d * 0.55, zc), (T, d * 0.55, zr - 0.03), W, 0.004))
    hx = x1 - 0.01
    for lean in (-24, 24):                                                                     # crossed horns
        parts.append(((hx - 0.016, -0.016, zr - 0.02), (hx + 0.016, 0.016, zr + 0.20), W, 0.006, (0, lean, 0)))
    return parts


def unity(p):
    """Blender (x, y, z) -> Unity (x, z, y), rounded for the recipe."""
    return [round(p[0], 4), round(p[2], 4), round(p[1], 4)]


def recipe(piece_id, name, glb, center, size, snaps):
    return {
        "id": piece_id, "kind": "piece", "base": "piece_chest_wood", "name": name,
        "look": {
            "mesh": {"file": "models/" + glb},
            "materials": [
                {"target": "Wood", "from": {"prefab": "wood_window", "material": "window_wood"}},
                {"target": "Metal", "from": {"prefab": "iron_grate", "material": "metalwall"}},
                {"target": "Cloth", "from": {"prefab": "wood_window", "material": "window_wood"}},
            ],
            "collision": {"mode": "box", "center": unity(center), "size": [round(size[0], 4), round(size[2], 4), round(size[1], 4)]},
        },
        "fields": {
            "Piece": {"m_allowRotatedOverlap": True, "m_notOnTiltingSurface": False},
            "WearNTear": {"m_burnable": True, "m_supports": True, "m_health": 400},
        },
        "snap": {"mode": "replace", "points": [unity(s) for s in snaps]},
    }


sc = bpy.context.scene
models = os.path.join(pack_dir, "models")
items = os.path.join(pack_dir, "items")
os.makedirs(models, exist_ok=True)


def export(path, objects):
    for o in sc.objects:
        o.select_set(o in objects)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_cameras=False,
                              export_lights=False, export_apply=True, export_animations=False)


def make(name, parts):
    obj = D.objects.new(name, build(name, parts))
    sc.collection.objects.link(obj)
    return obj


info = {"cell": [CELL_W, CELL_H], "sections": {}}
pieces = {}
d = DEPTH
for cols, rows in ((1, 1), (1, 3), (3, 1)):
    parts, cells = section_parts(cols, rows)
    obj = make("Section", parts)
    glb = f"post_section_{cols}x{rows}.glb"
    export(os.path.join(models, glb), [obj])
    w, h = cols * CELL_W, rows * CELL_H
    nodes = [(-w / 2 + i * CELL_W, j * CELL_H) for i in range(cols + 1) for j in range(rows + 1)
             if i in (0, cols) or j in (0, rows)]
    snaps = [(x, y, z) for x, z in nodes for y in (-d, d)]
    pid = f"Drake_PostSection{cols}x{rows}"
    # Collision is the back board only: the boxes in the cells need their own doors free to hover.
    rec = recipe(pid, f"Post Wall {cols}x{rows}", glb, (0, -d + 0.015, h / 2), (w, 0.03, h), snaps)
    pieces[pid] = rec
    info["sections"][pid] = {"cols": cols, "rows": rows, "cells": [unity((x, 0, z)) for x, z in cells],
                             "shelves": [round(j * CELL_H, 4) for j in range(rows + 1)], "width": round(w, 4), "depth": round(2 * d, 4)}
    pieces[pid]["_obj"] = obj

for rows in (1, 3):
    obj = make("Side", side_parts(rows))
    glb = f"post_side_{rows}.glb"
    export(os.path.join(models, glb), [obj])
    h = rows * CELL_H
    snaps = [(0, y, k * CELL_H) for k in range(rows + 1) for y in (-d, d)]
    pid = f"Drake_PostSide{rows}"
    pieces[pid] = recipe(pid, f"Post Wall Side ({rows} tall)", glb, (T / 2, 0, h / 2), (T, 2 * d + 0.02, h), snaps)
    pieces[pid]["_obj"] = obj

for cols in (1, 3):
    obj = make("Roof", roof_parts(cols))
    glb = f"post_roof_{cols}.glb"
    export(os.path.join(models, glb), [obj])
    w = cols * CELL_W
    snaps = [(-w / 2 + i * CELL_W, y, 0) for i in range(cols + 1) for y in (-d, d)]
    pid = f"Drake_PostRoof{cols}"
    pieces[pid] = recipe(pid, f"Post Wall Roof ({cols} wide)", glb, (0, 0, 0.16), (w, 2 * (d + OVER), 0.32), snaps)
    pieces[pid]["_obj"] = obj

obj = make("RoofEnd", roof_end_parts())
export(os.path.join(models, "post_roof_end.glb"), [obj])
pieces["Drake_PostRoofEnd"] = recipe("Drake_PostRoofEnd", "Post Wall Roof End", "post_roof_end.glb",
                                     ((T + END) / 2, 0, 0.16), (T + END, 2 * (d + OVER), 0.32), [(0, -d, 0), (0, d, 0)])
pieces["Drake_PostRoofEnd"]["_obj"] = obj

for pid, rec in pieces.items():
    out = {k: v for k, v in rec.items() if k != "_obj"}
    with open(os.path.join(items, pid + ".json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
print("POSTWALL " + json.dumps(info))


# ---------- preview: a run of mixed pieces with boxes in every cell ----------
def paint(mat, color, rough, metallic=0.0):
    mat.use_nodes = True
    b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metallic
    mat.diffuse_color = color


for o in sc.objects:
    o.hide_render = True


def place(pid, x, z, mirror=False):
    src = pieces[pid]["_obj"]
    o = D.objects.new(pid, src.data)
    sc.collection.objects.link(o)
    o.location = (x, 0, z)
    if mirror:
        o.rotation_euler = (0, 0, math.pi)
    return o


bpy.ops.import_scene.gltf(filepath=os.path.join(models, "pobox.glb"))
box_parts = [o for o in sc.objects if o.select_get()]
for o in box_parts:
    o.hide_render = True


def boxes(pid, x, z):
    for bx, bz, _ in info["sections"][pid]["cells"]:
        for o in box_parts:
            copy = D.objects.new(o.name, o.data)
            sc.collection.objects.link(copy)
            copy.location = o.location + Vector((x + bx, 0, z + bz))
            copy.rotation_euler = o.rotation_euler
            if o.name.startswith("Flag") and rng.random() < 0.4:
                copy.rotation_euler = (0, math.radians(90), 0)


# [side3][col 1x3][3 rows of 3x1][col 1x3][side3], roofs and ends on top
x = 0.0
cw = CELL_W
layout = [("Drake_PostSection1x3", 1)] + [("rows", 3)] + [("Drake_PostSection1x3", 1)]
total = sum(n for _, n in layout) * cw
x = -total / 2
place("Drake_PostSide3", x, 0, mirror=True)
for pid, n in layout:
    w = n * cw
    cx = x + w / 2
    if pid == "rows":
        for r in range(3):
            place("Drake_PostSection3x1", cx, r * CELL_H)
            boxes("Drake_PostSection3x1", cx, r * CELL_H)
        place("Drake_PostRoof3", cx, 3 * CELL_H)
    else:
        place(pid, cx, 0)
        boxes(pid, cx, 0)
        place("Drake_PostRoof1", cx, 3 * CELL_H)
    x += w
place("Drake_PostSide3", x, 0)
place("Drake_PostRoofEnd", -total / 2, 3 * CELL_H, mirror=True)
place("Drake_PostRoofEnd", x, 3 * CELL_H)

paint(wood, (0.22, 0.12, 0.06, 1), 0.8)
paint(metal, (0.10, 0.10, 0.11, 1), 0.5, 0.8)
paint(cloth, (0.25, 0.60, 0.18, 1), 0.9)
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.color_type = "MATERIAL"
sc.display.shading.light = "STUDIO"
sc.render.resolution_x, sc.render.resolution_y = 1000, 760
wd = D.worlds.new("w")
sc.world = wd
wd.use_nodes = True
wd.node_tree.nodes["Background"].inputs["Color"].default_value = (0.30, 0.38, 0.26, 1)
wd.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.4
ld = D.lights.new("sun", "SUN")
ld.energy = 3.2
sun = D.objects.new("sun", ld)
sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
sc.collection.objects.link(sun)
cam = D.objects.new("cam", D.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
for name, loc in (("front", (0.9, 3.4, 1.0)), ("side", (-2.6, 2.4, 1.4))):
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector((0, 0, 0.62)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = f"{out_png}_{name}.png"
    bpy.ops.render.render(write_still=True)
