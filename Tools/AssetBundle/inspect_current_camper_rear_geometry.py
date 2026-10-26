"""Report rear light mesh bounds in the installed cumulative camper bundle."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
env = UnityPy.load(str(source))
objects = {o.path_id: o for o in env.objects}
gos = {i: o.read() for i, o in objects.items() if o.type.name == 'GameObject'}
transforms = {i: o.read() for i, o in objects.items() if o.type.name == 'Transform'}
go_transform = {}
for go_id, go in gos.items():
    for c in go.m_Component:
        if c.component.path_id in transforms:
            go_transform[go_id] = c.component.path_id

def path_of(transform_id):
    path = []
    while transform_id in transforms:
        tr = transforms[transform_id]
        go = gos.get(tr.m_GameObject.path_id)
        if go:
            path.append(go.m_Name)
        transform_id = tr.m_Father.path_id if tr.m_Father else 0
    return '/'.join(reversed(path))

rows = []
for obj in objects.values():
    if obj.type.name != 'MeshFilter':
        continue
    filt = obj.read()
    tr_id = go_transform.get(filt.m_GameObject.path_id)
    if not tr_id:
        continue
    path = path_of(tr_id)
    if 'Camper shared lights' not in path and 'Rear lamp' not in path and 'RearRedLenses' not in path:
        continue
    mesh = filt.m_Mesh.read()
    handler = MeshHandler(mesh)
    handler.process()
    vertices = handler.m_Vertices
    if not vertices:
        continue
    rows.append({'path': path, 'mesh_id': filt.m_Mesh.path_id,
                 'count': len(vertices),
                 'x': [min(v[0] for v in vertices), max(v[0] for v in vertices)],
                 'y': [min(v[1] for v in vertices), max(v[1] for v in vertices)],
                 'z': [min(v[2] for v in vertices), max(v[2] for v in vertices)]})
print(json.dumps(rows, indent=2))
for go_id, go in gos.items():
    tr_id = go_transform.get(go_id)
    if not tr_id:
        continue
    path = path_of(tr_id)
    if 'RearRedLenses' not in path and 'Camper shared parts parts' not in path:
        continue
    renderers = [objects[c.component.path_id].read() for c in go.m_Component
                 if c.component.path_id in objects and objects[c.component.path_id].type.name == 'MeshRenderer']
    print('STATE', path, 'active', go.m_IsActive,
          'renderers', [r.m_Enabled for r in renderers])
