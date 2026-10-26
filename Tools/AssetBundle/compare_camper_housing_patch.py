import json,struct,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
before=Path('E:/_Haven/shared/REBIRTHBackups/VehicleStudio/CamperRearHousing_20261023/rebirth_camper80s.unity3d')
after=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
rows=[]
for path in (before,after):
    env=UnityPy.load(str(path));mesh=next(o for o in env.objects if o.path_id==6130890988526752843).read()
    h=MeshHandler(mesh);h.process();rows.append((mesh,h.m_Vertices))
(a,va),(b,vb)=rows
ra,rb=a.m_VertexData.m_DataSize,b.m_VertexData.m_DataSize
changed=[]
for i in range(len(va)):
    p=struct.unpack_from('<fff',ra,i*36);q=struct.unpack_from('<fff',rb,i*36)
    if max(abs(p[k]-q[k]) for k in range(3))>1e-6:
        changed.append(dict(index=i,before=p,after=q,decodedBefore=list(va[i]),decodedAfter=list(vb[i])))
print(json.dumps(dict(count=len(changed),first=changed[:20],
    boundsBefore=[a.m_LocalAABB.m_Center.z,a.m_LocalAABB.m_Extent.z],
    boundsAfter=[b.m_LocalAABB.m_Center.z,b.m_LocalAABB.m_Extent.z],
    vertexBytesEqual=ra==rb),indent=2))
