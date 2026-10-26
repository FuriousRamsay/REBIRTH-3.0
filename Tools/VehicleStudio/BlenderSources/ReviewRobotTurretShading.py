"""Compare source-map and rebaked tangent normals on the approved 40k mesh."""
import bpy
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
bpy.ops.wm.open_mainfile(filepath=str(P/'RustyRobotTurret_Optimized.blend'))
sc=bpy.context.scene;source=bpy.data.objects['SOURCE_283376'];source.hide_set(False);source.hide_render=False
low=[o for o in sc.objects if o.type=='MESH' and o.name.endswith('LOD0')]
material=low[0].data.materials[0];normal_node=next(n for n in material.node_tree.nodes if n.type=='NORMAL_MAP')
original_node=next(n for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and 'Normal' in n.image.name)
original=original_node.image
target=bpy.data.images.new('Robot_Normal_Rebaked',width=1024,height=1024,alpha=False);target.colorspace_settings.name='Non-Color';target.generated_color=(.5,.5,1,1)
bake_node=material.node_tree.nodes.new('ShaderNodeTexImage');bake_node.image=target;material.node_tree.nodes.active=bake_node
caps=low[0].data.materials[1];unused=bpy.data.images.new('Unused caps',width=16,height=16)
capnode=caps.node_tree.nodes.new('ShaderNodeTexImage');capnode.image=unused;caps.node_tree.nodes.active=capnode
sc.render.bake.use_selected_to_active=True;sc.render.bake.use_clear=False;sc.render.bake.margin=4;sc.render.bake.cage_extrusion=.003;sc.render.bake.max_ray_distance=.008
for o in low:
    bpy.ops.object.select_all(action='DESELECT');source.select_set(True);o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.bake(type='NORMAL')
pixels=list(target.pixels)
for i in range(0,len(pixels),4):
    if max(pixels[i:i+3])<.02:pixels[i:i+3]=[.5,.5,1]
target.pixels=pixels;target.filepath_raw=str(P/'Robot_Normal_Rebaked.png');target.file_format='PNG';target.save()
source.hide_render=True;source.hide_set(True)
cam=sc.camera;cam.data.ortho_scale=1.25;cam.location=(-2,-3,1.8);cam.rotation_euler=(Vector((0,0,.44))-cam.location).to_track_quat('-Z','Y').to_euler();sc.cycles.samples=32
for name,im,strength in [('shading-rebaked',target,1),('shading-no-normal',original,0)]:
    original_node.image=im;normal_node.inputs['Strength'].default_value=strength
    sc.render.filepath=str(P/(name+'.png'));bpy.ops.render.render(write_still=True)
original_node.image=target;normal_node.inputs['Strength'].default_value=1
bpy.ops.wm.save_as_mainfile(filepath=str(P/'RobotTurret_ShadingCandidate.blend'))
