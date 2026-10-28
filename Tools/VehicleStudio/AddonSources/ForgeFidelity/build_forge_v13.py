"""Apply authored stone albedo to actual meshes; preserve diagnostic relief separately."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v12.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v12','ForgeFidelity/v13')
texture=Path(__file__).with_name('Textures')/'stone-albedo-v1.png'
if not texture.exists():raise RuntimeError('Authored texture must be saved in repository first')
extra=f'''
image=bpy.data.images.load({str(texture.as_posix())!r});image.colorspace_settings.name='sRGB';image.pack()
nodes=stone.node_tree.nodes;links=stone.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexImage');tex.name='Authored stone albedo';tex.image=image;tex.projection='BOX';tex.projection_blend=.22;tex.extension='REPEAT'
coords=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=10
links.new(coords.outputs['Object'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
# This image is colour data only. Existing relief remains separately authored; no fake normal map claim.
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
