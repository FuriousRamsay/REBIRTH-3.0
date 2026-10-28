import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'WallStoneFacets_v68';plan=json.loads((out/'geometry-plan.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));D=Vector(plan['cameraAxis']);errors=[]
for part in plan['parts']:
    obj=bpy.data.objects[part['object']]
    for change in part['vertexChanges']:
        before=Vector(change['from']);after=Vector(change['to']);actual=obj.matrix_world@obj.data.vertices[change['index']].co
        assert (actual-before).length<.000001,'Plan source differs'
        delta=after-before;error=(delta-D*delta.dot(D)).length;errors.append(error);assert error<.000001,'Projection changed'
for part in plan['parts']:
    obj=bpy.data.objects[part['object']];inverse=obj.matrix_world.inverted()
    for change in part['vertexChanges']:obj.data.vertices[change['index']].co=inverse@Vector(change['to'])
    obj.data.update()
assert not (out/'wall-facets-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'wall-facets-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'build-receipt.json').write_text(json.dumps({'source':plan['source'],'changedObjects':[p['object'] for p in plan['parts']],'maximumProjectionErrorMeters':max(errors),'fidelity':'VISUAL_AND_INTERSECTION_REVIEW_PENDING'},indent=2))
