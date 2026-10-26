"""List connected rendered face groups near the camper's rear bumper caps."""
import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source=Path(sys.argv[1]) if len(sys.argv)>1 else Path('Resources/rebirth_camper80s.unity3d')
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
for mesh_id in (-6753923948166799871,-6045061701533401843,8398484439755328957):
    h=MeshHandler(objects[mesh_id].read());h.process();v=h.m_Vertices
    faces=[tuple(f) for sub in h.get_triangles() for f in sub if len(set(f))==3 and
           all(.55<abs(v[i][0])<1.05 and .38<v[i][1]<.78 and -3.58<v[i][2]<-3.25 for i in f)]
    adjacency={}
    for fi,face in enumerate(faces):
        for index in face:
            key=tuple(round(c,4) for c in v[index])
            adjacency.setdefault(key,[]).append(fi)
    unseen=set(range(len(faces)));groups=[]
    while unseen:
        seed=unseen.pop();queue=[seed];group={seed}
        while queue:
            for index in faces[queue.pop()]:
                key=tuple(round(c,4) for c in v[index])
                for other in adjacency[key]:
                    if other in unseen:unseen.remove(other);queue.append(other);group.add(other)
        ids={i for fi in group for i in faces[fi]}
        axes=[[v[i][a] for i in ids] for a in range(3)]
        groups.append(dict(faces=len(group),verts=len(ids),bounds=[[round(min(a),4),round(max(a),4)] for a in axes]))
    print('\nmesh',mesh_id,'faces',len(faces),'components',len(groups))
    print(json.dumps(sorted((g for g in groups if g['faces']>=6),key=lambda g:(g['bounds'][0][0],g['bounds'][1][0],g['bounds'][2][0])),indent=2))
