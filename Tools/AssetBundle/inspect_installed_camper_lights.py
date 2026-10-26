"""List every serialized light and its hierarchy in the installed camper bundle."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

source=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(source))
objects={obj.path_id:obj for obj in env.objects}
gos={i:o.read() for i,o in objects.items() if o.type.name=='GameObject'}
transforms={i:o.read() for i,o in objects.items() if o.type.name=='Transform'}
go_transform={}
for go_id,go in gos.items():
    for component in go.m_Component:
        if component.component.path_id in transforms:
            go_transform[go_id]=component.component.path_id

def ancestry(tr_id):
    parts=[]
    while tr_id in transforms:
        tr=transforms[tr_id]
        go=gos.get(tr.m_GameObject.path_id)
        if go:
            parts.append(go.m_Name)
        tr_id=tr.m_Father.path_id if tr.m_Father else 0
    return '/'.join(reversed(parts))

rows=[]
for obj in objects.values():
    if obj.type.name!='Light':
        continue
    light=obj.read()
    go_id=light.m_GameObject.path_id
    tr_id=go_transform.get(go_id)
    tr=transforms.get(tr_id)
    rows.append({'path':ancestry(tr_id), 'active':gos[go_id].m_IsActive,
                 'enabled':light.m_Enabled, 'type':light.m_Type,
                 'range':light.m_Range,'angle':light.m_SpotAngle,
                 'intensity':light.m_Intensity, 'shadows':light.m_Shadows.m_Type, 'cullingMask':light.m_CullingMask,
                 'localPosition':[tr.m_LocalPosition.x,tr.m_LocalPosition.y,tr.m_LocalPosition.z] if tr else None,
                 'localRotation':[tr.m_LocalRotation.x,tr.m_LocalRotation.y,tr.m_LocalRotation.z,tr.m_LocalRotation.w] if tr else None})
print(json.dumps(rows,indent=2,default=str))
