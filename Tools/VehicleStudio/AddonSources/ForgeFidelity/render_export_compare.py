"""Render actual baked material with the reference authoring camera and lighting."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);out=Path(args[1])
if out.exists():raise RuntimeError('Preserve prior baked comparison')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath=str(folder/'export.blend'))
scene=bpy.context.scene;cam=scene.camera;scene.cycles.samples=32
for label,location,target,lens,width,height in [
 ('baked-full',(-1.05,-1.8,1.05),(-.09,-.015,.29),60,1400,1100),
 ('baked-bellows',(-.95,-.82,.62),(-.41,-.01,.30),70,1100,900),
 ('baked-hearth',(-.55,-1.05,.64),(0,-.10,.25),70,1100,900)]:
 cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
 scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.filepath=str(out/(label+'.png'))
 bpy.ops.render.render(write_still=True)
(out/'render-receipt.json').write_text(json.dumps({'status':'BAKED_MATERIAL_COMPARISON_NOT_FIDELITY_ACCEPTANCE','source':str(folder),'samples':32,'lighting':'Saved original scene lights/world','display':scene.view_settings.view_transform,'exposure':scene.view_settings.exposure},indent=2))
