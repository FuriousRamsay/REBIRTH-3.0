import bpy
from pathlib import Path
from mathutils import Vector,Matrix
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PloughClay')
bpy.ops.wm.open_mainfile(filepath=str(P/'plough-clay-study.blend'))
collection=bpy.data.collections['Reference plough clay']
for obj in bpy.context.scene.objects:
    if obj.type in ['MESH','CURVE'] and collection not in obj.users_collection:obj.hide_render=True
scene=bpy.context.scene;cam=scene.camera;cam.location=(3,1.5,1.30);target=Vector((0,.79,2.55))
forward=(target-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward)
cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=2.90
scene.render.filepath=str(P/'rear-edge-layout.png');bpy.ops.render.render(write_still=True)
