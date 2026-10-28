"""Blender authoring prototype: new compact masonry forge, no imported station mesh."""
import bpy, math, random
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012/Forge_v2')
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

