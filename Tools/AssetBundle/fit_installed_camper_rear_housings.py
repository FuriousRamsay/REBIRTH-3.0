"""Fit the four existing rear housing rims around the unchanged rear lenses."""
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

mod = Path(__file__).resolve().parents[2]
source = mod / 'Resources' / 'rebirth_camper80s.unity3d'
output = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearHousing_20261023_v2')
candidate = output / source.name
target = 6130890988526752843
scale = 1.25
rear_shift = -.011

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

if candidate.exists():
    raise RuntimeError('Refusing to overwrite the existing candidate')
output.mkdir(parents=True, exist_ok=True)
env = UnityPy.load(str(source))
objects = {obj.path_id: obj for obj in env.objects}
mesh = objects[target].read()
if mesh.m_Name != 'shared_camper_31' or mesh.m_VertexData.m_VertexCount != 13414:
    raise RuntimeError('Unexpected installed camper housing mesh')
helper = MeshHandler(mesh)
helper.process()
vertices = helper.m_Vertices
faces = [tuple(face) for submesh in helper.get_triangles() for face in submesh]
rear_faces = [face for face in faces if all(.68 < abs(vertices[i][0]) < .89
              and .55 < vertices[i][1] < .67 and -3.47 < vertices[i][2] < -3.42 for i in face)]
index_to_faces = {}
for face_index, face in enumerate(rear_faces):
    for vertex in face:
        index_to_faces.setdefault(vertex, []).append(face_index)
unseen = set(range(len(rear_faces)))
components = []
while unseen:
    pending = [unseen.pop()]
    component = set(pending)
    while pending:
        for vertex in rear_faces[pending.pop()]:
            for adjacent in index_to_faces[vertex]:
                if adjacent in unseen:
                    unseen.remove(adjacent)
                    pending.append(adjacent)
                    component.add(adjacent)
    indices = {vertex for face_index in component for vertex in rear_faces[face_index]}
    xs = [vertices[i][0] for i in indices]
    ys = [vertices[i][1] for i in indices]
    components.append((component, indices, min(xs), max(xs), min(ys), max(ys)))
outer = [row for row in components if len(row[0]) == 32
         and .065 < row[3]-row[2] < .068 and .064 < row[5]-row[4] < .067]
if len(outer) != 4 or len(rear_faces) != 320 or len(components) != 12:
    raise RuntimeError(f'Unexpected housing topology: {len(outer)} outer / {len(rear_faces)} faces / {len(components)} components')

raw = bytearray(mesh.m_VertexData.m_DataSize)
if len(raw) != len(vertices)*36:
    raise RuntimeError('Unexpected housing vertex stride')
changed = set()
centers = []
for _, indices, xmin, xmax, ymin, ymax in outer:
    cx, cy = (xmin+xmax)/2, (ymin+ymax)/2
    centers.append([cx, cy])
    for i in indices:
        x, y, z = struct.unpack_from('<fff', raw, i*36)
        if max(abs(x-vertices[i][0]),abs(y-vertices[i][1]),abs(z-vertices[i][2])) > 1e-5:
            raise RuntimeError('Raw vertex data differs from decoded mesh')
        struct.pack_into('<fff', raw, i*36,
                         cx+(x-cx)*scale, cy+(y-cy)*scale, z+rear_shift)
        changed.add(i)
if len(changed) != 128:
    raise RuntimeError(f'Unexpected changed vertex count: {len(changed)}')
mesh.m_VertexData.m_DataSize = bytes(raw)
old_min = mesh.m_LocalAABB.m_Center.z-mesh.m_LocalAABB.m_Extent.z
new_min = min(struct.unpack_from('<f', raw, i*36+8)[0] for i in changed)
if new_min < old_min:
    expansion = (old_min-new_min)/2
    mesh.m_LocalAABB.m_Center.z -= expansion
    mesh.m_LocalAABB.m_Extent.z += expansion
mesh.save()
env.save(pack='lz4', out_path=str(output))

check = UnityPy.load(str(candidate))
after = {obj.path_id: obj for obj in check.objects}
changed_objects = [path_id for path_id in objects if objects[path_id].get_raw_data() != after[path_id].get_raw_data()]
if changed_objects != [target]:
    raise RuntimeError(f'Unexpected changed objects: {changed_objects}')
verified = after[target].read().m_VertexData.m_DataSize
for i in changed:
    x0, y0, z0 = struct.unpack_from('<fff', raw, i*36)
    x1, y1, z1 = struct.unpack_from('<fff', verified, i*36)
    if max(abs(x0-x1),abs(y0-y1),abs(z0-z1)) > 1e-6:
        raise RuntimeError('Saved housing differs from candidate vertex data')
report = {'sourceSha256': sha(source), 'candidateSha256': sha(candidate),
          'meshPathId': target, 'changedObjects': changed_objects,
          'changedVertices': len(changed), 'outerRims': len(outer),
          'rimCenters': sorted(centers), 'radialScale': scale,
          'rearShiftMeters': rear_shift, 'serializedObjectCount': len(objects)}
(output/'bundle-verified.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
