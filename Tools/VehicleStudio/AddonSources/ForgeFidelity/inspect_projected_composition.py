"""Project evaluated component bounds from actual saved clay scene; no rendering edits."""
import bpy,json
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/LeftComposition_01')
bpy.ops.wm.open_mainfile(filepath=str(root/'forge-clay.blend'))
s=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get()
groups={
 'bellowsFrameAndLeather':lambda n:n.startswith('Reference left') and any(t in n for t in ['timber','upright','frame foot','foot neck','bearing','cross shaft','leather']),
 'nozzleAndFlange':lambda n:n.startswith('Reference left') and any(t in n for t in ['flange','tube','collar']),
 'crucibleWithRetainers':lambda n:n.startswith(('Hollow crucible','Crucible')),
 'topCollar':lambda n:n.startswith('Reference thick open vent')}
result={}
for label,predicate in groups.items():
 points=[];names=[]
 for o in s.objects:
  if o.type!='MESH' or not predicate(o.name):continue
  names.append(o.name);ev=o.evaluated_get(deps)
  for vertex in ev.data.vertices:
   p=world_to_camera_view(s,s.camera,ev.matrix_world@vertex.co)
   points.append((p.x,1-p.y))
 if points:result[label]={'bounds':[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)],'objects':names}
(root/'projected-composition.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v['bounds'] for k,v in result.items()},indent=2))
