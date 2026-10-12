"""The jute mat piece itself (free layout): a woven 2 x 2 m mat, one mesh "Mat", one material "Fabric" (jute_carpet in game).
Args after --: out_blend out_glb
Blender axes: X = width, Y = depth, Z = up; origin = centre of the underside. Forge maps Unity (x,y,z) = Blender (x,z,y)."""
import bpy, bmesh, sys
from mathutils import Vector

out_blend, out_glb = sys.argv[sys.argv.index("--") + 1:]
D = bpy.data
for o in list(D.objects):
    D.objects.remove(o)

fabric = D.materials.new("Fabric")
fabric.use_nodes = True
next(n for n in fabric.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.62, 0.50, 0.30, 1)

me = D.meshes.new("Mat")
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
SIZE = 2.0


def box(lo, hi):
    before = set(bm.faces)
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]:
        v.co = Vector(((lo[0] + hi[0]) / 2 + v.co.x * (hi[0] - lo[0]),
                       (lo[1] + hi[1]) / 2 + v.co.y * (hi[1] - lo[1]),
                       (lo[2] + hi[2]) / 2 + v.co.z * (hi[2] - lo[2])))
    for f in bm.faces:
        if f in before:
            continue
        for l in f.loops:
            l[uv].uv = (l.vert.co.x / SIZE + 0.5, l.vert.co.y / SIZE + 0.5)   # planar, so the weave texture runs true


h = SIZE / 2
box((-h, -h, 0.0), (h, h, 0.022))                                             # the mat
for x0, x1, y0, y1 in ((-h, h, -h, -h + 0.07), (-h, h, h - 0.07, h), (-h, -h + 0.07, -h, h), (h - 0.07, h, -h, h)):
    box((x0, y0, 0.022), (x1, y1, 0.028))                                      # border band
for i in range(1, 10):
    y = -h + i * 0.2
    box((-h + 0.07, y - 0.003, 0.022), (h - 0.07, y + 0.003, 0.025))           # weave lines
for i in range(21):
    x = -h + 0.05 + i * 0.095
    box((x - 0.006, -h - 0.07, 0.0), (x + 0.006, -h, 0.012))                   # fringes, both ends
    box((x - 0.006, h, 0.0), (x + 0.006, h + 0.07, 0.012))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(me)
bm.free()
me.materials.append(fabric)
for p in me.polygons:
    p.use_smooth = False
obj = D.objects.new("Mat", me)
bpy.context.scene.collection.objects.link(obj)

bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True)
for o in bpy.context.scene.objects:
    o.select_set(o.type == "MESH")
bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB", use_selection=True, export_cameras=False,
                          export_lights=False, export_apply=True)
print("JUTEMATINFO ok")
