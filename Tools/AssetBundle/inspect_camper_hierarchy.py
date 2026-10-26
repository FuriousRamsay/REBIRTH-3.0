"""Summarize the installed camper bundle's rendered mesh hierarchy."""
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

bundle = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
env = UnityPy.load(str(bundle))
objects = {o.path_id: o for o in env.objects}
gos = {i: o.read() for i, o in objects.items() if o.type.name == 'GameObject'}
transforms = {i: o.read() for i, o in objects.items() if o.type.name == 'Transform'}
roots = Counter()
mesh_rows = []
for i, o in objects.items():
    if o.type.name != 'MeshFilter':
        continue
    d = o.read()
    go = gos.get(d.m_GameObject.path_id)
    if not go or not d.m_Mesh or not d.m_Mesh.path_id:
        continue
    tr = next((transforms.get(c.component.path_id) for c in go.m_Component if c.component.path_id in transforms), None)
    ancestors = []
    while tr:
        parent_go = gos.get(tr.m_GameObject.path_id)
        if parent_go:
            ancestors.append(parent_go.m_Name)
        tr = transforms.get(tr.m_Father.path_id) if tr.m_Father else None
    if ancestors:
        roots[ancestors[-1]] += 1
    mesh_rows.append((go.m_Name, d.m_Mesh.path_id, '/'.join(reversed(ancestors))))
print('roots', roots)
print('meshfilters', len(mesh_rows))
for name, mesh, chain in mesh_rows[:100]:
    print(mesh, chain)
