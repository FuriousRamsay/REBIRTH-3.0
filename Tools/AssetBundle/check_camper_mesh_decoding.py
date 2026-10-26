"""Compare serialized raw and UnityPy-decoded positions in installed rear casing."""
import json,struct,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
p=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(p))
mesh=next(o for o in env.objects if o.path_id==6130890988526752843).read()
h=MeshHandler(mesh);h.process(); verts=h.m_Vertices
raw=mesh.m_VertexData.m_DataSize
rows=[]
for i,v in enumerate(verts):
    if .65<abs(v[0])<.9 and .55<v[1]<.67 and -3.5<v[2]<-3.4:
        r=struct.unpack_from('<fff',raw,i*36)
        delta=max(abs(r[a]-v[a]) for a in range(3))
        if delta>1e-4:
            rows.append(dict(index=i,decoded=list(v),raw=list(r),delta=delta))
print(json.dumps(dict(mismatchCount=len(rows),first=rows[:10],compressedVertices=len(mesh.m_CompressedMesh.m_Vertices.m_Data) if hasattr(mesh.m_CompressedMesh.m_Vertices,'m_Data') else 'unknown'),indent=2))
