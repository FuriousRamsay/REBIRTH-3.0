import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrucibleRimBreaks_v70';plan=json.loads((out/'geometry-plan.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));obj=bpy.data.objects[plan['object']];inverse=obj.matrix_world.inverted()
def audit():
    bm=bmesh.new();bm.from_mesh(obj.data);report={'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True)};bm.free();return report
before=audit()
for change in plan['vertexChanges']:assert (obj.matrix_world@obj.data.vertices[change['index']].co-Vector(change['from'])).length<.000001
for change in plan['vertexChanges']:obj.data.vertices[change['index']].co=inverse@Vector(change['to'])
obj.data.update();after=audit();assert after['nonManifoldEdges']==before['nonManifoldEdges'] and after['zeroAreaFaces']==before['zeroAreaFaces'] and after['signedVolume']>0
assert not (out/'rim-breaks-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(out/'rim-breaks-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
cam=sc.camera;target=Vector((*plan['centerXY'],plan['topZ']-.09));direction=cam.rotation_euler.to_quaternion()@Vector((0,0,-1));cam.location=target-direction*3;cam.data.ortho_scale=.62;sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.render.filepath=str(out/'crucible-closeup.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'before':before,'after':after,'poseAndHardwareUnchanged':True,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
