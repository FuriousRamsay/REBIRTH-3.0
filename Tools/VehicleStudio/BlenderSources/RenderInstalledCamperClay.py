"""Create a clay review from geometry exported from the installed camper bundle."""
import bpy
import json
from math import pi
from mathutils import Matrix, Vector
from pathlib import Path

root = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CurrentInstalledCamperClay_20261023')
manifest = json.loads((root/'export-manifest.json').read_text())
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.wm.obj_import(filepath=str(root/'current-installed-camper.obj'))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
# The Unity prefab has its vehicle length along local Y. Stand that authoring
# frame upright for a conventional Blender rear view; retain every relative
# transform, including the plate-to-door spacing.
stand = Matrix.Scale(-1, 4, Vector((1, 0, 0))) @ Matrix.Rotation(-pi/2, 4, 'X')
for obj in objects:
    obj.matrix_world = stand @ obj.matrix_world
plate_obj = next(o for o in objects if 'Camper_original_plate_0' in o.name)
plate = sum((plate_obj.matrix_world @ v.co for v in plate_obj.data.vertices), Vector()) / len(plate_obj.data.vertices)

clay = bpy.data.materials.new('Clay - current installed camper')
clay.diffuse_color = (0.72, 0.73, 0.77, 1)
clay.use_nodes = True
clay.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (0.72, 0.73, 0.77, 1)
for obj in objects:
    obj.data.materials.clear()
    obj.data.materials.append(clay)

# Make the exact displayed rear plate distinguishable within the clay render.
plate_mat = bpy.data.materials.new('Clay - moved visible rear plate')
plate_mat.diffuse_color = (0.97, 0.80, 0.42, 1)
plate_mat.use_nodes = True
plate_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (0.97, 0.80, 0.42, 1)
for obj in objects:
    if 'Camper_original_plate_0' in obj.name:
        obj.data.materials.clear()
        obj.data.materials.append(plate_mat)

world = bpy.context.scene.world
world.color = (0.15, 0.15, 0.15)
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.48, 0.49, 0.52, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.7

def area(name, loc, power, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    direction = Vector((0, plate.y-0.5, 1.7))-obj.location
    obj.rotation_euler = direction.to_track_quat('-Z','Y').to_euler()

area('Key', (plate.x-4, plate.y+4, 8), 3000, 8)
area('Fill', (plate.x+5, plate.y+2, 4), 1500, 7)

camera_data = bpy.data.cameras.new('Rear clay camera')
camera = bpy.data.objects.new('Rear clay camera', camera_data)
bpy.context.collection.objects.link(camera)
bpy.context.scene.camera = camera
camera_data.type = 'ORTHO'
camera.location = (0, plate.y+8.5, 2.0)
target = Vector((0, plate.y-0.5, 1.7))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.ortho_scale = 8.8

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.render.filepath = str(root/'current-camper-rear-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'current-installed-camper-clay.blend'))
bpy.ops.render.render(write_still=True)

camera.location = (0, plate.y+3.0, plate.z+0.25)
camera.rotation_euler = (Vector((0,plate.y,plate.z+0.1))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.ortho_scale = 3.0
scene.render.filepath = str(root/'current-camper-plate-detail.png')
bpy.ops.render.render(write_still=True)
print('RENDERED', len(objects), str(root))
