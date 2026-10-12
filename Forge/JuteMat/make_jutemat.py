"""Jute mat, free layout (mockup option A): a woven mat with goods laid anywhere on it. No tags on the mat: prices show when you point at an item.
Args after --: out_blend out_png_prefix
Stand-in goods are preview only; the mat itself is one mesh with Jute / Wood / Metal / Cloth materials.
Writes <prefix>_three_quarter.png and <prefix>_top.png."""
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix

out_blend, out_png = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)
sc = bpy.context.scene


def mat(name, color, rough=0.8, metallic=0.0):
    m = D.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metallic
    return m


JUTE = mat("Jute", (0.62, 0.50, 0.30, 1), 0.95)
JUTE_DARK = mat("JuteBorder", (0.34, 0.12, 0.08, 1), 0.95)
WOOD = mat("Wood", (0.22, 0.12, 0.06, 1))
WOOD_L = mat("WoodLight", (0.45, 0.28, 0.14, 1))
METAL = mat("Metal", (0.62, 0.64, 0.68, 1), 0.35, 0.9)
IRON = mat("Iron", (0.10, 0.10, 0.11, 1), 0.5, 0.8)
CLOTH = mat("Cloth", (0.55, 0.12, 0.10, 1), 0.9)
GOLD = mat("Gold", (0.85, 0.65, 0.2, 1), 0.4, 0.8)
PARCH = mat("Parchment", (0.93, 0.88, 0.74, 1), 0.9)


def box(name, lo, hi, m, rot=(0, 0, 0)):
    me = D.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * (hi[0] - lo[0]), v.co.y * (hi[1] - lo[1]), v.co.z * (hi[2] - lo[2])))
    bm.to_mesh(me)
    bm.free()
    me.materials.append(m)
    o = D.objects.new(name, me)
    o.location = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
    o.rotation_euler = [math.radians(a) for a in rot]
    sc.collection.objects.link(o)
    return o


def cyl(name, c, r, h, m, rot=(0, 0, 0), verts=32):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=verts, location=c, rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(m)
    return o


def turned(o, z_deg, about):
    """Rotate an object about the vertical axis through `about` (x, y)."""
    pivot = Vector((about[0], about[1], 0))
    rot = Matrix.Rotation(math.radians(z_deg), 4, "Z")
    o.matrix_world = Matrix.Translation(pivot) @ rot @ Matrix.Translation(-pivot) @ o.matrix_world


def tag(x, y, label, price, yaw=0):
    """A little price tag: a plaque on an iron stake, with the name and price written on it."""
    stake = box("stake", (x - 0.008, y - 0.008, 0.0), (x + 0.008, y + 0.008, 0.07), IRON)
    plaque = box("plaque", (x - 0.075, y - 0.012, 0.06), (x + 0.075, y + 0.012, 0.13), WOOD_L)
    bpy.ops.object.text_add(location=(x, y - 0.014, 0.075), rotation=(math.radians(90), 0, 0))
    t = bpy.context.active_object
    t.data.body = f"{label}\n{price} g"
    t.data.align_x = "CENTER"
    t.data.size = 0.028
    t.data.extrude = 0.001
    t.data.materials.append(PARCH)
    t.data.space_line = 0.9
    for o in (stake, plaque, t):
        if yaw:
            turned(o, yaw, (x, y))


# ---- the mat: 2 x 2 m, woven, with a dark border band and short fringes at both ends
MW = 1.0
box("Mat", (-MW, -MW, 0.0), (MW, MW, 0.022), JUTE)
for i in range(1, 10):                                              # weave lines
    z = -MW + i * 0.2
    box("weave", (-MW, z - 0.002, 0.022), (MW, z + 0.002, 0.024), JUTE_DARK)
for x0, x1, y0, y1 in ((-MW, MW, -MW, -MW + 0.07), (-MW, MW, MW - 0.07, MW), (-MW, -MW + 0.07, -MW, MW), (MW - 0.07, MW, -MW, MW)):
    box("border", (x0, y0, 0.022), (x1, y1, 0.027), JUTE_DARK)     # border band
for i in range(21):
    x = -MW + 0.05 + i * 0.095
    box("fringe", (x - 0.006, -MW - 0.07, 0.0), (x + 0.006, -MW, 0.012), JUTE)
    box("fringe", (x - 0.006, MW, 0.0), (x + 0.006, MW + 0.07, 0.012), JUTE)

# ---- goods laid out by hand: nothing is on a grid. Each item is built around its own origin and placed with a turn.
z0 = 0.027


def group(x, y, yaw):
    return (x, y, yaw)


def attach(e, *objs):
    """Move items built around the origin to (x, y), turned by yaw about the vertical axis."""
    x, y, yaw = e
    bpy.context.view_layer.update()
    placing = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
    for o in objs:
        o.matrix_world = placing @ o.matrix_world
    return objs


# iron sword (blade along +x)
e = group(-0.45, 0.45, 28)
attach(e, box("blade", (0.0, -0.04, z0), (0.62, 0.04, z0 + 0.012), METAL), box("guard", (-0.03, -0.14, z0), (0.0, 0.14, z0 + 0.025), IRON),
       box("grip", (-0.15, -0.022, z0), (-0.03, 0.022, z0 + 0.04), WOOD), box("pommel", (-0.19, -0.035, z0), (-0.15, 0.035, z0 + 0.05), IRON))

# battle axe (haft along +x, head at the end)
e = group(0.05, -0.35, -14)
attach(e, box("haft", (-0.45, -0.025, z0), (0.45, 0.025, z0 + 0.05), WOOD_L), box("head", (0.30, 0.0, z0), (0.44, 0.22, z0 + 0.03), METAL),
       box("back", (0.30, -0.07, z0), (0.44, 0.0, z0 + 0.04), IRON))

# round shield, face up
e = group(0.5, 0.5, 0)
attach(e, cyl("shield", (0, 0, z0 + 0.02), 0.28, 0.04, WOOD_L), cyl("boss", (0, 0, z0 + 0.06), 0.07, 0.04, METAL), cyl("rim", (0, 0, z0 + 0.015), 0.292, 0.03, IRON))

# stack of iron bars
e = group(-0.65, -0.55, 12)
attach(e, *[box("bar", (-0.09, -0.035, z0 + k * 0.03), (0.09, 0.035, z0 + 0.03 + k * 0.03), IRON) for k in range(3)])

# bowl of stew and a jar of honey
e = group(-0.2, -0.05, 0)
attach(e, cyl("bowl", (0, 0, z0 + 0.03), 0.09, 0.06, WOOD), cyl("stew", (0, 0, z0 + 0.06), 0.075, 0.01, CLOTH), cyl("jar", (0.16, -0.07, z0 + 0.05), 0.045, 0.1, GOLD))

# arrow bundle
e = group(0.62, -0.15, 8)
attach(e, *([box("arrow", (-0.22, -0.03 + k * 0.012, z0), (0.2, -0.03 + k * 0.012 + 0.008, z0 + 0.008), WOOD_L) for k in range(6)] +
            [box("tip", (0.2, -0.03 + k * 0.012, z0), (0.24, -0.03 + k * 0.012 + 0.008, z0 + 0.008), METAL) for k in range(6)] +
            [box("tie", (-0.02, -0.04, z0), (0.02, 0.05, z0 + 0.02), CLOTH)]))

# render
bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1100, 800
w = D.worlds.new("w")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.30, 0.38, 0.26, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.3
ld = D.lights.new("sun", "SUN")
ld.energy = 3.5
sun = D.objects.new("sun", ld)
sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
sc.collection.objects.link(sun)
ground = D.meshes.new("g")
gm = bmesh.new()
bmesh.ops.create_grid(gm, x_segments=1, y_segments=1, size=4)
gm.to_mesh(ground)
gm.free()
gmat = mat("Grass", (0.15, 0.24, 0.10, 1), 1.0)
ground.materials.append(gmat)
go = D.objects.new("ground", ground)
go.location.z = -0.002
sc.collection.objects.link(go)
cd = D.cameras.new("cam")
cam = D.objects.new("cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam
for name, loc in (("three_quarter", (1.6, -2.7, 1.5)), ("top", (0.0, -0.001, 3.6))):
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector((0, 0, 0.02)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = f"{out_png}_{name}.png"
    bpy.ops.render.render(write_still=True)
