"""Blender authoring prototype: new compact masonry forge, no imported station mesh."""
import bpy, math, random
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012/Forge_v1')
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
cube('Back wall',(0,.22,.32),(.70,.16,.42),stone)
for side in [-1,1]:
    for row in range(3):
        cube('Masonry side', (side*.29,0,.17+row*.115),(.16,.52,.11),stone,.018)
for i in range(7):
    angle=math.pi*i/6
    o=cube('Arched hearth stone',(.235*math.cos(angle),-.20,.33+.20*math.sin(angle)),(.13,.19,.13),stone,.02)
    o.rotation_euler[1]=angle-math.pi/2
cube('Hearth shadow',(0,.13,.24),(.39,.025,.23),dark,0)
cube('Ash tray',(0,-.07,.165),(.39,.36,.025),ash)
cube('Upper masonry',(0,.065,.53),(.56,.40,.12),stone,.025)
bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.07,depth=.28,location=(.12,.14,.72))
bpy.context.object.name='Short iron flue';bpy.context.object.data.materials.append(steel)
for x in [-.35,.35]:
    cube('Securing strap',(x,0,.12),(.025,.64,.025),steel,.004)
    for y in [-.27,.27]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.018,depth=.018,location=(x,y,.085))
        bpy.context.object.name='Mount bolt';bpy.context.object.data.materials.append(steel)
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        bpy.ops.object.convert(target='MESH');obj.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'roof-forge.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(out/'roof-forge.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
(out/'scope.txt').write_text('New purpose-built geometry prototype. Material colors only; UV/baked textures and native-reference fidelity not yet accepted. Footprint .82 x .68m; height .86m. Not installed.\n')
