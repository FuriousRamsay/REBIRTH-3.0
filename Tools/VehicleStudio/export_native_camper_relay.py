import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'AssetBundle/.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
root=Path(__file__).resolve().parents[2]
env=UnityPy.load(str(root.parent.parent/'Data/Addressables/Standalone/automatic_assets_entities/electrical.bundle'))
go=next(o.read() for o in env.objects if o.type.name=='GameObject' and o.read().m_Name=='relayPrefab')
components=[p.component.read() for p in go.m_Component]
transform=next(c for c in components if c.__class__.__name__=='Transform')
lod=next(c for c in components if c.__class__.__name__=='LODGroup')
allowed={r.renderer.path_id for r in lod.m_LODs[0].renderers}
def vec(v):return [float(v.x),float(v.y),float(v.z)]
parts=[]
def walk(t,chain):
 q=t.m_LocalRotation;chain=chain+[{'p':vec(t.m_LocalPosition),'q':[q.w,q.x,q.y,q.z],'s':vec(t.m_LocalScale)}]
 g=t.m_GameObject.read();cs=[p.component.read() for p in g.m_Component]
 renderer=next((p.component for p in g.m_Component if p.component.type.name=='MeshRenderer'),None)
 if renderer and renderer.path_id in allowed:
  mf=next(c for c in cs if c.__class__.__name__=='MeshFilter');m=MeshHandler(mf.m_Mesh.read());m.process()
  parts.append({'name':g.m_Name,'chain':chain,'vertices':[list(v[:3]) for v in m.m_Vertices],'faces':[list(f) for sm in m.get_triangles() for f in sm]})
 for child in t.m_Children:walk(child.read(),chain)
walk(transform,[])
out=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RelayPlacement_20261020/native-relay.json');out.write_text(json.dumps({'source':'@:Entities/Electrical/relayPrefab.prefab','parts':parts}))
print('Exported',len(parts),'native relay meshes',sum(len(p['faces']) for p in parts),'triangles')
