import bpy,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsStitches_v34'
bpy.ops.wm.open_mainfile(filepath=str(out/'bellows-stitches-clay.blend'))
smooth='--smooth-leather' in sys.argv
if smooth:
    out=root/'BellowsLeatherNormals_v35';out.mkdir(parents=True,exist_ok=True)
    body=bpy.data.objects['planned_mesh_000']
    for polygon in body.data.polygons:polygon.use_smooth=True
    assert not (out/'leather-normals-clay.blend').exists()
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'leather-normals-clay.blend'))
plan=json.loads((root/'BellowsSeams_v33/geometry-plan.json').read_text());points=[Vector(p) for part in plan['parts'] for p in part['points']]
target=sum(points,Vector())/len(points);sc=bpy.context.scene;cam=sc.camera
direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));cam.location=target-direction*4
cam.data.ortho_scale=.52;sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.render.resolution_percentage=100
sc.cycles.device='CPU';sc.cycles.samples=48;sc.render.filepath=str(out/'bellows-closeup.png');assert not Path(sc.render.filepath).exists()
bpy.ops.render.render(write_still=True)
(out/'closeup-receipt.json').write_text(json.dumps({'source':'bellows-stitches-clay.blend','smoothLeatherOnly':smooth,'target':list(target),'cameraLocation':list(cam.location),'orthoScale':.52,'resolution':[1200,900],'purpose':'Actual surface contact and stitch inspection; unchanged source geometry'},indent=2))
