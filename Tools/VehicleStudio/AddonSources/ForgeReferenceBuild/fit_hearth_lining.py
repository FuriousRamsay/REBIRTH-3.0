"""Plan and inspect a bounded inner-lining experiment; never promote automatically."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'HearthLiningFit_v31';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MortarContactFit_v29/mortar-contact.blend'))
hearth=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text())
axis=Vector(hearth['outwardAxis']);changes=[]
for obj in bpy.context.scene.objects:
    if not obj.name.startswith('hearth_stones_') or obj.hide_render:continue
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    center=sum(points,Vector())/len(points);records=[]
    for vertex,p in zip(obj.data.vertices,points):
        delta=p-center;plane=delta-axis*delta.dot(axis)
        motion=plane*.25
        if motion.length>.025:motion=motion.normalized()*.025
        records.append({'index':vertex.index,'from':list(p),'to':list(p+motion)})
    changes.append({'object':obj.name,'center':list(center),'rotationUnchanged':list(obj.rotation_euler),'vertices':records})
assert changes
plan={'status':'BOUNDED_EXPERIMENT_REQUIRES_CLAY_REVIEW','source':'MortarContactFit_v29/mortar-contact.blend','method':'Expand existing traced inner stones 25 percent in hearth plane, with each vertex movement capped at 25 mm. Preserve depth, rotations, arch, outer stones and all hardware.','axis':list(axis),'changes':changes}
planpath=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not planpath.exists(),'Preserve prior plan'
    planpath.write_text(json.dumps(plan,indent=2));print('PLAN_SAVED',len(changes));sys.exit(0)
saved=json.loads(planpath.read_text());assert saved==plan,'Plan differs from current saved source'
for record in saved['changes']:
    obj=bpy.data.objects[record['object']];inverse=obj.matrix_world.inverted()
    for change in record['vertices']:obj.data.vertices[change['index']].co=inverse@Vector(change['to'])
    obj.data.update()
for obj in bpy.context.scene.objects:
    if obj.type=='MESH' and obj.name!='Inspection ground':
        material=bpy.data.materials.get('Lining review clay')
        if material is None:
            material=bpy.data.materials.new('Lining review clay');material.diffuse_color=(.5,.5,.5,1)
        obj.data.materials.clear();obj.data.materials.append(material)
scene=bpy.context.scene;scene.cycles.device='CPU';scene.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'hearth-lining-clay.blend'))
scene.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'modifiedObjects':len(changes),'fidelity':'VISUAL_REVIEW_PENDING','materials':'Diagnostic clay only'},indent=2))
