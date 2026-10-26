"""Locate rendered mesh faces around the owner's marked rear bumper end caps."""
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source=Path(sys.argv[1]) if len(sys.argv)>1 else Path('Resources/rebirth_camper80s.unity3d')
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
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
cache={}
def world_matrix(tr_id):
    if tr_id in cache:return cache[tr_id]
    tr=transforms[tr_id];m=local_matrix(tr)
    parent=tr.m_Father.path_id if tr.m_Father else 0
    cache[tr_id]=world_matrix(parent)@m if parent in transforms else m
    return cache[tr_id]
def path(tr_id):
    names=[]
    while tr_id in transforms:
        tr=transforms[tr_id];go=gos.get(tr.m_GameObject.path_id)
        if go:names.append(go.m_Name)
        tr_id=tr.m_Father.path_id if tr.m_Father else 0
    return '/'.join(reversed(names))
for obj in objects.values():
    if obj.type.name!='MeshFilter':continue
    filt=obj.read();go=gos.get(filt.m_GameObject.path_id)
    if not go or not filt.m_Mesh or not filt.m_Mesh.path_id:continue
    comp={c.component.path_id for c in go.m_Component}
    renderer=next((objects[i].read() for i in comp if i in objects and objects[i].type.name=='MeshRenderer'),None)
    if renderer is None or not renderer.m_Enabled:continue
    tr_id=next((i for i in comp if i in transforms),None)
    if tr_id is None:continue
    try:
        helper=MeshHandler(filt.m_Mesh.read());helper.process()
        verts=np.asarray(helper.m_Vertices)
        if verts.size==0:continue
        matrix=world_matrix(tr_id);world=verts@matrix[:3,:3].T+matrix[:3,3]
        matches=np.where((np.abs(world[:,0])>.75)&(np.abs(world[:,0])<1.3)&
                         (world[:,1]>.25)&(world[:,1]<.9)&
                         (world[:,2]<-3.1)&(world[:,2]>-4.0))[0]
        if len(matches)<6:continue
        ids=set(map(int,matches));faces=[f for sub in helper.get_triangles() for f in sub
                                    if len(set(f))==3 and all(i in ids for i in f)]
        if len(faces)<2:continue
        used=np.asarray([world[i] for f in faces for i in f]);bounds=[[round(float(used[:,a].min()),4),round(float(used[:,a].max()),4)] for a in range(3)]
        print(filt.m_Mesh.path_id,'faces',len(faces),'verts',len(matches),'bounds',bounds,path(tr_id))
    except Exception as exc:print('ERROR',go.m_Name,exc,file=sys.stderr)
