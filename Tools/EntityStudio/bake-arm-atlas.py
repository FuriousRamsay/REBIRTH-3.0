"""Blender background diagnostic: bake owned source albedo to new arm UV charts."""
import json
import hashlib
import argparse
import math
import sys
from pathlib import Path
import bpy

args = sys.argv[sys.argv.index('--')+1:]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('folder', type=Path)
parser.add_argument('texture', type=Path)
parser.add_argument('--uv-angle-degrees', type=float, default=66.0)
parser.add_argument('--uv-margin', type=float, default=.015)
options = parser.parse_args(args)
if not 1 <= options.uv_angle_degrees <= 89 or not 0 < options.uv_margin < .1:
    raise RuntimeError('Invalid UV chart parameters')
folder, texture_path = options.folder, options.texture
data = json.loads((folder/'Input.json').read_text())
normal_source = folder/'SourceNormals.json'
source_normals = None
if normal_source.exists():
    normal_data = json.loads(normal_source.read_text())
    if normal_data['inputSha256'] != hashlib.sha256((folder/'Input.json').read_bytes()).hexdigest():
        raise RuntimeError('Source normals do not match the exchange input')
    source_normals = normal_data['normals']
    if len(source_normals) != len(data['vertices']):
        raise RuntimeError('Source normal count differs from exchange vertices')
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = .015
scene.render.bake.max_ray_distance = .04
scene.render.bake.margin = 12
source_image = bpy.data.images.load(str(texture_path))

def object_from(name, vertices, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(v['x'], v['y'], v['z']) for v in vertices], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj

for part in (1, 2):
    bpy.ops.object.select_all(action='DESELECT')
    source_faces = [data['triangles'][i:i+3] for i in range(0, len(data['triangles']), 3)
                    if data['parts'][data['triangles'][i]] == part]
    source = object_from(f'SourceArm{part}', data['vertices'], source_faces)
    for polygon in source.data.polygons:
        polygon.use_smooth = True
    if source_normals is not None:
        source.data.normals_split_custom_set_from_vertices([(n['x'], n['y'], n['z']) for n in source_normals])
    uv = source.data.uv_layers.new(name='OriginalAtlas')
    for loop in source.data.loops:
        value = data['uv'][loop.vertex_index]
        uv.data[loop.index].uv = (value['x'], value['y'])
    material = bpy.data.materials.new(f'SourceOwnedAlbedo{part}')
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    image_node = nodes.new('ShaderNodeTexImage')
    image_node.image = source_image
    emission = nodes.new('ShaderNodeEmission')
    output = nodes.new('ShaderNodeOutputMaterial')
    material.node_tree.links.new(image_node.outputs['Color'], emission.inputs['Color'])
    material.node_tree.links.new(emission.outputs[0], output.inputs['Surface'])
    source.data.materials.append(material)
    arm = json.loads((folder/f'Arm{part}_Transferred.json').read_text())
    faces = [arm['triangles'][i:i+3] for i in range(0, len(arm['triangles']), 3)]
    target = object_from(f'TargetArm{part}', arm['vertices'], faces)
    for polygon in target.data.polygons:
        polygon.use_smooth = True
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(options.uv_angle_degrees), island_margin=options.uv_margin)
    bpy.ops.object.mode_set(mode='OBJECT')
    target_material = bpy.data.materials.new(f'ArmAtlas{part}')
    target_material.use_nodes = True
    image = bpy.data.images.new(f'Arm{part}_Albedo', width=2048, height=2048, alpha=True)
    node = target_material.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image
    target_material.node_tree.nodes.active = node
    target.data.materials.append(target_material)
    source.select_set(True)
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw = str(folder/f'Arm{part}_Albedo.png')
    image.file_format = 'PNG'
    image.save()
    for link in list(emission.inputs['Color'].links):
        material.node_tree.links.remove(link)
    emission.inputs['Color'].default_value = (1, 1, 1, 1)
    coverage_image = bpy.data.images.new(f'Arm{part}_Coverage', width=2048, height=2048, alpha=True)
    coverage_image.colorspace_settings.name = 'Non-Color'
    node.image = coverage_image
    bpy.ops.object.bake(type='EMIT')
    coverage_image.filepath_raw = str(folder/f'Arm{part}_Coverage.png')
    coverage_image.file_format = 'PNG'
    coverage_image.save()
    material.node_tree.links.new(image_node.outputs['Color'], emission.inputs['Color'])
    for suffix in ('roughness', 'metallic'):
        source_map = bpy.data.images.load(str(texture_path.parent/f'{suffix}.png'), check_existing=True)
        source_map.colorspace_settings.name = 'Non-Color'
        image_node.image = source_map
        packed_image = bpy.data.images.new(f'Arm{part}_{suffix}', width=2048, height=2048, alpha=True)
        packed_image.colorspace_settings.name = 'Non-Color'
        node.image = packed_image
        bpy.ops.object.bake(type='EMIT')
        packed_image.filepath_raw = str(folder/f'Arm{part}_{suffix}.png')
        packed_image.file_format = 'PNG'
        packed_image.save()
    source_normal = bpy.data.images.load(str(texture_path.parent/'normal.png'), check_existing=True)
    source_normal.colorspace_settings.name = 'Non-Color'
    image_node.image = source_normal
    normal_node = nodes.new('ShaderNodeNormalMap')
    normal_node.space = 'TANGENT'
    principled = nodes.new('ShaderNodeBsdfPrincipled')
    material.node_tree.links.new(image_node.outputs['Color'], normal_node.inputs['Color'])
    material.node_tree.links.new(normal_node.outputs['Normal'], principled.inputs['Normal'])
    material.node_tree.links.new(principled.outputs['BSDF'], output.inputs['Surface'])
    normal_image = bpy.data.images.new(f'Arm{part}_normal', width=2048, height=2048, alpha=True)
    normal_image.colorspace_settings.name = 'Non-Color'
    node.image = normal_image
    scene.render.bake.normal_space = 'TANGENT'
    bpy.ops.object.bake(type='NORMAL')
    normal_image.filepath_raw = str(folder/f'Arm{part}_normal.png')
    normal_image.file_format = 'PNG'
    normal_image.save()
    target.data.calc_loop_triangles()
    vertices, coordinates, weights, triangles, normals, lookup = [], [], [], [], [], {}
    for triangle in target.data.loop_triangles:
        for loop_index in triangle.loops:
            index = target.data.loops[loop_index].vertex_index
            value = target.data.uv_layers.active.data[loop_index].uv
            key = (index, round(value.x, 8), round(value.y, 8))
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(arm['vertices'][index])
                coordinates.append({'x': value.x, 'y': value.y})
                weights.append(arm['weights'][index])
                normal = target.data.vertices[index].normal
                normals.append({'x': normal.x, 'y': normal.y, 'z': normal.z})
            triangles.append(lookup[key])
    arm.update(vertices=vertices, uv=coordinates, weights=weights, triangles=triangles, normals=normals)
    (folder/f'Arm{part}_Baked.json').write_text(json.dumps(arm))
    print(f'ARM_ATLAS part={part} vertices={len(vertices)} triangles={len(triangles)//3} image=2048x2048')
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.data.objects.remove(target, do_unlink=True)
