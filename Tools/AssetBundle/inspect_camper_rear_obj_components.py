"""Inspect connected rear-light surfaces in the current installed camper OBJ."""
from pathlib import Path
import json

source = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CurrentInstalledCamperClay_20261023/current-installed-camper.obj')
target = 'Camper_shared_lights_RearRedLenses'
vertices = []
faces = []
active = False
base = 0
for line in source.open(encoding='utf-8'):
    if line.startswith('o '):
        if active:
            break
        active = target in line
        if active:
            base = len(vertices)
    elif line.startswith('v '):
        x, y, z = map(float, line.split()[1:4])
        vertices.append((x, y, z))
    elif active and line.startswith('f '):
        faces.append(tuple(int(v.split('/')[0])-1 for v in line.split()[1:4]))

rear = [face for face in faces if all(vertices[i][1] > 3.4 and vertices[i][2] < .8 for i in face)]
vertex_to_faces = {}
for index, face in enumerate(rear):
    for vertex in face:
        vertex_to_faces.setdefault(vertex, []).append(index)
components = []
unseen = set(range(len(rear)))
while unseen:
    pending = [unseen.pop()]
    component = set(pending)
    while pending:
        for vertex in rear[pending.pop()]:
            for neighbor in vertex_to_faces[vertex]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    pending.append(neighbor)
                    component.add(neighbor)
    used = {v for face_index in component for v in rear[face_index]}
    points = [vertices[v] for v in used]
    components.append({'faces': len(component), 'vertices': len(used),
                       'x': [min(p[0] for p in points), max(p[0] for p in points)],
                       'rear': [min(p[1] for p in points), max(p[1] for p in points)],
                       'height': [min(p[2] for p in points), max(p[2] for p in points)]})
print(json.dumps({'all_faces': len(faces), 'rear_faces': len(rear),
                  'components': sorted(components, key=lambda row:(row['x'][0], row['height'][0]))}, indent=2))
