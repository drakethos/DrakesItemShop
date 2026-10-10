"""Armor and weapon racks (free-standing or wall-mounted, big or small), fully procedural.
Args after --: out_blend out_glb out_png texdir type(armor|weapon|any) mount(stand|wall) size(big|small)
Armor racks and wall racks (type any: armor and weapons) hang on iron hooks (+ wooden pegs on the armor stand);
weapon racks hold each weapon in a two-peg fork
(guard, axe head or shield rim rests on the fork, the rest hangs down).
Blender axes: X = width, Y = depth (+Y = front, items side), Z = up. Forge maps Unity (x,y,z) = Blender (x,z,y)."""
import bpy, bmesh, math, sys, random, json
from mathutils import Vector

out_blend, out_glb, out_png, texdir, rtype, kind, size = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

WOOD_U, WOOD_V = 0.9, 0.19        # vanilla Planks5c density: grain along U
rng = random.Random(11)
wood = D.materials.new("Wood")
metal = D.materials.new("Metal")
slots = []                        # (kind, x, y, z) hang points, front side


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
    """parts: (lo, hi, material index (0 wood, 1 metal), bevel)."""
    me = D.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for lo, hi, mat, bev in parts:
        lo, hi = Vector(lo), Vector(hi)
        before = set(bm.faces)
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector(((lo.x + hi.x) / 2 + v.co.x * (hi.x - lo.x),
                           (lo.y + hi.y) / 2 + v.co.y * (hi.y - lo.y),
                           (lo.z + hi.z) / 2 + v.co.z * (hi.z - lo.z)))
        dims = hi - lo
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
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))
    return me


def hook(x, y, z, reach=0.13, rise=0.06):
    """Iron hook out of a front face at y: back plate, arm forward, upturned tip."""
    r = 0.011
    return [((x - 0.025, y, z - 0.04), (x + 0.025, y + 0.008, z + 0.04), 1, 0.003),      # plate
            ((x - r, y, z - r), (x + r, y + reach, z + r), 1, 0.004),                     # arm
            ((x - r, y + reach - 2 * r, z - r), (x + r, y + reach, z + rise), 1, 0.004)]  # tip


def peg(x, y, z, reach=0.11, d=1):
    """Turned wooden peg with an iron ferrule; d = -1 points it out of the back."""
    reach *= d
    return [((x - 0.018, min(y, y + reach), z - 0.018), (x + 0.018, max(y, y + reach), z + 0.018), 0, 0.008),
            ((x - 0.026, min(y + reach, y + reach - 0.02 * d), z - 0.026),
             (x + 0.026, max(y + reach, y + reach - 0.02 * d), z + 0.026), 1, 0.006)]


def fork(x, y, z, reach=0.12, gap=0.075, d=1):
    """Two pegs a guard / axe head / shield rim rests on; the weapon hangs between them."""
    return peg(x - gap / 2, y, z, reach, d) + peg(x + gap / 2, y, z, reach, d)


parts = []
if kind == "stand":
    W = 2.2 if size == "big" else 1.4
    H = (1.95 if size == "big" else 1.8) if rtype == "armor" else 1.45
    T = 0.09                      # upright thickness
    FD = 0.62                     # foot depth
    for x in (-W / 2 + T / 2, W / 2 - T / 2):
        parts += [((x - T / 2, -0.035, 0.0), (x + T / 2, 0.055, H - 0.05), 0, 0.012),           # upright
                  ((x - 0.06, -FD / 2, 0.0), (x + 0.06, FD / 2, 0.10), 0, 0.015),               # foot
                  ((x - 0.045, -FD / 2 + 0.04, 0.10), (x + 0.045, FD / 2 - 0.04, 0.13), 0, 0.01),
                  ((x - 0.065, -0.06, H - 0.05), (x + 0.065, 0.08, H - 0.02), 1, 0.006),          # iron cap
                  ((x - 0.07, -FD / 2 - 0.005, 0.02), (x + 0.07, -FD / 2 + 0.03, 0.08), 1, 0.004),  # foot shoes
                  ((x - 0.07, FD / 2 - 0.03, 0.02), (x + 0.07, FD / 2 + 0.005, 0.08), 1, 0.004)]
    over = 0.07                   # top bar overhangs the uprights
    tz = H - 0.16
    parts += [((-W / 2 - over, -0.045, tz), (W / 2 + over, 0.065, tz + 0.10), 0, 0.015)]       # top bar
    if rtype == "armor":
        parts += [((-W / 2 + T, -0.03, 0.22), (W / 2 - T, 0.05, 0.29), 0, 0.01)]                 # stretcher
        mz = 1.02
        parts += [((-W / 2 + T, -0.035, mz), (W / 2 - T, 0.055, mz + 0.08), 0, 0.012)]          # mid bar
        n = 3 if size == "big" else 2
        step = (W - 2 * T) / n
        for i in range(n):
            x = -W / 2 + T + step * (i + 0.5)
            parts += hook(x, 0.065, tz + 0.05)
            slots.append(("hook", x, 0.065 + 0.13, tz + 0.05))
            parts += peg(x, 0.055, mz + 0.04)
            slots.append(("peg", x, 0.055 + 0.11, mz + 0.04))
    else:
        parts += [((-W / 2 + T, -0.03, 0.22), (W / 2 - T, 0.05, 0.29), 0, 0.01)]                 # stretcher
        n = 6 if size == "big" else 4
        step = (W - 2 * T) / n
        for i in range(n):
            x = -W / 2 + T + step * (i + 0.5)
            parts += fork(x, 0.065, tz + 0.05)
            slots.append(("fork", x, 0.065 + 0.06, tz + 0.05 + 0.02))
            # second row out of the back of the bar (items face -Z there)
            parts += fork(x, -0.045, tz + 0.05, d=-1)
            slots.append(("fork_back", x, -0.045 - 0.06, tz + 0.05 + 0.02))
    BOUNDS = [-W / 2 - over, -FD / 2, 0.0, W / 2 + over, FD / 2, H]
else:
    n = 5 if size == "big" else 3
    pitch = 0.40 if rtype == "weapon" else 0.55       # chest pieces ~0.5 m wide; weapons ~0.2 but shields wider
    W = n * pitch + 0.2
    BH = 0.30                     # board height, origin = back centre of the board
    parts += [((-W / 2, 0.0, -BH / 2), (W / 2, 0.04, BH / 2), 0, 0.008),                      # backboard
              ((-W / 2 - 0.03, 0.0, BH / 2 - 0.01), (W / 2 + 0.03, 0.075, BH / 2 + 0.05), 0, 0.012),  # top shelf lip
              ((-W / 2 - 0.02, 0.0, -BH / 2 - 0.04), (W / 2 + 0.02, 0.06, -BH / 2 + 0.01), 0, 0.012)]  # bottom rail
    for x in (-W / 2 + 0.05, W / 2 - 0.05):                                                      # iron straps + rivets
        parts += [((x - 0.025, 0.04, -BH / 2 + 0.01), (x + 0.025, 0.048, BH / 2 - 0.01), 1, 0.003)]
        for z in (-BH / 2 + 0.05, BH / 2 - 0.05):
            parts += [((x - 0.008, 0.048, z - 0.008), (x + 0.008, 0.056, z + 0.008), 1, 0.004)]
    for i in range(n):
        x = -W / 2 + 0.1 + pitch * (i + 0.5)
        if rtype != "weapon":
            parts += hook(x, 0.04, 0.0, reach=0.14)
            slots.append(("hook", x, 0.18, 0.0))
        else:
            parts += fork(x, 0.04, 0.03)
            slots.append(("fork", x, 0.04 + 0.06, 0.03 + 0.02))
    BOUNDS = [-W / 2 - 0.03, 0.0, -BH / 2 - 0.04, W / 2 + 0.03, 0.2, BH / 2 + 0.05]

rack = D.objects.new("Rack", build("Rack", parts))
bpy.context.scene.collection.objects.link(rack)

bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)
for o in bpy.context.scene.objects:
    o.select_set(o.type == "MESH")
bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB", use_selection=True,
                          export_cameras=False, export_lights=False, export_apply=True)
print("RACKINFO " + json.dumps({"type": rtype, "mount": kind, "size": size, "bounds": BOUNDS, "slots": slots}))

# ---------- Preview only: stand-in items on the hang points (not exported) ----------
prop = D.materials.new("Prop")
prop.use_nodes = True
pb = next(n for n in prop.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
pb.inputs["Base Color"].default_value = (0.55, 0.32, 0.22, 1)
steel = D.materials.new("Steel")
steel.use_nodes = True
sb = next(n for n in steel.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
sb.inputs["Base Color"].default_value = (0.75, 0.75, 0.78, 1)
sb.inputs["Metallic"].default_value = 0.9
sb.inputs["Roughness"].default_value = 0.35


def add_box(lo, hi, mat):
    me = D.meshes.new("p")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * (hi[0] - lo[0]), v.co.y * (hi[1] - lo[1]), v.co.z * (hi[2] - lo[2])))
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = D.objects.new("prop", me)
    o.location = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
    bpy.context.scene.collection.objects.link(o)


def add_disk(c, r, t, mat):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=t, location=c, rotation=(math.pi / 2, 0, 0))
    bpy.context.active_object.data.materials.append(mat)


def tunic(x, y, z):          # chest armour hanging from a hook: shoulders + body
    add_box((x - 0.26, y - 0.06, z - 0.16), (x + 0.26, y + 0.06, z - 0.02), prop)
    add_box((x - 0.20, y - 0.05, z - 0.62), (x + 0.20, y + 0.05, z - 0.14), prop)


def cape(x, y, z):
    add_box((x - 0.22, y - 0.02, z - 0.85), (x + 0.22, y + 0.02, z - 0.02), prop)


def legs(x, y, z):           # trousers hanging by the waistband
    add_box((x - 0.18, y - 0.04, z - 0.14), (x + 0.18, y + 0.04, z - 0.03), prop)
    for side in (-1, 1):
        add_box((x + side * 0.10 - 0.07, y - 0.035, z - 0.75), (x + side * 0.10 + 0.07, y + 0.035, z - 0.14), prop)


def helmet(x, y, z):         # helmet hanging by its rim
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.14, location=(x, y, z - 0.10))
    o = bpy.context.active_object
    o.scale.z = 0.9
    o.data.materials.append(steel)


def sword(x, y, z):          # guard resting on the fork, blade down, grip up
    add_box((x - 0.10, y - 0.02, z - 0.03), (x + 0.10, y + 0.02, z), steel)
    add_box((x - 0.018, y - 0.018, z), (x + 0.018, y + 0.018, z + 0.16), prop)
    add_box((x - 0.03, y - 0.006, z - 0.80), (x + 0.03, y + 0.006, z - 0.03), steel)


def axe(x, y, z):            # head resting on the fork, haft down
    add_box((x - 0.02, y - 0.02, z - 0.75), (x + 0.02, y + 0.02, z + 0.08), prop)
    add_box((x + 0.02, y - 0.012, z - 0.06), (x + 0.17, y + 0.012, z + 0.10), steel)


def mace(x, y, z):
    add_box((x - 0.02, y - 0.02, z - 0.55), (x + 0.02, y + 0.02, z), prop)
    add_box((x - 0.07, y - 0.07, z), (x + 0.07, y + 0.07, z + 0.14), steel)


def shield(x, y, z):         # rim resting on the fork
    add_disk((x, y + 0.03, z - 0.24), 0.26, 0.04, prop)


show = {"hook": [tunic, cape, tunic, legs, tunic],
        "peg": [helmet, legs, helmet],
        "fork": [sword, axe, shield, sword, mace, axe],
        "fork_back": [axe, sword, mace, shield, sword, axe]}
seen = {}
for where, x, y, z in slots:
    k = seen.get(where, 0)
    seen[where] = k + 1
    show[where][k % len(show[where])](x, y + (0.04 if where == "fork_back" else -0.04), z)


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


textured(wood, texdir + "/wood_window_window_wood.tga")
metal.use_nodes = True
mb = next(n for n in metal.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
mb.inputs["Base Color"].default_value = (0.16, 0.16, 0.17, 1)
mb.inputs["Metallic"].default_value = 0.8
mb.inputs["Roughness"].default_value = 0.5

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 900, 700
w = D.worlds.new("w")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.42, 0.3, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.5
ld = D.lights.new("sun", "SUN")
ld.energy = 3.5
lo = D.objects.new("sun", ld)
lo.rotation_euler = (math.radians(50), 0, math.radians(-30))
sc.collection.objects.link(lo)
if kind == "wall":                # a plain wall behind it so it reads as mounted
    backdrop = D.materials.new("Backdrop")
    backdrop.use_nodes = True
    next(n for n in backdrop.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.5, 0.5, 0.47, 1)
    add_box((-3.0, -0.12, -2.0), (3.0, 0.0, 1.2), backdrop)
cd = D.cameras.new("cam")
cam = D.objects.new("cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam
cx, cz = (0.0, BOUNDS[5] / 2 + 0.1) if kind == "stand" else (0.0, -0.3)
far = max(2.2, (BOUNDS[3] - BOUNDS[0]) * 1.45, (BOUNDS[5] - BOUNDS[2]) * 1.8)
cam.location = Vector((far * 0.45, far, cz + 0.5))
cam.rotation_euler = (Vector((cx, 0.0, cz)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = out_png
bpy.ops.render.render(write_still=True)
