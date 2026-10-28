import bpy,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownRivets_v41';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'BellowsLeatherNormals_v35/leather-normals-clay.blend'))
assignments=[('harness_crown_left_cross','harness_crown_diagonal_left',.022),('harness_crown_front_cross','harness_crown_cross_band',.011),('harness_crown_right_cross','harness_crown_cross_band',.011)]
parts=[]
for name,band,lift in assignments:
    obj=bpy.data.objects[name];position=obj.location+Vector((0,0,lift));center=sum((obj.matrix_world@v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
    parts.append({'object':name,'band':band,'originalLocation':list(obj.location),'destination':list(position),'originalMeshCenter':list(center),'liftMeters':lift,'rotationUnchanged':list(obj.rotation_euler)})
plan={'source':'CrownRigidLayers_v40/crown-layers-clay.blend','baseline':'BellowsLeatherNormals_v35/leather-normals-clay.blend','method':'Assign original crown fasteners to their band and apply matching rigid Z translation; do not infer translation from closest surface.','parts':parts,'status':'PLAN_REQUIRES_CLAY_REVIEW'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps(parts));sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']))
for part in parts:
    obj=bpy.data.objects[part['object']];obj.location=part['destination'];assert (Vector(obj.rotation_euler)-Vector(part['rotationUnchanged'])).length<.00001
assert not (out/'crown-rivets-clay.blend').exists()
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'crown-rivets-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'movedRivets':parts,'geometryUnchanged':True,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
