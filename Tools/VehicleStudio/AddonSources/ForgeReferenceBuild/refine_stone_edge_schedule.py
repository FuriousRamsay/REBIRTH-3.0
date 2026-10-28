import bpy,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'StoneEdgeSchedule_v45';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CrownTopFaces_v43/crown-top-face-clay.blend'));parts=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not obj.name.startswith(('stone_','inferred_rear_stone_')):continue
    for modifier in obj.modifiers:
        if modifier.type=='BEVEL' and modifier.width>=.008:
            parts.append({'object':obj.name,'modifier':modifier.name,'fromWidth':modifier.width,'fromSegments':modifier.segments,'toWidth':.003,'toSegments':2})
plan={'source':'CrownTopFaces_v43/crown-top-face-clay.blend','method':'Reduce broad stone edge rounding to 3 mm, 2 segments. No vertices moved; preserve stone volumes, poses and mortar. Compare against 9 mm source edges.','parts':parts,'status':'CLAY_COMPARISON_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('PLANNED_STONE_BEVELS',len(parts));sys.exit(0)
assert json.loads(path.read_text())==plan
for part in parts:
    modifier=bpy.data.objects[part['object']].modifiers[part['modifier']];modifier.width=part['toWidth'];modifier.segments=part['toSegments']
assert not (out/'stone-edges-clay.blend').exists();bpy.ops.wm.save_as_mainfile(filepath=str(out/'stone-edges-clay.blend'))
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'changedBevels':len(parts),'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
