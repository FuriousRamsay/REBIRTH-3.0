"""Apply Blender-measured delta to the camper-visible rear plate copy.

Only the 64 rear position triples of the 128-vertex camper plate copy change.
Every other serialized object is checked byte-for-byte after repacking.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

MOD = Path(__file__).resolve().parents[2]
SOURCE = MOD / 'Resources' / 'rebirth_camper80s.unity3d'
OUT = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlateVisible_20261023')
CANDIDATE = OUT / 'rebirth_camper80s.unity3d'
DELTA = json.loads(Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlate_20261023/plate-delta.json').read_text())
TARGET = 23893537759662578

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

if CANDIDATE.exists():
    raise RuntimeError('Candidate already exists; refusing overwrite')
OUT.mkdir(parents=True, exist_ok=True)
dx = float(DELTA['deltaX'])
if abs(dx + .15) > .0001 or DELTA['rearVertices'] != 64 or DELTA['allVertices'] != 128:
    raise RuntimeError('Blender edit does not match expected plate shift')

env = UnityPy.load(str(SOURCE))
objects = {obj.path_id: obj for obj in env.objects}
obj = objects[TARGET]
if obj.type.name != 'Mesh':
    raise RuntimeError('Target is not a mesh')
mesh = obj.read()
if mesh.m_Name != 'licence_plate(Clone)' or mesh.m_VertexData.m_VertexCount != 128:
    raise RuntimeError('Unexpected mesh identity or topology')
channels = mesh.m_VertexData.m_Channels
if (channels[0].stream, channels[0].offset, channels[0].format, channels[0].dimension) != (0, 0, 0, 3):
    raise RuntimeError('Unexpected position format')
raw = bytearray(mesh.m_VertexData.m_DataSize)
if len(raw) != 128 * 48:
    raise RuntimeError('Unexpected vertex stride')
rear = []
front = []
for i in range(128):
    x, y, z = struct.unpack_from('<fff', raw, i * 48)
    (rear if z < -3 else front).append((i, x, y, z))
if len(rear) != 64 or len(front) != 64:
    raise RuntimeError('Front/rear vertex partition changed')
old = (min(v[1] for v in rear), max(v[1] for v in rear))
if not (-.84 < old[0] < -.81 and -.54 < old[1] < -.51):
    raise RuntimeError(f'Installed plate at unexpected position {old}')
for i, x, _, _ in rear:
    struct.pack_into('<f', raw, i * 48, x + dx)
mesh.m_VertexData.m_DataSize = bytes(raw)
mesh.save()
env.save(pack='lz4', out_path=str(OUT))

reopened = UnityPy.load(str(CANDIDATE))
after = {o.path_id: o for o in reopened.objects}
if set(objects) != set(after):
    raise RuntimeError('Object inventory changed')
changed = [pid for pid in objects if objects[pid].get_raw_data() != after[pid].get_raw_data()]
if changed != [TARGET]:
    raise RuntimeError(f'Unexpected serialized changes: {changed[:20]} total={len(changed)}')
new_mesh = after[TARGET].read()
new_raw = new_mesh.m_VertexData.m_DataSize
new_rear = [struct.unpack_from('<fff', new_raw, i * 48) for i in range(128)
            if struct.unpack_from('<f', new_raw, i * 48 + 8)[0] < -3]
new_bounds = (min(v[0] for v in new_rear), max(v[0] for v in new_rear))
if len(new_rear) != 64 or not (-.99 < new_bounds[0] < -.96 and -.69 < new_bounds[1] < -.66):
    raise RuntimeError(f'Candidate plate bounds wrong: {new_bounds}')
report = {
    'source': str(SOURCE), 'sourceSha256': sha(SOURCE),
    'candidate': str(CANDIDATE), 'candidateSha256': sha(CANDIDATE),
    'meshPathId': TARGET, 'objects': len(objects), 'changedObjects': changed,
    'rearVertices': len(rear), 'frontVerticesUnchanged': True,
    'oldRearX': old, 'newRearX': new_bounds, 'deltaX': dx,
}
(OUT / 'bundle-verified.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
