import sys,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'Tools/AssetBundle/.python'))
import UnityPy
env=UnityPy.load(str(root.parent.parent/'Data/Addressables/Standalone/automatic_assets_other/items.bundle'))
for obj in env.objects:
    if obj.type.name!='GameObject':continue
    go=obj.read()
    if go.m_Name!='turret_laser':continue
    print('LASER',obj.path_id)
    tr=next(c.component.read() for c in go.m_Component if c.component.type.name=='Transform')
    ancestor=tr
    while ancestor:
        print('ANCESTOR',ancestor.m_GameObject.read().m_Name)
        ancestor=ancestor.m_Father.read() if ancestor.m_Father.path_id else None
    for component in go.m_Component:
        c=component.component
        print(c.type.name,json.dumps(c.read_typetree(),default=str)[:18000])
        if c.type.name=='MeshFilter':
            mesh=c.read().m_Mesh.read();print('MESHBOUNDS',mesh.m_LocalAABB)
