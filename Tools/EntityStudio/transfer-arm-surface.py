"""Blender background script: transfer same-part source UVs and four-weight skin."""
import hashlib
import json
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

folder = Path(sys.argv[sys.argv.index('--') + 1])
raw = (folder / 'Input.json').read_bytes()
source = json.loads(raw)
points = [Vector((v['x'], v['y'], v['z'])) for v in source['vertices']]
for part in (1, 2):
    triangles = [source['triangles'][i:i+3] for i in range(0, len(source['triangles']), 3)
                 if source['parts'][source['triangles'][i]] == part]
    tree = BVHTree.FromPolygons(points, triangles, all_triangles=True)
    arm = json.loads((folder / f'Arm{part}.json').read_text())
    uv, weights, distances = [], [], []
    for vertex in arm['vertices']:
        p = Vector((vertex['x'], vertex['y'], vertex['z']))
        hit, _, face, distance = tree.find_nearest(p)
        if face is None:
            raise RuntimeError('No same-part transfer surface')
        ia, ib, ic = triangles[face]
        a, b, c = points[ia], points[ib], points[ic]
        v0, v1, v2 = b-a, c-a, hit-a
        d00, d01, d11 = v0.dot(v0), v0.dot(v1), v1.dot(v1)
        denominator = d00*d11-d01*d01
        if abs(denominator) < 1e-18:
            bary = [1.0, 0.0, 0.0]
        else:
            wb = (d11*v2.dot(v0)-d01*v2.dot(v1))/denominator
            wc = (d00*v2.dot(v1)-d01*v2.dot(v0))/denominator
            bary = [max(0.0, 1-wb-wc), max(0.0, wb), max(0.0, wc)]
            total = sum(bary)
            bary = [value/total for value in bary]
        ids = (ia, ib, ic)
        uv.append({axis: sum(source['uv'][i][axis]*amount for i, amount in zip(ids, bary)) for axis in ('x', 'y')})
        influence = {}
        for i, amount in zip(ids, bary):
            for slot in range(4):
                bone = source['weights'][i][f'm_BoneIndex{slot}']
                influence[bone] = influence.get(bone, 0.0) + amount*source['weights'][i][f'm_Weight{slot}']
        ordered = sorted(influence.items(), key=lambda pair: (-pair[1], pair[0]))[:4]
        total = sum(value for _, value in ordered)
        weight = {}
        for slot in range(4):
            bone, value = ordered[slot] if slot < len(ordered) else (0, 0)
            weight[f'm_BoneIndex{slot}'] = bone
            weight[f'm_Weight{slot}'] = value/total
        weights.append(weight)
        distances.append(distance)
    arm.update(uv=uv, weights=weights, sourceSha256=hashlib.sha256(raw).hexdigest(),
               transferMaxDistance=max(distances), transferMeanDistance=sum(distances)/len(distances))
    (folder / f'Arm{part}_Transferred.json').write_text(json.dumps(arm))
    print(f'ARM_TRANSFER part={part} maxDistance={max(distances):.6f} meanDistance={sum(distances)/len(distances):.6f}')
