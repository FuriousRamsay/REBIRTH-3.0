import bpy
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkTextured_20261020')
bpy.ops.wm.open_mainfile(filepath=str(out/'truck-bodywork-textured.blend'))
sc=bpy.context.scene;sc.cycles.samples=24;sc.render.resolution_percentage=100
cam=sc.camera;target=Vector((0,.35,.85));direction=Vector((5,6,3));cam.location=target+direction;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=5.9
for o in bpy.data.collections['Studio'].objects:o.hide_set(False)
sc.render.filepath=str(out/'blender-original.png');bpy.ops.render.render(write_still=True)
