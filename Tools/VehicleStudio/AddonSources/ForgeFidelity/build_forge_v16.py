"""Authored worn-iron albedo on the actual forge, with preserved Blender proof."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v15.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v15','ForgeFidelity/v16')
texture=Path(__file__).with_name('Textures')/'iron-albedo-v1.png'
if not texture.exists():raise RuntimeError('Authored iron texture must be preserved first')
extra=f'''
image=bpy.data.images.load({texture.as_posix()!r});image.colorspace_settings.name='sRGB';image.pack()
nodes=iron.node_tree.nodes;links=iron.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexImage');tex.name='Authored iron albedo';tex.image=image;tex.projection='BOX';tex.projection_blend=.15
bpy.ops.object.empty_add(type='PLAIN_AXES',location=(0,0,0));frame=bpy.context.object;frame.name='Iron material mapping frame'
coords=nodes.new('ShaderNodeTexCoord');coords.object=frame;scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=5
links.new(coords.outputs['Object'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
# Colour image is not a normal map. Existing pitting remains a separate relief input.
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
# Preserve the fully expanded source rather than relying only on nested revision drivers.
Path(__file__).with_name('resolved_forge_v16.py').write_text(source)
exec(compile(source,__file__,'exec'))
