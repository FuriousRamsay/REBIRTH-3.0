import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownHeadContact_v42';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CrownRivets_v41/crown-rivets-clay.blend'))
spec=json.loads((repo/'upright-harness-plan.json').read_text());deps=bpy.context.evaluated_depsgraph_get();parts=[]
for record in spec['parts']:
    if record['id'] not in ('crown_left_cross','crown_front_cross','crown_right_cross'):continue
    obj=bpy.data.objects['harness_'+record['id']];parent=bpy.data.objects['harness_'+record['parent']]
    assert len(obj.data.vertices)==240
    points=[obj.matrix_world@v.co for v in obj.data.vertices];base=sum(points[:48],Vector())/48;top=sum(points[-48:],Vector())/48;axis=(top-base).normalized()
    tree=BVHTree.FromObject(parent,deps);inverse=parent.matrix_world.inverted()
    hit,n,index,d=tree.ray_cast(inverse@(base+axis*.12),(inverse.to_3x3()@(-axis)).normalized(),.25)
    method='AXIAL_RAY'
    if hit is None:
        hit,n,index,d=tree.find_nearest(inverse@base);method='NEAREST_EXPLICIT_PARENT_EDGE_REQUIRES_POSITION_REVIEW'
    assert hit is not None,f'Unsupported head {obj.name}'
    surface=parent.matrix_world@hit;normal=(parent.matrix_world.to_3x3()@n).normalized()
    if normal.dot(axis)<=.15:
        failure={'object':obj.name,'parent':parent.name,'method':method,'axis':list(axis),'surfaceNormal':list(normal),'alignment':normal.dot(axis),'nearestDistanceMeters':d,'status':'PLAN_REJECTED_EDGE_CONTACT_NOT_PLATE_FACE'}
        (out/'contact-plan-rejection.json').write_text(json.dumps(failure,indent=2));print(json.dumps(failure));sys.exit(1)
    delta=surface+axis*.0005-base;assert delta.length<.08
    parts.append({'object':obj.name,'parent':parent.name,'contactMethod':method,'base':list(base),'axis':list(axis),'contactPoint':list(surface),'contactNormal':list(normal),'delta':list(delta),'destination':list(obj.location+delta)})
plan={'source':'CrownRivets_v41/crown-rivets-clay.blend','status':'MEASURED_PLAN_REQUIRES_CLAY_REVIEW','method':'Use explicit parent from original harness plan. Ray along measured five-ring head axis to actual evaluated parent plate. Seat bottom ring 0.5 mm above surface, preserve shape and rotation.','parts':parts}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps(parts));sys.exit(0)
assert json.loads(path.read_text())==plan
for part in parts:bpy.data.objects[part['object']].location=part['destination']
assert not (out/'head-contact-clay.blend').exists()
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'head-contact-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'heads':parts,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
