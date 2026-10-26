"""Render the saved Blender clay source from directly behind the camper."""
import bpy
from mathutils import Vector
from pathlib import Path

folder = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlate_20261023')
bpy.ops.wm.open_mainfile(filepath=str(folder / 'camper-rear-plate-final.blend'))
scene = bpy.context.scene
camera = scene.camera
camera.location = Vector((0.0, 8.0, 1.30))
camera.rotation_euler = (Vector((0.0, 2.7, 1.30)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 3.4
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.filepath = str(folder / 'camper-rear-clay.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'camper-rear-plate-final.blend'))
