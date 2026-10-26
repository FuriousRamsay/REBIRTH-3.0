import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
e=UnityPy.load(sys.argv[1]);o={x.path_id:x for x in e.objects}
for mid in (-6753923948166799871,-6045061701533401843,8398484439755328957,6130890988526752843,-6571958702597769235):
    m=o[mid];t=m.read_typetree();h=MeshHandler(m.read());h.process()
    vd=t['m_VertexData'];print(mid,m.read().m_Name,'verts',vd['m_VertexCount'],'stride',len(vd['m_DataSize'])//vd['m_VertexCount'],'stream',t['m_StreamData']['size'])
    print(vd['m_Channels'])
    print('normal0',h.m_Normals[0] if h.m_Normals else None,'uv0',h.m_UV0[0] if h.m_UV0 else None)
