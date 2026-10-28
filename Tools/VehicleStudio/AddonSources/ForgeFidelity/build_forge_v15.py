"""Explicit stone UVs and irregular subdued thermal cracks."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v14.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v14','ForgeFidelity/v15')
extra='''
# Explicit per-stone UV projection removes the grazing-angle box projection streaks.
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH' and stone in list(o.data.materials)]:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
 bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.018)
 bpy.ops.object.mode_set(mode='OBJECT')
nodes=stone.node_tree.nodes;links=stone.node_tree.links
image_node=nodes.get('Authored stone albedo');image_node.projection='FLAT'
uv=nodes.new('ShaderNodeTexCoord');links.new(uv.outputs['UV'],image_node.inputs['Vector'])
# Distorted crack field: coal crust remains dark with sparse warm fissures.
nodes=ember.node_tree.nodes;links=ember.node_tree.links
crack=next(n for n in nodes if n.type=='TEX_VORONOI')
coords=nodes.new('ShaderNodeTexCoord');distort=nodes.new('ShaderNodeTexNoise');distort.inputs['Scale'].default_value=5;distort.inputs['Detail'].default_value=4
links.new(coords.outputs['Generated'],distort.inputs['Vector'])
scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=.28
links.new(distort.outputs['Color'],scale.inputs[0])
add=nodes.new('ShaderNodeVectorMath');add.operation='ADD';links.new(coords.outputs['Generated'],add.inputs[0]);links.new(scale.outputs[0],add.inputs[1]);links.new(add.outputs[0],crack.inputs['Vector'])
crack.inputs['Scale'].default_value=4.5
for node in nodes:
 if node.type=='MATH' and node.operation=='MULTIPLY':node.inputs[1].default_value=2.1
# Fine charcoal albedo variation on the remaining non-emissive coal pieces.
nodes=coal.node_tree.nodes;links=coal.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=32;tex.inputs['Detail'].default_value=6
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.003,.003,.002,1);ramp.color_ramp.elements[1].color=(.045,.039,.030,1)
links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.6;bump.inputs['Distance'].default_value=.0015
links.new(tex.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
