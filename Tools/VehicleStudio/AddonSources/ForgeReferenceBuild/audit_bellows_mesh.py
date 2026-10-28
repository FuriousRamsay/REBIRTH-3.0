import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
bpy.ops.wm.open_mainfile(filepath=str(root/'LeftChamberLining_v32/left-lining-clay.blend'))
records=[]
for o in bpy.context.scene.objects:
    if o.type=='MESH' and o.name.startswith(('harness_crown','harness_left_front','harness_right_front')):
        records.append({'name':o.name,'vertices':len(o.data.vertices),'dimensions':list(o.dimensions),'location':list(o.location),'modifiers':[m.type for m in o.modifiers]})
print(json.dumps(records));(root/'LeftChamberLining_v32/harness-mesh-audit.json').write_text(json.dumps(records,indent=2))
