"""Explicit metal UVs and physical bellows wrinkles from the consolidated authoring source."""
from pathlib import Path
source=Path(__file__).with_name('resolved_forge_v16.py').read_text()
source=source.replace('ForgeFidelity/v16','ForgeFidelity/v17')
extra='''
# UV unwrap iron separately; grazing-angle box maps caused stripes on bolt caps.
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH' and iron in list(o.data.materials)]:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
 bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015)
 bpy.ops.object.mode_set(mode='OBJECT')
nodes=iron.node_tree.nodes;links=iron.node_tree.links
tex=nodes.get('Authored iron albedo');tex.projection='FLAT'
coords=nodes.new('ShaderNodeTexCoord');links.new(coords.outputs['UV'],tex.inputs['Vector'])
# Round pleat surfaces and add shallow physical wrinkles, preserving the fan profile.
from mathutils import noise
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Pleated fan leather side')]:
 bpy.context.view_layer.objects.active=obj
 sub=obj.modifiers.new('Rounded folded leather','SUBSURF');sub.levels=3
 bpy.ops.object.modifier_apply(modifier=sub.name)
 for vertex in obj.data.vertices:
  p=obj.matrix_world@vertex.co
  wrinkle=.0012*noise.noise(Vector((p.x*120,p.y*70,p.z*160)))
  vertex.co+=vertex.normal*wrinkle
 for poly in obj.data.polygons:poly.use_smooth=True
# Shorter support feet keep the bellows integrated with the forge base.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Bellows support leg'):obj.location.z=.12;obj.scale.z=.8
 if obj.name.startswith('Bellows anchoring shoe'):obj.location.z=.018
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
