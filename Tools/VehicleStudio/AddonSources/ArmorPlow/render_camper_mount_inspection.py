"""Render existing attachment geometry only. Do not change or save the model."""
import bpy,json
from pathlib import Path
from mathutils import Vector,Matrix
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PloughClay')
O=P/'CamperMountInspection';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'plough-clay-study.blend'))
scene=bpy.context.scene;cam=scene.camera;scene.cycles.samples=32
# Restore source camper visibility left hidden only during prior technical renders.
for obj in scene.objects:obj.hide_render=False
def render(name,eye,target,scale):
    cam.location=eye;forward=(Vector(target)-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward)
    cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=scale
    scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
render('side-current-attachment',(4,.75,2.1),(.8,.60,1.96),1.80)
render('under-current-attachment',(2.6,-.15,2.8),(.45,.48,1.92),1.90)
render('rear-current-attachment',(3.1,.58,1.15),(.7,.52,1.95),1.65)
(O/'inspection.json').write_text(json.dumps({'source':'plough-clay-study.blend','geometryChanged':False,'currentRails':{'xCentersM':[-.84,.84],'heightCenterM':.515,'zRearM':1.75,'zFrontM':2.43},'finding':'Rails extend into the front body envelope. This study contains no dedicated chassis-side receiver, mounting plate or chassis fastening joint. Base mesh survey includes exterior body, not a qualified chassis assembly.'},indent=2))
