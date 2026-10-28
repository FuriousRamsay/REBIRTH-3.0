"""Run with Blender --background --factory-startup --python this.py -- input output."""
import json
import sys
from pathlib import Path
import bpy
import argparse

args = sys.argv[sys.argv.index("--") + 1:]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('input', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--surface-folder', type=Path)
parser.add_argument('--triangle-budget', type=int, default=10000)
parser.add_argument('--close-wrist', action='store_true')
parser.add_argument('--open-wrist', action='store_true')
options = parser.parse_args(args)
if options.triangle_budget < 1000 or (options.close_wrist and options.open_wrist):
    raise RuntimeError('Invalid remesh budget or conflicting morphology modes')
source = json.loads(options.input.read_text(encoding="utf-8-sig"))
output = options.output
output.mkdir(parents=True, exist_ok=True)
close_wrist = options.close_wrist
open_wrist = options.open_wrist
for part in (1, 2):
    selected = [tuple(source["triangles"][i:i+3]) for i in range(0, len(source["triangles"]), 3)
                if source["parts"][source["triangles"][i]] == part]
    used = sorted({index for triangle in selected for index in triangle})
    remap = {old: new for new, old in enumerate(used)}
    vertices = [(source["vertices"][i]["x"], source["vertices"][i]["y"], source["vertices"][i]["z"]) for i in used]
    faces = [tuple(remap[i] for i in triangle) for triangle in selected]
    if options.surface_folder:
        surface = json.loads((options.surface_folder/f'Arm{part}_Transferred.json').read_text())
        vertices = [(v['x'],v['y'],v['z']) for v in surface['vertices']]
        faces = [tuple(surface['triangles'][i:i+3]) for i in range(0,len(surface['triangles']),3)]
    mesh = bpy.data.meshes.new(f"FarmerArm{part}")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("LocalVolumeRepair", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = .002
    modifier.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    if close_wrist or open_wrist:
        amount = -.006 if open_wrist else .006
        def wrist_weight(vertex):
            x = abs(vertex.co.x)
            return min(1.0, max(0.0, (x-.52)/.06))*min(1.0, max(0.0, (.85-x)/.05))
        offsets = [v.normal.copy() * (amount*wrist_weight(v)) for v in obj.data.vertices]
        for v, delta in zip(obj.data.vertices, offsets):
            v.co += delta
        obj.data.update()
        closing = obj.modifiers.new('WristVolumeUnion', 'REMESH')
        closing.mode = 'VOXEL'
        closing.voxel_size = .002
        closing.use_smooth_shade = True
        bpy.ops.object.modifier_apply(modifier=closing.name)
        offsets = [v.normal.copy() * (amount*wrist_weight(v)) for v in obj.data.vertices]
        for v, delta in zip(obj.data.vertices, offsets):
            v.co -= delta
        obj.data.update()
    obj.data.calc_loop_triangles()
    dense_triangles = len(obj.data.loop_triangles)
    simplify = obj.modifiers.new("VolumeSurfaceBudget", "DECIMATE")
    simplify.ratio = min(1.0, options.triangle_budget / dense_triangles)
    simplify.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=simplify.name)
    obj.data.calc_loop_triangles()
    result = {
        "part": part, "voxelSize": .002,
        "vertices": [{"x": v.co.x, "y": v.co.y, "z": v.co.z} for v in obj.data.vertices],
        "triangles": [i for triangle in obj.data.loop_triangles for i in triangle.vertices],
        "sourceTriangles": len(faces),
        "triangleBudget": options.triangle_budget,
        "inputSurface": str(options.surface_folder) if options.surface_folder else str(options.input),
        "denseTriangles": dense_triangles,
        "wristClosing": close_wrist,
        "wristOpening": open_wrist,
        "scope": "Diagnostic volume reconstruction; UV/skin transfer and silhouette validation still required.",
    }
    (output / f"Arm{part}.json").write_text(json.dumps(result), encoding="utf-8")
    print(f"ARM_REMESH part={part} sourceTriangles={len(faces)} resultTriangles={len(result['triangles'])//3} vertices={len(result['vertices'])}")
    bpy.data.objects.remove(obj, do_unlink=True)
