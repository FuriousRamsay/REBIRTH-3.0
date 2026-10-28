"""Blender authoring prototype: new compact masonry forge, no imported station mesh."""
import bpy, math, random
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012/Forge_v7')
out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(12)
def material(name,color,metal=0,rough=.85):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    return m
stone=material('Sooted clay masonry',(.28,.22,.16)); steel=material('Oxidized iron',(.10,.09,.075),.7,.72)
dark=material('Dark hearth recess',(.018,.014,.01)); ash=material('Ash bed',(.13,.12,.105))
def cube(name,loc,size,mat,bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name
    o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('Rounded worn edges','BEVEL');mod.width=bevel;mod.segments=2
        o.modifiers.new('Surface normals','WEIGHTED_NORMAL')
    return o
# Compact .78m x .64m footprint, low arched clay/stone body with actual open mouth.
cube('Mounting skid',(0,0,.035),(.82,.68,.07),steel)
cube('Hearth floor',(0,0,.10),(.76,.60,.10),stone)
# Retain native forge dome, arched mouth and side bellows silhouette in a low mount.
bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=(0,.05,.29))
dome=bpy.context.object;dome.name='Compact native-style masonry dome';dome.scale=(.34,.28,.39)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);dome.data.materials.append(stone)
cut=cube('Hearth opening cutter',(0,-.25,.24),(.27,.48,.25),dark,.04)
bpy.context.view_layer.objects.active=cut
for mod in list(cut.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.context.view_layer.objects.active=dome
mod=dome.modifiers.new('Real hearth opening','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cut
bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
cube('Dark inner hearth',(0,.04,.23),(.24,.015,.20),dark,0)
cube('Ash tray',(0,-.10,.165),(.25,.27,.02),ash)
cube('Bellows support',(-.31,-.18,.27),(.06,.10,.32),steel)
cube('Side bellows',(-.31,-.16,.44),(.16,.22,.07),steel,.03)
bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.09,depth=.025,location=(-.27,-.22,.18))
bpy.context.object.name='Small working basin';bpy.context.object.data.materials.append(steel)
leather=material('Worn bellows leather',(.105,.055,.027),0,.9)
for row in range(5):
    cube('Bellows leather pleat',(-.31,-.16,.40+row*.015),(.14+(row%2)*.015,.20+(row%2)*.02,.012),leather,.005)
cube('Bellows wooden top',(-.31,-.16,.49),(.18,.24,.025),stone,.01)
cube('Bellows lever',(-.31,-.16,.55),(.025,.28,.025),steel,.004)
for z in [.25,.46]:
    bpy.ops.mesh.primitive_torus_add(major_radius=.30 if z==.25 else .26,minor_radius=.009,major_segments=32,minor_segments=6,location=(0,.05,z))
    bpy.context.object.name='Dome reinforcing iron band';bpy.context.object.scale.y=.82;bpy.context.object.data.materials.append(steel)
for x in [-.35,.35]:
    cube('Securing strap',(x,0,.12),(.025,.64,.025),steel,.004)
    for y in [-.27,.27]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.018,depth=.018,location=(x,y,.085))
        bpy.context.object.name='Mount bolt';bpy.context.object.data.materials.append(steel)
# Keep reinforcement out of the hearth mouth.
import bmesh
for band in [o for o in bpy.context.scene.objects if o.name.startswith('Dome reinforcing')]:
    bm=bmesh.new();bm.from_mesh(band.data)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if (band.matrix_world @ v.co).y < -.13],context='VERTS')
    bm.to_mesh(band.data);bm.free()
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        bpy.ops.object.convert(target='MESH');obj.select_set(False)
# Bake procedural masonry variation to the dome's real UVs; retain source nodes.
bpy.ops.object.select_all(action='DESELECT');dome.select_set(True);bpy.context.view_layer.objects.active=dome
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
nodes=stone.node_tree.nodes;links=stone.node_tree.links
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18;noise.inputs['Detail'].default_value=4
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.045,.04,.033,1);ramp.color_ramp.elements[1].color=(.32,.29,.24,1)
links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
voronoi=nodes.new('ShaderNodeTexVoronoi');voronoi.feature='DISTANCE_TO_EDGE';voronoi.inputs['Scale'].default_value=8
seam=nodes.new('ShaderNodeValToRGB');seam.color_ramp.elements[0].position=.005;seam.color_ramp.elements[0].color=(.026,.023,.018,1);seam.color_ramp.elements[1].position=.022;seam.color_ramp.elements[1].color=(.13,.115,.09,1)
links.new(voronoi.outputs['Distance'],seam.inputs[0]);links.new(seam.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.45;bump.inputs['Distance'].default_value=.018
links.new(voronoi.outputs['Distance'],bump.inputs['Height']);links.new(bump.outputs['Normal'],nodes.get('Principled BSDF').inputs['Normal'])
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.65
links.new(seam.outputs['Color'],mix.inputs[1]);links.new(ramp.outputs['Color'],mix.inputs[2]);links.new(mix.outputs[0],nodes.get('Principled BSDF').inputs['Base Color'])
for poly in dome.data.polygons: poly.use_smooth=True
image=bpy.data.images.new('Forge masonry albedo',width=1024,height=1024)
texture=nodes.new('ShaderNodeTexImage');texture.image=image;nodes.active=texture
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=16
bpy.context.scene.render.bake.use_pass_direct=False;bpy.context.scene.render.bake.use_pass_indirect=False;bpy.context.scene.render.bake.use_pass_color=True
bpy.ops.object.bake(type='DIFFUSE');image.filepath_raw=str(out/'forge-masonry-albedo.png');image.file_format='PNG';image.save()
normal=bpy.data.images.new('Forge masonry normal',width=1024,height=1024);normal.colorspace_settings.name='Non-Color'
normalNode=nodes.new('ShaderNodeTexImage');normalNode.image=normal;nodes.active=normalNode
bpy.ops.object.bake(type='NORMAL');normal.filepath_raw=str(out/'forge-masonry-normal.png');normal.file_format='PNG';normal.save()
links.new(texture.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
bpy.ops.wm.save_as_mainfile(filepath=str(out/'roof-forge.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(out/'roof-forge.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
(out/'scope.txt').write_text('New purpose-built geometry prototype. Material colors only; UV/baked textures and native-reference fidelity not yet accepted. Footprint .82 x .68m; height .86m. Not installed.\n')






