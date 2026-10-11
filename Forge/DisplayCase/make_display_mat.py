"""Display mats: a jute runner in a low wooden tray with iron corner brackets, to lay on a table and show wares (Blender 5.1).
Fully procedural; also writes the Forge recipes (items/Drake_DisplayMat*.json) and a preview.
Args after --: pack_dir (Forge/DisplayCase) preview_png
Blender axes: X = width (across), Y = length (Unity Z), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y).
Prints MATINFO: the cloth area items lie on (Unity space) for DisplayMatPiece."""
import bpy, bmesh, math, sys, random, json, os
from mathutils import Vector

pack_dir, out_png = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19
rng = random.Random(5)
wood = D.materials.new("Wood")
metal = D.materials.new("Metal")
fabric = D.materials.new("Fabric")
WD, MT, FB = 0, 1, 2

WIDTH = 0.62           # across the mat
BASE = 0.02            # tray floor
RIM_W, RIM_H = 0.035, 0.045
CLOTH = 0.006          # jute thickness on the floor
SIZES = {"Drake_DisplayMat": (1.80, 6, "display_mat.glb", "Display Mat"),
         "Drake_DisplayMatSmall": (0.92, 3, "display_mat_small.glb", "Small Display Mat")}


def box(bm, uv, lo, hi, mat, bev):
    lo, hi = Vector(lo), Vector(hi)
    before = set(bm.faces)
    r = bmesh.ops.create_cube(bm, size=1.0)
    dims = hi - lo
    centre = (lo + hi) / 2
    for v in r["verts"]:
        v.co = centre + Vector((v.co.x * dims.x, v.co.y * dims.y, v.co.z * dims.z))
    if bev:
        edges = list({e for v in r["verts"] for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=min(bev, min(dims) * 0.3), segments=2, profile=0.5, affect="EDGES", clamp_overlap=True)
    new = [f for f in bm.faces if f not in before]
    bm.normal_update()
    long_axis = max(range(3), key=lambda i: dims[i])
    ou, ov = rng.random(), rng.random()
    for f in new:
        f.material_index = mat
        n = f.normal
        normal_axis = max(range(3), key=lambda i: abs(n[i]))
        plane = [i for i in range(3) if i != normal_axis]
        ua = long_axis if long_axis in plane else plane[0]
        va = plane[1] if ua == plane[0] else plane[0]
        for l in f.loops:
            if mat == FB:   # the whole rug once across its top, like the display case cloth
                l[uv].uv = ((l.vert.co.x - lo.x) / dims.x, (l.vert.co.y - lo.y) / dims.y)
            elif mat == WD:
                l[uv].uv = (l.vert.co[ua] / WOOD_U + ou, l.vert.co[va] / WOOD_V + ov)
            else:
                l[uv].uv = (l.vert.co.x + l.vert.co.y, l.vert.co.z)


def mesh(name, parts):
    me = D.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for lo, hi, mat, bev in parts:
        box(bm, uv, lo, hi, mat, bev)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for m in (wood, metal, fabric):
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))
    return me


def tray(length):
    w, l = WIDTH / 2, length / 2
    wood_parts = [((-w, -l, 0), (w, l, BASE), WD, 0.004)]                                        # floor
    for sx in (-1, 1):                                                                         # long rims
        x0 = sx * w
        wood_parts.append(((min(x0, x0 - sx * RIM_W), -l, 0), (max(x0, x0 - sx * RIM_W), l, RIM_H), WD, 0.008))
    for sy in (-1, 1):                                                                         # short rims
        y0 = sy * l
        wood_parts.append(((-w + RIM_W, min(y0, y0 - sy * RIM_W), 0), (w - RIM_W, max(y0, y0 - sy * RIM_W), RIM_H * 0.9), WD, 0.008))
    metal_parts = []
    arm = 0.09
    for sx in (-1, 1):                                                                         # L brackets on the corners
        for sy in (-1, 1):
            x0, y0 = sx * w, sy * l
            metal_parts.append(((min(x0, x0 - sx * arm), min(y0, y0 - sy * RIM_W - 0.004), RIM_H - 0.001),
                                (max(x0, x0 - sx * arm), max(y0, y0 - sy * RIM_W - 0.004), RIM_H + 0.004), MT, 0.002))
            metal_parts.append(((min(x0, x0 - sx * RIM_W - 0.004), min(y0, y0 - sy * arm), RIM_H - 0.001),
                                (max(x0, x0 - sx * RIM_W - 0.004), max(y0, y0 - sy * arm), RIM_H + 0.004), MT, 0.002))
            metal_parts.append(((x0 - 0.012, y0 - 0.012, 0.004), (x0 + 0.012, y0 + 0.012, RIM_H - 0.004), MT, 0.003))   # corner strap
    for sx in (-1, 1):                                                                         # rivets along the long rims
        n = max(2, int(length / 0.3))
        for i in range(1, n):
            y = -l + i * length / n
            x = sx * (w - RIM_W / 2)
            metal_parts.append(((x - 0.007, y - 0.007, RIM_H), (x + 0.007, y + 0.007, RIM_H + 0.004), MT, 0.003))
    cloth_parts = [((-w + RIM_W, -l + RIM_W, BASE), (w - RIM_W, l - RIM_W, BASE + CLOTH), FB, 0.002)]
    return wood_parts + metal_parts, cloth_parts


def unity(p):
    return [round(p[0], 4), round(p[2], 4), round(p[1], 4)]


sc = bpy.context.scene
models = os.path.join(pack_dir, "models")
items = os.path.join(pack_dir, "items")


def export(path, objects):
    for o in sc.objects:
        o.select_set(o in objects)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_cameras=False,
                              export_lights=False, export_apply=True, export_animations=False)


info = {}
shown = []
for pid, (length, slots, glb, title) in SIZES.items():
    tray_parts, cloth_parts = tray(length)
    t = D.objects.new("Tray", mesh("Tray", tray_parts))
    c = D.objects.new("Cloth", mesh("Cloth", cloth_parts))
    for o in (t, c):
        sc.collection.objects.link(o)
    export(os.path.join(models, glb), [t, c])
    w, l = WIDTH / 2, length / 2
    recipe = {
        "id": pid, "kind": "piece", "base": "piece_chest_wood", "name": title,
        "look": {
            "mesh": {"file": "models/" + glb},
            "materials": [
                {"target": "Wood", "from": {"prefab": "wood_window", "material": "window_wood"}},
                {"target": "Fabric", "from": {"prefab": "jute_carpet", "material": "jute_carpet"}},
                {"target": "Metal", "from": {"prefab": "iron_grate", "material": "metalwall"}},
            ],
            "collision": {"mode": "box", "center": [0.0, round(RIM_H / 2, 4), 0.0], "size": [WIDTH, RIM_H, round(length, 4)]},
        },
        "fields": {
            "Container": {"m_width": slots, "m_height": 1},
            "Piece": {"m_allowRotatedOverlap": True, "m_notOnTiltingSurface": False},
            "WearNTear": {"m_burnable": True, "m_supports": False, "m_health": 200},
        },
        "snap": {"mode": "replace", "points": [unity((sx * w, sy * l, 0)) for sx in (-1, 1) for sy in (-1, 1)]},
    }
    with open(os.path.join(items, pid + ".json"), "w", encoding="utf-8") as f:
        json.dump(recipe, f, indent=2)
    inner = l - RIM_W - 0.02
    info[pid] = {"slots": slots, "floor_y": round(BASE + CLOTH, 4), "floor_z": [round(-inner, 4), round(inner, 4)],
                 "across": round(WIDTH - 2 * RIM_W - 0.04, 4)}
    shown.append((t, c, length))
print("MATINFO " + json.dumps(info))


# ---------- preview ----------
def paint(mat, color, rough, metallic=0.0):
    mat.use_nodes = True
    b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metallic
    mat.diffuse_color = color


paint(wood, (0.22, 0.12, 0.06, 1), 0.8)
paint(metal, (0.10, 0.10, 0.11, 1), 0.5, 0.8)
paint(fabric, (0.55, 0.42, 0.25, 1), 1.0)
(t1, c1, _), (t2, c2, _) = shown
for o in (t2, c2):
    o.location = (0.9, 0, 0)
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.color_type = "MATERIAL"
sc.display.shading.light = "STUDIO"
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
cam = D.objects.new("cam", D.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.location = Vector((2.0, -1.8, 1.4))
cam.rotation_euler = (Vector((0.45, 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = out_png
bpy.ops.render.render(write_still=True)
