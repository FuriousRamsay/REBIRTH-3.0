"""Read-only inventory of rear bumper/lamp objects and local geometry bounds."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
import numpy as np

env=UnityPy.load(str(Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'))
objects={o.path_id:o for o in env.objects}
gos={i:o.read() for i,o in objects.items() if o.type.name=='GameObject'}
transforms={i:o.read() for i,o in objects.items() if o.type.name=='Transform'}
def local_matrix(tr):
    q=tr.m_LocalRotation;x,y,z,w=q.x,q.y,q.z,q.w
    r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    s=tr.m_LocalScale;p=tr.m_LocalPosition
    m=np.eye(4);m[:3,:3]=r@np.diag([s.x,s.y,s.z]);m[:3,3]=[p.x,p.y,p.z]
    return m
def world_matrix(tr_id):
    tr=transforms[tr_id]
    m=local_matrix(tr)
    parent=tr.m_Father.path_id if tr.m_Father else 0
    return world_matrix(parent)@m if parent in transforms else m
names=('Rear bumper - straight span rounded returns','Camper shared parts parts','RearRedLenses','Camper shared lights RearRedLenses')
for go_id,go in gos.items():
    if go.m_Name not in names:continue
    components={c.component.path_id:objects[c.component.path_id] for c in go.m_Component if c.component.path_id in objects}
    filt=next((o.read() for o in components.values() if o.type.name=='MeshFilter'),None)
    if filt is None:continue
    mesh=filt.m_Mesh.read();helper=MeshHandler(mesh);helper.process()
    v=helper.m_Vertices
    tr_id=next((c.component.path_id for c in go.m_Component if c.component.path_id in transforms),None)
    matrix=world_matrix(tr_id)
    world=np.asarray(v)@matrix[:3,:3].T+matrix[:3,3]
    print(go.m_Name,'go',go_id,'mesh',filt.m_Mesh.path_id,'verts',len(v),'faces',sum(map(len,helper.get_triangles())))
    print('world origin',matrix[:3,3].tolist(),'world rear bounds',[[float(world[:,a].min()),float(world[:,a].max())] for a in range(3)])
    for axis in range(3):print('xyz'[axis],min(p[axis] for p in v),max(p[axis] for p in v))
    for side in (-1,1):
        region=[p for p in v if side*p[0]>.8 and .3<p[1]<.9 and -3.9<p[2]<-3.0]
        print('side',side,'region',len(region), [[min(p[a] for p in region),max(p[a] for p in region)] for a in range(3)] if region else None)
