"""Read-only component inspection of the frozen Codex clay candidate."""
import bpy, json
from pathlib import Path
from mathutils import Vector

source = Path('E:/_Haven/shared/REBIRTHTemp/ForgeComparison/Codex/forge-diagnostic-clay.blend')
out = Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ComponentInspection')
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
deps = bpy.context.evaluated_depsgraph_get()
records = []
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.hide_render:
        continue
    ev = obj.evaluated_get(deps)
    mesh = ev.to_mesh()
    pts = [obj.matrix_world @ v.co for v in mesh.vertices]
    records.append({
        'name': obj.name,
        'vertices': len(pts),
        'bounds': [[min(p[i] for p in pts) for i in range(3)],
                   [max(p[i] for p in pts) for i in range(3)]],
        'modifiers': [{'name': m.name, 'type': m.type} for m in obj.modifiers],
        'materials': [m.name if m else None for m in obj.data.materials],
    })
    ev.to_mesh_clear()
(out/'components.json').write_text(json.dumps({'source': str(source), 'objects': records}, indent=2))
print(json.dumps([r for r in records if 'harness' in r['name']], indent=2))
