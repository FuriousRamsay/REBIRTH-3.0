"""Read-only mesh identities and rear face bounds for camper lamp LODs."""
import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
source=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(source)); objects={o.path_id:o for o in env.objects}
gos={i:o.read() for i,o in objects.items() if o.type.name=='GameObject'}
targets={6334569543015674285,9186490770423258333,-1539019402489239449,8483864902034819404,-7330270643628669136,3804555665347380124}
rows=[]
for i,o in objects.items():
    if o.type.name!='GameObject':continue
    go=gos[i]; comps={c.component.path_id for c in go.m_Component}
    renderer=next((p for p in comps if p in targets),None)
    if renderer is None:continue
    filt=next((objects[p].read() for p in comps if p in objects and objects[p].type.name=='MeshFilter'),None)
    if filt is None or not filt.m_Mesh:continue
    mesh=filt.m_Mesh.read(); helper=MeshHandler(mesh); helper.process(); verts=helper.m_Vertices
    rear=[v for v in verts if .6<abs(v[0])<.95 and .45<v[1]<.8 and -3.6<v[2]<-3.3]
    rows.append(dict(name=go.m_Name,renderer=renderer,mesh=filt.m_Mesh.path_id,meshName=mesh.m_Name,
                     rearVertices=len(rear),vertexCount=len(verts),rawBytes=len(mesh.m_VertexData.m_DataSize),
                     stride=len(mesh.m_VertexData.m_DataSize)//len(verts),
                     bounds=[[min(v[a] for v in rear),max(v[a] for v in rear)] for a in range(3)] if rear else None))
print(json.dumps(rows,indent=2))
