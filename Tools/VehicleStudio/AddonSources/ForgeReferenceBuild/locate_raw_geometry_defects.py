import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'RawDefectLocations_v79';out.mkdir(parents=True,exist_ok=True);classes=json.loads((root/'MasonryDefectClasses_v75/classification.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/'ValidatedBevelCleanup_v78/validated-bevel-clay.blend'));records=[]
for record in classes['objects']:
    if record['category']!='RAW_GEOMETRY':continue
    obj=bpy.data.objects[record['object']];points=[obj.matrix_world@v.co for v in obj.data.vertices]
    records.append({'object':obj.name,'raw':record['raw'],'boundsMin':[min(p[k] for p in points) for k in range(3)] if points else None,'boundsMax':[max(p[k] for p in points) for k in range(3)] if points else None,'materials':[m.name if m else None for m in obj.data.materials],'modifiers':record['modifiers'],'empty':not points})
report={'source':'ValidatedBevelCleanup_v78/validated-bevel-clay.blend','objects':records,'status':'ROLE_AND_REPAIR_CLASSIFICATION_REQUIRED'};path=out/'locations.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps([{'object':r['object'],'vertices':r['raw']['vertices'],'faces':r['raw']['faces'],'boundsMin':r['boundsMin'],'boundsMax':r['boundsMax'],'empty':r['empty']} for r in records]))
