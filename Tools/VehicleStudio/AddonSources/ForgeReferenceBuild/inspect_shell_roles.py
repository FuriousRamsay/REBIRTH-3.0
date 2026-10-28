import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
bpy.ops.wm.open_mainfile(filepath=str(root/'MortarRelief_v16/mortar-relief.blend'))
result=[]
for o in bpy.context.scene.objects:
    if o.type!='MESH' or not any(s in o.name.lower() for s in ('collar','ring','crown','furnace_core')):continue
    points=[o.matrix_world@Vector(p) for p in o.bound_box]
    result.append({'object':o.name,'min':[min(p[k] for p in points) for k in range(3)],'max':[max(p[k] for p in points) for k in range(3)],'vertices':len(o.data.vertices)})
(root/'MortarRelief_v16/shell-role-bounds.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
