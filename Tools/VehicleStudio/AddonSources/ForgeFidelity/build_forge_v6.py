"""Fieldstone fracture planes, differentiated materials and missing fittings."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v5.py').read_text().replace('ForgeFidelity/v5','ForgeFidelity/v6')
driver=driver.replace("exec(compile(source, __file__, 'exec'))", "pass")
exec(compile(driver,__file__,'exec'))
source=source.replace('o.scale=size;bpy.ops.object.transform_apply', '''# Clip the deformed sphere against irregular fracture planes.
 for axis in range(3):
  low=random.uniform(-.91,-.72);high=random.uniform(.72,.91)
  for vertex in o.data.vertices:vertex.co[axis]=max(low,min(high,vertex.co[axis]))
 o.scale=size;bpy.ops.object.transform_apply''')
source=source.replace('poly.use_smooth=True','poly.use_smooth=False',1)
extra='''
# Cross-bracing follows the dome surface as a curved strip, with attached domed rivets.
for z,rx,ry in [(.42,.215,.191),(.29,.277,.231)]:
 verts=[];segments=40
 for j in range(segments+1):
  t=math.pi+math.pi*j/segments
  # Lower band leaves the hearth opening unobstructed.
  for h in [-.016,.016]:verts.append((rx*math.cos(t),ry*math.sin(t),z+h))
 faces=[]
 for j in range(segments):
  t=math.pi+math.pi*(j+.5)/segments
  if z<.3 and abs(rx*math.cos(t))<.17:continue
  faces.append((2*j,2*j+1,2*j+3,2*j+2))
 obj=mesh('Curved horizontal iron cross brace',verts,faces,iron)
 mod=obj.modifiers.new('Cross brace thickness','SOLIDIFY');mod.thickness=.009
 for j in range(1,segments,6):
  t=math.pi+math.pi*j/segments;x=rx*math.cos(t);y=ry*math.sin(t)
  if z<.3 and abs(x)<.17:continue
  bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.009,location=(x,y-.007,z))
  bpy.context.object.name='Domed brace rivet';bpy.context.object.scale=(1,.6,1);bpy.context.object.data.materials.append(iron)
# Crucible bands and bracket make the cup an attached part, not a loose cylinder.
for z in [.075,.177]:
 bpy.ops.mesh.primitive_torus_add(major_radius=.065,minor_radius=.006,major_segments=32,minor_segments=8,location=(-.19,-.29,z))
 bpy.context.object.name='Crucible retaining hoop';bpy.context.object.data.materials.append(iron)
for x in [-.245,-.135]:
 cube('Crucible vertical retaining bracket',(x,-.321,.12),(.014,.017,.15),iron,.002)
 for z in [.075,.177]:rod('Crucible bracket rivet',(x,-.33,z),(x,-.34,z),.006,iron)
# Independent broad wear, mineral flecks and fine relief; avoid a uniform grey noise wash.
for material in [stone,iron,wood,leather]:
 nodes=material.node_tree.nodes;links=material.node_tree.links;p=nodes.get('Principled BSDF')
 tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=9;tex.inputs['Detail'].default_value=5
 coords=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
 mapping.inputs[1].default_value=(3,3,65) if material==wood else (1,1,1)
 links.new(coords.outputs['Generated'],mapping.inputs[0]);links.new(mapping.outputs[0],tex.inputs['Vector'])
 ramp=nodes.new('ShaderNodeValToRGB')
 colors={stone:((.009,.008,.007,1),(.09,.076,.052,1)),iron:((.008,.011,.014,1),(.035,.028,.018,1)),wood:((.025,.008,.002,1),(.12,.05,.016,1)),leather:((.012,.004,.002,1),(.065,.023,.009,1))}
 a,b=colors[material];ramp.color_ramp.elements[0].color=a;ramp.color_ramp.elements[0].position=.22;ramp.color_ramp.elements[1].color=b;ramp.color_ramp.elements[1].position=.8
 links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
 if material==stone:
  grain=nodes.new('ShaderNodeTexVoronoi');grain.inputs['Scale'].default_value=55
  fleck=nodes.new('ShaderNodeValToRGB');fleck.color_ramp.elements[0].position=.035;fleck.color_ramp.elements[0].color=(.21,.19,.15,1);fleck.color_ramp.elements[1].position=.13;fleck.color_ramp.elements[1].color=(.015,.012,.009,1)
  links.new(grain.outputs['Distance'],fleck.inputs[0])
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='ADD';mix.inputs[0].default_value=.22;links.new(ramp.outputs[0],mix.inputs[1]);links.new(fleck.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],p.inputs['Base Color'])
 if material==iron:p.inputs['Metallic'].default_value=.75;p.inputs['Roughness'].default_value=.48
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
