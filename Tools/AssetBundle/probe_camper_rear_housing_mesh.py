"""Inspect the exact installed camper rear housing vertex format before editing."""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
mesh_id = -8493837939203116619
env = UnityPy.load(str(source))
mesh_filter = next(o for o in env.objects if o.path_id == mesh_id).read()
mesh = mesh_filter.m_Mesh.read()
handler = MeshHandler(mesh)
handler.process()
raw = mesh.m_VertexData.m_DataSize
print('name', mesh.m_Name, 'count', mesh.m_VertexData.m_VertexCount, 'processed', len(handler.m_Vertices), 'bytes', len(raw), 'stride', len(raw)//mesh.m_VertexData.m_VertexCount, 'mesh_id', mesh_filter.m_Mesh.path_id)
print('triangles', sum(len(sub) for sub in handler.get_triangles()))
print('references', sum(1 for obj in env.objects if obj.type.name=='MeshFilter' and obj.read().m_Mesh.path_id==mesh_filter.m_Mesh.path_id))
for i in range(3):
    print('vertex', i, handler.m_Vertices[i], 'raw', struct.unpack_from('<fff', raw, i*36))
