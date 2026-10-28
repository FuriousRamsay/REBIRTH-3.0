import bpy,json,sys,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'HearthLipRecession_v47';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'StoneEdgeSchedule_v45/stone-edges-clay.blend'))
core=bpy.data.objects['furnace_core'];anchor=Vector((.110273242,.027804852,.668842912));axis=Vector((.125862253,-.992047727,0));radius=.075;changes=[];inverse=core.matrix_world.inverted()
for vertex in core.data.vertices:
    world=core.matrix_world@vertex.co;distance=(world-anchor).length
    if distance>=radius:continue
    movement=.025*.5*(1+math.cos(math.pi*distance/radius))
    if movement<.0001:continue
    destination=world-axis*movement
    changes.append({'index':vertex.index,'from':list(world),'to':list(destination),'distanceFromAnchorMeters':distance,'movementMeters':movement})
assert changes
before=[v.co.copy() for v in core.data.vertices];after=before.copy()
for c in changes:after[c['index']]=inverse@Vector(c['to'])
flips=[];changedIndices={c['index'] for c in changes}
for polygon in core.data.polygons:
    indices=list(polygon.vertices)
    if not any(i in changedIndices for i in indices):continue
    a,b,c=indices[:3];old=(before[b]-before[a]).cross(before[c]-before[a]);new=(after[b]-after[a]).cross(after[c]-after[a])
    if old.length>1e-10 and new.length>1e-10 and old.normalized().dot(new.normalized())<=0:flips.append(polygon.index)
assert not flips
plan={'source':'StoneEdgeSchedule_v45/stone-edges-clay.blend','object':core.name,'anchor':list(anchor),'radiusMeters':radius,'maximumMovementMeters':.025,'direction':list(-axis),'method':'Local continuous core recession with cosine falloff, from measured first-hit lip position. No normal threshold mask. No stone, lining, hardware or topology change.','vertexChanges':changes,'faceFlipsDetected':flips,'status':'BOUNDED_PLAN_REQUIRES_CLAY_REVIEW'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('PLANNED_LIP_VERTICES',len(changes));sys.exit(0)
assert json.loads(path.read_text())==plan
for c in changes:core.data.vertices[c['index']].co=inverse@Vector(c['to'])
core.data.update();assert not (out/'lip-recession-clay.blend').exists()
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'lip-recession-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'modifiedVertices':len(changes),'maximumMovementMeters':max(c['movementMeters'] for c in changes),'faceFlipScreen':0,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
