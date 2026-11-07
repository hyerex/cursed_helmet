# Turns the head model into the mod's own files (embedded in the DLL): Assets\head.bin (the mesh)
# and Assets\head_color.jpg, head_normal.jpg, head_ao.jpg (its textures, made smaller).
# Run with Blender (no window):
#   blender -b --factory-startup -P prepare_head.py -- <head.glb or head.fbx> <Assets folder> [texture size]
#
# Use a .glb with the textures inside (what scan apps such as Polycam or KIRI Engine export). An
# .fbx often loses them; from an .fbx without textures only the mesh is written.
#
# Mesh: in the model's own units, centred on its bounds (the mod scales and places it). Blender is
# Z-up, Unity Y-up: (x, y, z) becomes (x, z, y), which mirrors, so the triangles are turned around.
# Corners with the same point, normal and UV are shared. UVs as in Blender (v up, like Unity).
#
# head.bin (little endian): int vertexCount, then per vertex 8 floats (position, normal, uv);
# int indexCount, then the indices (int).
import bpy
import os
import struct
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out_dir = args[0], args[1]
size = int(args[2]) if len(args) > 2 else 2048
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith((".glb", ".gltf")):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']

verts, index = [], []
seen = {}
for o in meshes:
    m = o.data
    m.calc_loop_triangles()
    world = o.matrix_world
    rot = world.to_3x3().inverted().transposed()
    uvs = m.uv_layers.active.data if m.uv_layers.active else None
    corner = m.corner_normals
    for t in m.loop_triangles:
        ids = []
        for loop in t.loops:
            p = world @ m.vertices[m.loops[loop].vertex_index].co
            n = (rot @ corner[loop].vector).normalized()
            uv = uvs[loop].uv if uvs else (0.0, 0.0)
            key = (o.name, m.loops[loop].vertex_index, round(n.x, 3), round(n.y, 3), round(n.z, 3), round(uv[0], 5), round(uv[1], 5))
            i = seen.get(key)
            if i is None:
                i = len(verts)
                seen[key] = i
                # Blender (x, y, z) -> Unity (x, z, y)
                verts.append(((p.x, p.z, p.y), (n.x, n.z, n.y), (uv[0], uv[1])))
            ids.append(i)
        # Turned around: the axis swap mirrors
        index += [ids[0], ids[2], ids[1]]

mn = Vector((min(v[0][0] for v in verts), min(v[0][1] for v in verts), min(v[0][2] for v in verts)))
mx = Vector((max(v[0][0] for v in verts), max(v[0][1] for v in verts), max(v[0][2] for v in verts)))
centre = (mn + mx) / 2
print(f"head: {len(verts)} vertices, {len(index) // 3} triangles, size (Unity x, y, z) {tuple(round(v, 4) for v in mx - mn)}")

with open(os.path.join(out_dir, "head.bin"), "wb") as f:
    f.write(struct.pack("<i", len(verts)))
    for p, n, uv in verts:
        f.write(struct.pack("<8f", p[0] - centre.x, p[1] - centre.y, p[2] - centre.z, n[0], n[1], n[2], uv[0], uv[1]))
    f.write(struct.pack("<i", len(index)))
    f.write(struct.pack(f"<{len(index)}i", *index))

# The textures, from the first material's nodes: colour, normal map, ambient occlusion
mat = next((s.material for o in meshes for s in o.material_slots if s.material), None)
found = {}
if mat and mat.node_tree:
    for node in mat.node_tree.nodes:
        if node.type != 'TEX_IMAGE' or node.image is None or node.image.size[0] == 0:
            continue
        links = [l.to_node for l in node.outputs['Color'].links]
        kind = None
        for to in links:
            if to.type == 'NORMAL_MAP':
                kind = "normal"
            elif to.type == 'BSDF_PRINCIPLED':
                kind = "color"
            elif to.type in ('SEPARATE_COLOR', 'SEPRGB') or 'Occlusion' in to.name or 'glTF' in to.name:
                kind = kind or "ao"
        found.setdefault(kind or node.image.name, node.image)
for kind in ("color", "normal", "ao"):
    img = found.get(kind)
    if img is None:
        print(f"no {kind} texture")
        continue
    copy = img.copy()
    copy.scale(size, size)
    path = os.path.join(out_dir, f"head_{kind}.jpg")
    copy.filepath_raw = path
    copy.file_format = 'JPEG'
    bpy.context.scene.render.image_settings.quality = 90
    copy.save()
    print(f"{kind}: {img.name} {tuple(img.size)} -> {path}")
print("written to", out_dir)
