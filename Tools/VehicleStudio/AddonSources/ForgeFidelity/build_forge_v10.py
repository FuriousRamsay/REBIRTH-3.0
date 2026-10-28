"""Correct retained band scale and add high-resolution chipped stone relief."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v9.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v9','ForgeFidelity/v10')
source=source.replace("rx,ry=(.279,.252) if z>.35 else (.318,.273)","rx,ry=(.245,.208) if z>.35 else (.287,.225)")
source=source.replace('z=sum(v.co.z for v in obj.data.vertices)/len(obj.data.vertices)','obj.scale=(1,1,1)\n z=sum(v.co.z for v in obj.data.vertices)/len(obj.data.vertices)')
extra='''
from mathutils import noise
for obj in list(bpy.context.scene.objects):
 if not obj.name.startswith(('Masonry_','Arch wedge','Crown infill','Front hearth sill')):continue
 bpy.context.view_layer.objects.active=obj
 sub=obj.modifiers.new('Stone surface subdivision','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=2
 bpy.ops.object.modifier_apply(modifier=sub.name)
 for vertex in obj.data.vertices:
  p=obj.matrix_world@vertex.co
  relief=.0018*noise.noise(p*180)+.0009*noise.noise(p*470)
  vertex.co+=vertex.normal*relief
 for poly in obj.data.polygons:poly.use_smooth=True
# Fine mineral fracture relief is independent of the broad albedo variation.
nodes=stone.node_tree.nodes;links=stone.node_tree.links;p=nodes.get('Principled BSDF')
coords=nodes.new('ShaderNodeTexCoord');grain=nodes.new('ShaderNodeTexNoise')
grain.inputs['Scale'].default_value=220;grain.inputs['Detail'].default_value=8;grain.inputs['Roughness'].default_value=.8
links.new(coords.outputs['Object'],grain.inputs['Vector'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.7;bump.inputs['Distance'].default_value=.002
links.new(grain.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
