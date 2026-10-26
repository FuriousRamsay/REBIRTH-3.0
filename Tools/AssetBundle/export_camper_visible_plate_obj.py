"""Export the exact camper-visible plate mesh for Blender clay inspection."""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

source = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
out = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlateVisible_20261023/visible-plate.obj')
env = UnityPy.load(str(source))
mesh = next(o for o in env.objects if o.path_id == 23893537759662578).read()
if mesh.m_Name != 'licence_plate(Clone)' or mesh.m_VertexData.m_VertexCount != 128:
    raise RuntimeError('Wrong visible plate mesh')
indices = struct.unpack('<180H', bytes(mesh.m_IndexBuffer))
if min(indices) != 32 or max(indices) != 95:
    raise RuntimeError('Unexpected visible plate triangles')
raw = mesh.m_VertexData.m_DataSize
vertices = [struct.unpack_from('<fff', raw, i * 48) for i in range(32, 96)]
if not (-.99 < min(v[0] for v in vertices) < -.96 and -.69 < max(v[0] for v in vertices) < -.66):
    raise RuntimeError('Installed plate was not shifted')
with out.open('w') as stream:
    stream.write('o Camper_visible_rear_plate\n')
    for x, y, z in vertices:
        stream.write(f'v {x:.9f} {-z:.9f} {y:.9f}\n')
    for i in range(0, len(indices), 3):
        # Reverse winding for Unity to Blender coordinate conversion.
        a, b, c = (indices[i+j] - 32 + 1 for j in range(3))
        stream.write(f'f {a} {c} {b}\n')
print(out)
