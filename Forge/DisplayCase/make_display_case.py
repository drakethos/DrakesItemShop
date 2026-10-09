"""Display case model, big or small, from the user's 'display case.blend' (Base mesh) + procedural glass/frame.
Args after --: out_blend out_glb out_png texdir size(big|small)"""
import bpy, bmesh, math, sys, random, json
from mathutils import Vector, Matrix

out_blend, out_glb, out_png, texdir, size = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
base, glass, panel = D.objects["Base"], D.objects["Glass"], D.objects["Glass Panel"]
wood, fabric, glassmat = D.materials["Wood"], D.materials["Fabric"], D.materials["Glass"]

WOOD_U, WOOD_V = 0.9, 0.19        # vanilla Planks5c density: grain along U
rng = random.Random(7)

C = -0.2015                       # case centre along its length (Blender Y)
FULL_Y0, FULL_Y1 = -2.304, 1.901  # glass ends of the big case
if size == "big":
    Y0, Y1 = FULL_Y0, FULL_Y1
    OPEN0, OPEN1 = -0.827, 0.315
else:
    half = 1.05
    Y0, Y1 = C - half, C + half
    OPEN0, OPEN1 = -0.32, 0.43
SHIFT = ((FULL_Y1 - FULL_Y0) - (Y1 - Y0)) / 2   # how far each end moves in
OVERLAP = 0.08                    # door pane overlaps the fixed glass on both sides
SLIDE = (OPEN1 - OPEN0) + 0.04    # far enough that the opening is clear

X0, X1 = -0.352, 1.071
BASE_TOP, GZ0, GZ1 = 1.033, 1.094, 1.714
P = 0.05
TOP = GZ1 + 0.006
G = 0.01                          # glass thickness


def wood_uv(uv, faces, long_axis=None):
    ou, ov = rng.random(), rng.random()
    for f in faces:
        n = f.normal
        normal_axis = max(range(3), key=lambda i: abs(n[i]))
        plane = [i for i in range(3) if i != normal_axis]
        if long_axis is not None and long_axis in plane:
            ua = long_axis
        else:
            ext = [max(v.co[i] for v in f.verts) - min(v.co[i] for v in f.verts) for i in plane]
            ua = plane[0] if ext[0] >= ext[1] else plane[1]
        va = plane[1] if ua == plane[0] else plane[0]
        for l in f.loops:
            l[uv].uv = (l.vert.co[ua] / WOOD_U + ou, l.vert.co[va] / WOOD_V + ov)


def smooth(me):
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))


def build(name, mats, parts):
    """parts: (lo, hi, material index, bevel). Wood members get their own grain direction and offset."""
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
        if mats[mat] is wood:
            wood_uv(uv, new, max(range(3), key=lambda i: dims[i]))
        else:
            for f in new:
                n = f.normal
                ax = max(range(3), key=lambda i: abs(n[i]))
                a, b = [(1, 2), (0, 2), (0, 1)][ax]
                for l in f.loops:
                    l[uv].uv = (l.vert.co[a], l.vert.co[b])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    smooth(me)
    return me


# ---------- Base: shortened for the small case (only its end vertices move), rounded edges, wood floor ----------
bm = bmesh.new()
bm.from_mesh(base.data)
bmesh.ops.transform(bm, matrix=base.matrix_world, verts=bm.verts)
for v in bm.verts:
    v.co.y += -SHIFT if v.co.y > C else SHIFT
bm.normal_update()
for f in bm.faces:
    f.material_index = 0
sharp = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(30)]
bmesh.ops.bevel(bm, geom=sharp, offset=0.012, segments=2, profile=0.5, affect="EDGES", clamp_overlap=True)
bm.normal_update()
uvl = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
wood_uv(uvl, bm.faces)
BY0 = min(v.co.y for v in bm.verts)
BY1 = max(v.co.y for v in bm.verts)
me = D.meshes.new("Base")
bm.to_mesh(me)
bm.free()
me.materials.append(wood)
smooth(me)
base.data = me
base.matrix_world = Matrix.Identity(4)

# ---------- Fixed glass: thin crystal walls with the door opening in the back wall ----------
glass.data = build("Glass", [glassmat], [
    ((X0, Y0, GZ0), (X0 + G, OPEN0, GZ1), 0, 0),      # back wall, left of the door
    ((X0, OPEN1, GZ0), (X0 + G, Y1, GZ1), 0, 0),      # back wall, right of the door
    ((X1 - G, Y0, GZ0), (X1, Y1, GZ1), 0, 0),         # front wall
    ((X0, Y0, GZ0), (X1, Y0 + G, GZ1), 0, 0),         # ends
    ((X0, Y1 - G, GZ0), (X1, Y1, GZ1), 0, 0),
    ((X0, Y0, GZ1 - 0.006), (X1, Y1, GZ1), 0, 0),     # lid
])
glass.matrix_world = Matrix.Identity(4)

# ---------- Door: crystal pane in a full wooden frame, on its own track just inside the back glass ----------
PY0, PY1, PZ0, PZ1 = OPEN0 - OVERLAP, OPEN1 + OVERLAP, 1.103, 1.703
PX0, PX1 = -0.338, -0.318
S, R = 0.045, 0.04                # stile width, rail height
panel.data = build("GlassPanel", [glassmat, wood], [
    ((-0.334, PY0 + S, PZ0 + R), (-0.326, PY1 - S, PZ1 - R), 0, 0.002),
    ((PX0, PY0, PZ0), (PX1, PY0 + S, PZ1), 1, 0.006),
    ((PX0, PY1 - S, PZ0), (PX1, PY1, PZ1), 1, 0.006),
    ((PX0, PY0 + S, PZ0), (PX1, PY1 - S, PZ0 + R), 1, 0.005),
    ((PX0, PY0 + S, PZ1 - R), (PX1, PY1 - S, PZ1), 1, 0.005),
])
panel.matrix_world = Matrix.Identity(4)

# ---------- Frame: posts, top rim, sill/track, legs, and stiles framing the door opening in the fixed glass ----------
parts = []
for x in (X0, X1):
    for y in (Y0, Y1):
        parts.append(((x - P / 2, y - P / 2, BASE_TOP), (x + P / 2, y + P / 2, TOP), 0, 0.01))
t0 = GZ1 - 0.02
parts += [((X0 - P / 2, Y0 + P / 2, t0), (X0 + P / 2, Y1 - P / 2, TOP), 0, 0.008),
          ((X1 - P / 2, Y0 + P / 2, t0), (X1 + P / 2, Y1 - P / 2, TOP), 0, 0.008),
          ((X0 + P / 2, Y0 - P / 2, t0), (X1 - P / 2, Y0 + P / 2, TOP), 0, 0.008),
          ((X0 + P / 2, Y1 - P / 2, t0), (X1 - P / 2, Y1 + P / 2, TOP), 0, 0.008)]
parts += [((X0 - P / 2, Y0 + P / 2, BASE_TOP), (-0.300, Y1 - P / 2, PZ0), 0, 0.008),
          ((X1 - P / 2, Y0 + P / 2, BASE_TOP), (X1 + P / 2, Y1 - P / 2, GZ0 + 0.005), 0, 0.008),
          ((X0 + P / 2, Y0 - P / 2, BASE_TOP), (X1 - P / 2, Y0 + P / 2, GZ0 + 0.005), 0, 0.008),
          ((X0 + P / 2, Y1 - P / 2, BASE_TOP), (X1 - P / 2, Y1 + P / 2, GZ0 + 0.005), 0, 0.008)]
# the fixed glass's cut edges at the door get wood too, outside the door's track
for y in (OPEN0, OPEN1):
    parts.append(((-0.372, y - 0.02, GZ0), (-0.341, y + 0.02, t0), 0, 0.006))
for x in (-0.369 + 0.09, 1.069 - 0.09):
    for y in (BY0 + 0.12, BY1 - 0.12):
        parts.append(((x - 0.055, y - 0.055, 0.0), (x + 0.055, y + 0.055, 0.28), 0, 0.012))
frame = D.objects.new("Frame", build("Frame", [wood], parts))
bpy.context.scene.collection.objects.link(frame)

# ---------- Jute cloth: the whole rug texture once, frayed edges over the wood floor ----------
me = D.meshes.new("Cloth")
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
cx0, cx1, cy0, cy1, cz = -0.29, 1.02, Y0 + 0.08, Y1 - 0.08, BASE_TOP + 0.003
f = bm.faces.new([bm.verts.new(c) for c in ((cx0, cy0, cz), (cx1, cy0, cz), (cx1, cy1, cz), (cx0, cy1, cz))])
bm.normal_update()
if f.normal.z < 0:
    f.normal_flip()
for l in f.loops:
    l[uv].uv = ((l.vert.co.y - cy0) / (cy1 - cy0), (l.vert.co.x - cx0) / (cx1 - cx0))
bm.to_mesh(me)
bm.free()
me.materials.append(fabric)
cloth = D.objects.new("Cloth", me)
bpy.context.scene.collection.objects.link(cloth)

for m in [m for m in D.materials if m.name.startswith("Material.")]:
    D.materials.remove(m)
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
print("CASEINFO " + json.dumps({"min": list(mn), "max": list(mx), "base": [-0.369, BY0, 1.069, BY1], "slide": SLIDE}))

# ---------- Preview with the real vanilla textures ----------
def textured(mat, tex, alpha_clip=False):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = D.images.load(tex)
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha_clip:
        nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])


textured(wood, texdir + "/wood_window_window_wood.tga")
textured(fabric, texdir + "/jute_carpet_jute_carpet.tga", True)
glassmat.use_nodes = True
g = next(n for n in glassmat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
g.inputs["Base Color"].default_value = (0.7, 0.9, 1.0, 1)
g.inputs["Alpha"].default_value = 0.15
g.inputs["Roughness"].default_value = 0.05
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1000, 560
w = D.worlds.new("w")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.42, 0.3, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.5
D.lights["Light"].energy = 1500
cam = D.objects["Camera"]
sc.camera = cam
far = 4.6 if size == "big" else 3.2


def shoot(path):
    cam.location = Vector((-far, C - far * 0.55, far * 0.62))
    cam.rotation_euler = (Vector((0.35, C, 0.9)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


shoot(out_png.replace(".png", "_closed.png"))
panel.location.y = -SLIDE
shoot(out_png.replace(".png", "_open.png"))
