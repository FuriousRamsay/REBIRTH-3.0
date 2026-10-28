import bpy
from pathlib import Path
from mathutils import Vector,Matrix
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PlateSamples')
bpy.ops.wm.open_mainfile(filepath=str(P/'reference-plate-samples.blend'))
scene=bpy.context.scene;cam=scene.camera
views=[('door-fitted',(4,1.6,.6),(.8,.94,.10),1.5),('front-arch-fitted',(4,1.7,2.7),(.8,.86,1.3),2.0),('rear-arch-fitted',(4,1.7,-2.8),(.85,.87,-1.62),2.1),('rear-lower-edge',(4,.58,-2.5),(.85,.65,-1.72),1.8),('front-curve-side',(5,1.05,1.25),(.8,.90,1.25),1.7)]
for label,eye,target,scale in views:
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(P/(label+'.png'));bpy.ops.render.render(write_still=True)
