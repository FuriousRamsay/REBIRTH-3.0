import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'LocalCrownLayers_v58';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CleanMortarCore_v55/clean-core-clay.blend'));deps=bpy.context.evaluated_depsgraph_get();targets=[]
for name in ('harness_left_front_strap','harness_right_front_strap'):
    obj=bpy.data.objects[name];ev=obj.evaluated_get(deps);mesh=ev.to_mesh();targets.append(BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons]));ev.to_mesh_clear()
parts=[]
for name,lift in [('harness_crown_cross_band',.011),('harness_crown_diagonal_left',.022)]:
    obj=bpy.data.objects[name];inverse=obj.matrix_world.inverted();changes=[]
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co;base=p-Vector((0,0,lift));distance=min(tree.find_nearest(base)[3] for tree in targets)
        t=max(0,min(1,1-distance/.055));weight=t*t*(3-2*t);destination=base+Vector((0,0,lift*weight))
        changes.append({'index':vertex.index,'from':list(p),'to':list(destination),'distanceToUnderlyingStrapMeters':distance,'layerWeight':weight})
    parts.append({'object':name,'fullLiftMeters':lift,'vertexChanges':changes})
plan={'source':'CleanMortarCore_v55/clean-core-clay.blend','method':'Replace global rigid lift with smoothstep-weighted Z lift within55 mm of evaluated front straps. Retain original fitted band shape outside overlaps. Preserve thickness and bevel. Rivet contact requires follow-up after changed band surface.','parts':parts,'status':'CLAY_AND_CONTACT_REVIEW_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([{'object':p['object'],'vertices':len(p['vertexChanges']),'fullyLowered':sum(v['layerWeight']==0 for v in p['vertexChanges'])} for p in parts]));sys.exit(0)
assert json.loads(path.read_text())==plan
flips=[]
for part in parts:
    obj=bpy.data.objects[part['object']];inverse=obj.matrix_world.inverted();before=[v.co.copy() for v in obj.data.vertices];after=before.copy()
    for change in part['vertexChanges']:after[change['index']]=inverse@Vector(change['to'])
    for polygon in obj.data.polygons:
        a,b,c=list(polygon.vertices)[:3];old=(before[b]-before[a]).cross(before[c]-before[a]);new=(after[b]-after[a]).cross(after[c]-after[a])
        if old.length>1e-10 and new.length>1e-10 and old.normalized().dot(new.normalized())<=0:flips.append([obj.name,polygon.index])
(out/'plan-validation.json').write_text(json.dumps({'faceFlipScreen':flips,'scope':'Base mesh orientation screen, not full self-intersection or fidelity validation'},indent=2));assert not flips
for part in parts:
    obj=bpy.data.objects[part['object']];inverse=obj.matrix_world.inverted()
    for change in part['vertexChanges']:obj.data.vertices[change['index']].co=inverse@Vector(change['to'])
    obj.data.update()
assert not (out/'local-layers-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'local-layers-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'changedBands':2,'fidelity':'VISUAL_REVIEW_PENDING','rivetsNeedRecheck':True},indent=2))
