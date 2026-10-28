"""Blender diagnostic: localized binary-volume opening, not normal offsets."""
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index('--')+1:]
source_folder, output = Path(args[0]), Path(args[1])
output.mkdir(parents=True, exist_ok=True)
(output/'Input.json').write_bytes((source_folder/'Input.json').read_bytes())
step = .002
directions = [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]
corners = [((1,0,0),(1,1,0),(1,1,1),(1,0,1)),((0,0,0),(0,0,1),(0,1,1),(0,1,0)),
           ((0,1,0),(0,1,1),(1,1,1),(1,1,0)),((0,0,0),(1,0,0),(1,0,1),(0,0,1)),
           ((0,0,1),(1,0,1),(1,1,1),(0,1,1)),((0,0,0),(0,1,0),(1,1,0),(1,0,0))]

def neighborhood(volume, erosion):
    result = volume.copy()
    for axis in range(3):
        for sign in (-1, 1):
            shifted = np.roll(volume, sign, axis=axis)
            if erosion:
                result &= shifted
            else:
                result |= shifted
    return result

for part in (1, 2):
    arm = json.loads((source_folder/f'Arm{part}.json').read_text())
    points = [Vector((v['x'],v['y'],v['z'])) for v in arm['vertices']]
    faces = [arm['triangles'][i:i+3] for i in range(0,len(arm['triangles']),3)]
    tree = BVHTree.FromPolygons(points, faces, all_triangles=True)
    array = np.array(points)
    low = np.floor(array.min(axis=0)/step)*step-step*5
    shape = np.ceil((array.max(axis=0)-low)/step).astype(int)+6
    volume = np.zeros(tuple(shape), dtype=bool)
    odd = 0
    for x in range(shape[0]):
        for y in range(shape[1]):
            cursor = Vector((low[0]+(x+.5)*step,low[1]+(y+.5)*step,low[2]))
            hits = []
            for attempt in range(64):
                hit, _, _, _ = tree.ray_cast(cursor, Vector((0,0,1)), float(shape[2]*step))
                if hit is None:
                    break
                hits.append(hit.z)
                cursor.z = hit.z+1e-6
            if len(hits)%2:
                odd += 1
            for i in range(0,len(hits)-1,2):
                start = max(0,int(np.ceil((hits[i]-low[2])/step-.5)))
                end = min(shape[2],int(np.ceil((hits[i+1]-low[2])/step-.5)))
                volume[x,y,start:end] = True
    if odd:
        raise RuntimeError(f'Part {part}: {odd} ambiguous odd-intersection voxel rays; repair refused')
    opened = volume.copy()
    for radius in (1,2,3):
        trial = volume.copy()
        for _ in range(radius):
            trial = neighborhood(trial, True)
        for _ in range(radius):
            trial = neighborhood(trial, False)
        for x in range(shape[0]):
            coordinate = abs(low[0]+(x+.5)*step)
            weight = min(1,max(0,(coordinate-.52)/.06))*min(1,max(0,(.85-coordinate)/.05))
            if round(weight*3) == radius:
                opened[x,:,:] = trial[x,:,:] & volume[x,:,:]
    vertices, polygons, lookup = [], [], {}
    for direction, face_corners in zip(directions,corners):
        axis = next(i for i,v in enumerate(direction) if v)
        boundary = opened & ~np.roll(opened,-direction[axis],axis=axis)
        for cell in np.argwhere(boundary):
            polygon = []
            for corner in face_corners:
                key = tuple(int(cell[i])+corner[i] for i in range(3))
                if key not in lookup:
                    lookup[key] = len(vertices)
                    vertices.append(tuple(float(low[i]+key[i]*step) for i in range(3)))
                polygon.append(lookup[key])
            polygons.append(polygon)
    mesh = bpy.data.meshes.new(f'OpenedArm{part}')
    mesh.from_pydata(vertices, [], polygons)
    mesh.update()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    smooth = obj.modifiers.new('VoxelBoundarySmoothing','SMOOTH')
    smooth.factor = .5
    smooth.iterations = 2
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    mesh.calc_loop_triangles()
    simplify = obj.modifiers.new('ReviewBudget','DECIMATE')
    simplify.ratio = min(1,10000/len(mesh.loop_triangles))
    simplify.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=simplify.name)
    obj.data.calc_loop_triangles()
    result = {'part':part,'voxelSize':step,'vertices':[dict(zip(('x','y','z'),v.co)) for v in obj.data.vertices],
              'triangles':[i for tri in obj.data.loop_triangles for i in tri.vertices],
              'occupiedBefore':int(volume.sum()),'occupiedAfter':int(opened.sum()),'oddRays':odd}
    (output/f'Arm{part}.json').write_text(json.dumps(result))
    print(f'BINARY_OPEN part={part} occupied={volume.sum()}->{opened.sum()} triangles={len(result["triangles"])//3}')
    bpy.data.objects.remove(obj,do_unlink=True)
