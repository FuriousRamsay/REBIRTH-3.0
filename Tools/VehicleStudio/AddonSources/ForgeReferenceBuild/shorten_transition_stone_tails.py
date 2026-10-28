import bpy,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'TransitionStoneTails_v63';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SidePatchVolume_v62/side-patch-volume-clay.blend'));sc=bpy.context.scene;axis=sc.camera.rotation_euler.to_quaternion()@Vector((0,0,1));parts=[]
for name in ('stone_left_shoulder_02','stone_left_middle_01','stone_left_edge_behind_bellows'):
    obj=bpy.data.objects[name];count=len(obj.data.vertices);assert (count-1)%4==0;n=(count-1)//4;points=[obj.matrix_world@v.co for v in obj.data.vertices]
    changes=[]
    for i in range(n):
        p=points[i];front=points[n+i];depth=(front-p).dot(axis);move=max(0,depth-.070);destination=p+axis*move
        changes.append({'index':i,'from':list(p),'to':list(destination),'originalTailDepthMeters':depth,'moveMeters':move})
    parts.append({'object':name,'ringSize':n,'vertexChanges':changes})
plan={'source':'SidePatchVolume_v62/side-patch-volume-clay.blend','axisTowardReferenceCamera':list(axis),'method':'Shorten back-cap tails to70 mm behind corresponding silhouette ring along reference-camera axis. Keep all front rings fixed; projected outlines unchanged. Side/rear fit remains inferred and requires render review.','parts':parts,'status':'GEOMETRY_AND_CLAY_REVIEW_REQUIRED'};path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([{'object':p['object'],'maximumMoveMeters':max(c['moveMeters'] for c in p['vertexChanges'])} for p in parts]));sys.exit(0)
assert json.loads(path.read_text())==plan
for part in parts:
    obj=bpy.data.objects[part['object']];before=[obj.matrix_world@v.co for v in obj.data.vertices];after=before.copy()
    for c in part['vertexChanges']:after[c['index']]=Vector(c['to'])
    for polygon in obj.data.polygons:
        a,b,c=list(polygon.vertices)[:3];old=(before[b]-before[a]).cross(before[c]-before[a]);new=(after[b]-after[a]).cross(after[c]-after[a])
        assert old.length<1e-10 or new.length<1e-10 or old.normalized().dot(new.normalized())>0,'Reject flipped face'
for part in parts:
    obj=bpy.data.objects[part['object']];inverse=obj.matrix_world.inverted()
    for c in part['vertexChanges']:obj.data.vertices[c['index']].co=inverse@Vector(c['to'])
    obj.data.update()
assert not (out/'stone-tails-clay.blend').exists();sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'stone-tails-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
