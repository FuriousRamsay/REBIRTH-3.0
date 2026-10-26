"""Read-only connected-component inventory for installed camper rear lamp meshes."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(source))
objects={obj.path_id:obj for obj in env.objects}
rows=[]
for mesh_id in (6130890988526752843,-9148037036518840170,-4796251074057068951,
                -6571958702597769235,209635268596600669,-7204612321527722442):
    mesh=objects[mesh_id].read()
    helper=MeshHandler(mesh); helper.process()
    verts=helper.m_Vertices
    faces=[tuple(f) for sub in helper.get_triangles() for f in sub]
    rear=[f for f in faces if all(.65<abs(verts[i][0])<.91 and .50<verts[i][1]<.72 and -3.53<verts[i][2]<-3.38 for i in f)]
    adjacency={}
    for fi,face in enumerate(rear):
        for vertex in face: adjacency.setdefault(vertex,[]).append(fi)
    unseen=set(range(len(rear)))
    comps=[]
    while unseen:
        queue=[unseen.pop()]; component=set(queue)
        while queue:
            for vertex in rear[queue.pop()]:
                for neighbor in adjacency[vertex]:
                    if neighbor in unseen:
                        unseen.remove(neighbor); queue.append(neighbor); component.add(neighbor)
        indices=sorted({v for fi in component for v in rear[fi]})
        coord=[[verts[i][axis] for i in indices] for axis in range(3)]
        comps.append(dict(faces=len(component),vertices=len(indices),
                          x=[min(coord[0]),max(coord[0])],y=[min(coord[1]),max(coord[1])],
                          z=[min(coord[2]),max(coord[2])],indices=indices))
    rows.append(dict(mesh=mesh.m_Name,mesh_id=mesh_id,rear_faces=len(rear),components=sorted(comps,key=lambda c:(c['x'][0],c['z'][0]))))
print(json.dumps(rows,indent=2))
