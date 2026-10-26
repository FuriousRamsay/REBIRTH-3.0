"""Move the installed camper's displayed rear plate inward based on clay review."""
import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

MOD = Path(__file__).resolve().parents[2]
SOURCE = MOD / 'Resources' / 'rebirth_camper80s.unity3d'
OUT = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlateRecentered_20261023')
CANDIDATE = OUT / 'rebirth_camper80s.unity3d'
TARGET = 23893537759662578
DX = 0.10

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

if CANDIDATE.exists():
    raise RuntimeError('Candidate already exists')
OUT.mkdir(parents=True, exist_ok=True)
env = UnityPy.load(str(SOURCE))
objects = {o.path_id: o for o in env.objects}
mesh = objects[TARGET].read()
if mesh.m_Name != 'licence_plate(Clone)' or mesh.m_VertexData.m_VertexCount != 128:
    raise RuntimeError('Unexpected displayed plate mesh')
raw = bytearray(mesh.m_VertexData.m_DataSize)
if len(raw) != 128 * 48:
    raise RuntimeError('Unexpected vertex data layout')
rear = [i for i in range(128) if struct.unpack_from('<f', raw, i*48+8)[0] < -3]
if len(rear) != 64:
    raise RuntimeError('Unexpected rear vertex count')
old = [struct.unpack_from('<f', raw, i*48)[0] for i in rear]
if not (-.99 < min(old) < -.96 and -.69 < max(old) < -.66):
    raise RuntimeError(f'Unexpected current plate bounds {min(old)} {max(old)}')
for i in rear:
    x = struct.unpack_from('<f', raw, i*48)[0]
    struct.pack_into('<f', raw, i*48, x+DX)
mesh.m_VertexData.m_DataSize = bytes(raw)
mesh.save()
env.save(pack='lz4', out_path=str(OUT))

check = UnityPy.load(str(CANDIDATE))
after = {o.path_id:o for o in check.objects}
changed = [pid for pid in objects if objects[pid].get_raw_data() != after[pid].get_raw_data()]
if changed != [TARGET]:
    raise RuntimeError(f'Unexpected changed objects: {changed}')
new_raw = after[TARGET].read().m_VertexData.m_DataSize
new = [struct.unpack_from('<f', new_raw, i*48)[0] for i in rear]
if not (-.89 < min(new) < -.86 and -.59 < max(new) < -.56):
    raise RuntimeError(f'Unexpected new bounds {min(new)} {max(new)}')
report = {'sourceSha256':sha(SOURCE),'candidateSha256':sha(CANDIDATE),'meshPathId':TARGET,
          'changedObjects':changed,'oldRearX':[min(old),max(old)],'newRearX':[min(new),max(new)],
          'deltaX':DX,'rearVertices':len(rear),'objectCount':len(objects)}
(OUT/'bundle-verified.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
