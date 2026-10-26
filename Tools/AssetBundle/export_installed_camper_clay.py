"""Export the exact installed camper mesh hierarchy to a Blender-readable OBJ."""
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

SOURCE = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
OUTPUT = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CurrentInstalledCamperClay_20261023')
OUTPUT.mkdir(parents=True, exist_ok=True)

env = UnityPy.load(str(SOURCE))
objects = {o.path_id: o for o in env.objects}
gos = {i: o.read() for i, o in objects.items() if o.type.name == 'GameObject'}
transforms = {i: o.read() for i, o in objects.items() if o.type.name == 'Transform'}
renderers = {}
go_transforms = {}
for go_id, go in gos.items():
    for comp in go.m_Component:
        ptr = comp.component
        if ptr.path_id in transforms:
            go_transforms[go_id] = ptr.path_id
        elif ptr.path_id in objects and objects[ptr.path_id].type.name == 'MeshRenderer':
            renderers[go_id] = objects[ptr.path_id].read()

def mat(tr):
    p, s, q = tr.m_LocalPosition, tr.m_LocalScale, tr.m_LocalRotation
    x, y, z, w = q.x, q.y, q.z, q.w
    rot = np.array([
        [1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
        [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
        [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)],
    ])
    m = np.eye(4)
    m[:3,:3] = rot @ np.diag([s.x,s.y,s.z])
    m[:3,3] = [p.x,p.y,p.z]
    return m

cache = {}
def world(tr_id):
    if tr_id in cache:
        return cache[tr_id]
    tr = transforms[tr_id]
    parent = tr.m_Father.path_id if tr.m_Father else 0
    cache[tr_id] = (world(parent) if parent in transforms else np.eye(4)) @ mat(tr)
    return cache[tr_id]

def ancestry(tr_id):
    result = []
    while tr_id in transforms:
        tr = transforms[tr_id]
        go = gos.get(tr.m_GameObject.path_id)
        if go:
            result.append(go.m_Name)
        tr_id = tr.m_Father.path_id if tr.m_Father else 0
    return list(reversed(result))

rows = []
errors = []
offset = 0
plate_centers = []
obj_path = OUTPUT / 'current-installed-camper.obj'
with obj_path.open('w', encoding='utf-8') as out:
    out.write('# Exact installed REBIRTH camper bundle, 2026-10-23\n')
    for i, o in objects.items():
        if o.type.name != 'MeshFilter':
            continue
        filt = o.read()
        go_id = filt.m_GameObject.path_id
        go = gos.get(go_id)
        if not go or go_id not in go_transforms or go_id not in renderers or not renderers[go_id].m_Enabled:
            continue
        path = ancestry(go_transforms[go_id])
        if 'REBIRTHPickupOnlyBodywork' in path or 'REBIRTHPickupOnlyPaint' in path:
            continue
        if any('LOD1' in p or 'LOD2' in p for p in path):
            continue
        if not filt.m_Mesh or not filt.m_Mesh.path_id:
            continue
        try:
            mesh = filt.m_Mesh.read()
            handler = MeshHandler(mesh)
            handler.process()
            verts = handler.m_Vertices
            triangles = handler.get_triangles()
            if not verts or not triangles:
                continue
            local = np.asarray(verts, dtype=np.float64)
            points = (local @ world(go_transforms[go_id])[:3,:3].T) + world(go_transforms[go_id])[:3,3]
            blender = np.column_stack((points[:,0], -points[:,2], points[:,1]))
            if not np.isfinite(blender).all():
                raise ValueError('nonfinite transformed vertices')
            out.write(f'o mesh_{i}_{go.m_Name.replace(" ", "_").replace("/", "_")}\n')
            for x,y,z in blender:
                out.write(f'v {x:.6f} {y:.6f} {z:.6f}\n')
            face_count = 0
            for sub in triangles:
                for a,b,c in sub:
                    if max(a,b,c) >= len(blender):
                        continue
                    out.write(f'f {a+1+offset} {b+1+offset} {c+1+offset}\n')
                    face_count += 1
            if filt.m_Mesh.path_id == 23893537759662578:
                plate_centers.append(blender[32:96].mean(axis=0).tolist())
            rows.append({'path':'/'.join(path),'mesh_id':filt.m_Mesh.path_id,'vertices':len(blender),'faces':face_count})
            offset += len(blender)
        except Exception as exc:
            errors.append({'path':'/'.join(path),'error':str(exc)[:250]})

manifest = {'source':str(SOURCE),'object_count':len(rows),'vertices':offset,'plate_centers':plate_centers,'errors':errors,'objects':rows}
(OUTPUT/'export-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in manifest.items() if k not in ('errors','objects')}))
print('errors',len(errors),'obj',obj_path)
