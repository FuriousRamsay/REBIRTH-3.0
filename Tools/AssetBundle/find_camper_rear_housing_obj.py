"""Locate installed camper OBJ surfaces at the rear bumper lamp height."""
from pathlib import Path
import json

source = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CurrentInstalledCamperClay_20261023/current-installed-camper.obj')
vertices = []
name = None
faces = []
rows = []

def finish():
    if not faces:
        return
    used = {i for face in faces for i in face}
    points = [vertices[i] for i in used]
    rows.append({'name': name, 'faces': len(faces),
                 'x': [min(p[0] for p in points), max(p[0] for p in points)],
                 'rear': [min(p[1] for p in points), max(p[1] for p in points)],
                 'height': [min(p[2] for p in points), max(p[2] for p in points)]})

for line in source.open(encoding='utf-8'):
    if line.startswith('o '):
        finish()
        name = line[2:].strip()
        faces = []
    elif line.startswith('v '):
        vertices.append(tuple(map(float, line.split()[1:4])))
    elif line.startswith('f '):
        face = tuple(int(v.split('/')[0])-1 for v in line.split()[1:4])
        if all(vertices[i][1] > 3.0 and .4 < vertices[i][2] < 1.1 for i in face):
            faces.append(face)
finish()
print(json.dumps(sorted(rows, key=lambda r:-r['faces'])[:35], indent=2))
