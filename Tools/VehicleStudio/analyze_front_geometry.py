"""Read-only topology/UV isolation of exported Unity cab mesh."""
import collections
import json
from pathlib import Path

FOLDER = Path('E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015')
d = json.loads((FOLDER / 'front-interior.json').read_text())
v = [tuple(p[k] for k in ('x', 'y', 'z')) for p in d['vertices']]
p = list(range(len(v)))

def root(i):
    while p[i] != i:
        p[i] = p[p[i]]
        i = p[i]
    return i

def union(a, b):
    p[root(a)] = root(b)

t = d['triangles']
for i in range(0, len(t), 3):
    union(t[i], t[i + 1])
    union(t[i], t[i + 2])
groups = collections.defaultdict(list)
for i in set(t):
    groups[root(i)].append(i)
out = []
for ids in groups.values():
    out.append(dict(n=len(ids), ids=ids,
                    lo=[min(v[i][k] for i in ids) for k in range(3)],
                    hi=[max(v[i][k] for i in ids) for k in range(3)],
                    uvlo=[min(d['uv'][i][k] for i in ids) for k in ('x', 'y')],
                    uvhi=[max(d['uv'][i][k] for i in ids) for k in ('x', 'y')]))
(FOLDER / 'front-components.json').write_text(json.dumps(out, indent=2))
print('components', len(out))
for item in sorted(out, key=lambda x: -x['n'])[:35]:
    print({k: value for k, value in item.items() if k != 'ids'})

# Two independently indexed upholstery islands. Refuse a changed source layout.
cushions = [g for g in out if g['n'] == 12 and .74 < g['lo'][1] < .76
            and .88 < g['hi'][1] < .90 and -.16 < g['lo'][2] < -.15
            and .40 < g['hi'][2] < .42]
assert len(cushions) == 2, 'Expected exactly two measured cushion islands'
backs = [g for g in out if g['n'] == 14 and .79 < g['lo'][1] < .80
         and 1.25 < g['hi'][1] < 1.26 and -.35 < g['lo'][2] < -.34
         and -.16 < g['hi'][2] < -.15]
assert len(backs) == 2, 'Expected two isolated back upholstery panels'
seed = {i for g in cushions + backs for i in g['ids']}
selected = {i for i, point in enumerate(v) if any(
    sum((point[k] - v[j][k]) ** 2 for k in range(3)) < 1e-10 for j in seed)}
assert 50 <= len(selected) <= 100, 'Unexpected seam-vertex selection'
before, after = [], []
for i in sorted(selected):
    x, y, z = v[i]
    before.append(dict(x=x, y=y, z=z))
    new_y = .69 + (y - .79) * .5 if y < .90 else y
    new_z = -.15 + (z + .15) * .65 if z > -.15 else z
    if z < -.15:
        new_z -= .05 * max(0., min(1., (1.20 - y) / .15))
    after.append(dict(x=x, y=new_y, z=new_z))
plan = dict(source=d['source'], vertexCount=len(v), vertexIds=sorted(selected),
            before=before, after=after, description='Flatten and shorten the isolated cushions; recess lower back upholstery50mm tapering to0 atY1.20; include coincident seam vertices; preserve remaining cab vertices and UVs.')
(FOLDER / 'front-cushion-plan.json').write_text(json.dumps(plan, indent=2))
print('Validated cushion plan:', len(selected), 'vertices; x, y, z bounds after:',
      [[min(p[k] for p in after), max(p[k] for p in after)] for k in ('x', 'y', 'z')])

