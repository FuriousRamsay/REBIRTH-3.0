"""Blend and render the installed camper-visible plate against the clay body."""
import bpy
from pathlib import Path
from mathutils import Vector

root = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio')
source = root / 'CamperRearPlate_20261023/camper-rear-plate-final.blend'
out = root / 'CamperRearPlateVisible_20261023'
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.ops.wm.obj_import(filepath=str(out / 'visible-plate.obj'))
plate = bpy.context.selected_objects[0]
plate.name = 'Installed camper-visible rear plate (corrected)'
mat = bpy.data.materials.new('Dark inspection clay - installed plate')
mat.diffuse_color = (.17, .18, .20, 1)
mat.use_nodes = True
mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.17, .18, .20, 1)
plate.data.materials.clear()
plate.data.materials.append(mat)
scene = bpy.context.scene
camera = scene.camera
camera.location = Vector((0, 8, 1.3))
camera.rotation_euler = (Vector((0, 2.7, 1.3)) - camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 3.4
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.filepath = str(out / 'installed-visible-plate-clay.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out / 'installed-visible-plate-clay.blend'))
