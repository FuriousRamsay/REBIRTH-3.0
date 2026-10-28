"""Reopen the saved diagnostic scene and reveal depth from independent angles."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/FloorCoalInspection')
if '--rear-stones' in sys.argv:out=out.parent/'RearMasonryInspection'
source=out/'forge-diagnostic-clay.blend'
if '--mortar' in sys.argv:
 out=out.parent/'ApprovedTextureBaseline/MasonryMortar_v5';source=out/'mortar.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
scene=bpy.context.scene;cam=scene.camera
scene.render.resolution_x=1440;scene.render.resolution_y=1080
target=Vector((-.05,.14,.46));cam.data.ortho_scale=2.1
for name,az in [('side',116.064),('rear',206.064)]:
 a=math.radians(az);e=math.radians(25)
 direction=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
 cam.location=target+direction*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(out/(name+'-view.png'));bpy.ops.render.render(write_still=True)
(out/'saved-scene-inspection.json').write_text(json.dumps({'sceneReopened':True,'views':['side-view.png','rear-view.png'],'fidelity':'UNAPPROVED','originalSceneUnchanged':True},indent=2))
