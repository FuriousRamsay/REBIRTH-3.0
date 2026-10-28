"""Continuous squat envelope, open hearth lip and refractory crucible."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v6.py').read_text()
driver=driver.replace("exec(compile(source,__file__,'exec'))",'pass')
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v6','ForgeFidelity/v7')
extra='''
# Reposition each course along an elliptical envelope; retain the full open mouth.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Masonry_'):
  row=int(obj.name.split('_')[1]);x,y,z=obj.location
  factor=[1,1.025,1.06,1.12,1.26][row]
  obj.location.x*=factor;obj.location.y*=factor
  if row>=3:obj.location.z-=.015*(row-2)
  obj.scale.z*=.90
 if obj.name.startswith('Hollow crucible'):
  obj.data.materials.clear();obj.data.materials.append(stone)
  # Coherent shallow ceramic spalling across outer and inner shell.
  from mathutils import noise
  for vertex in obj.data.vertices:
   p=vertex.co;delta=.0025*noise.noise(p*90)
   p.x+=delta;p.y+=delta*.6
 if obj.name.startswith('Curved horizontal iron cross brace'):
  obj.scale.x*=1.09;obj.scale.y*=1.09
# Front hearth lip with irregular refractory stones and a recessed ember bed.
for i in range(5):
 rock('Front hearth sill stone_%d'%i,(-.12+i*.06,-.215,.075),(.035,.035,.024),stone)
for i in range(48):
 x=random.uniform(-.12,.12);y=random.uniform(-.17,.10)
 obj=rock('Dense hearth charcoal_%d'%i,(x,y,.088+random.uniform(0,.018)),(.012,.016,.009),coal)
 # Hot fissures rather than whole uniformly orange stones.
 if i%3==0:
  for j in range(2):
   a=(x-.009,y+j*.005,.103);b=(x+.006,y+.003+j*.005,.105)
   rod('Coal glowing fissure',a,b,.0018,ember)
# Put riveted retaining plates on the lower upright straps.
for x in [-.195,.195]:
 for z in [.11,.29]:
  cube('Forged retaining plate',(x,-.259,z),(.052,.012,.064),iron,.003)
  bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.012,location=(x,-.272,z))
  bpy.context.object.name='Retaining plate domed rivet';bpy.context.object.scale.y=.6;bpy.context.object.data.materials.append(iron)
# Localised oxidation mask keeps dark metal dominant while exposing worn rusty patches.
nodes=iron.node_tree.nodes;links=iron.node_tree.links;p=nodes.get('Principled BSDF')
previous=p.inputs['Base Color'].links[0].from_socket
rust=nodes.new('ShaderNodeTexNoise');rust.inputs['Scale'].default_value=38;rust.inputs['Detail'].default_value=7
mask=nodes.new('ShaderNodeValToRGB');mask.color_ramp.elements[0].position=.60;mask.color_ramp.elements[1].position=.70
links.new(rust.outputs['Fac'],mask.inputs[0])
mix=nodes.new('ShaderNodeMixRGB');mix.inputs[2].default_value=(.12,.045,.014,1)
links.new(mask.outputs[0],mix.inputs[0]);links.new(previous,mix.inputs[1]);links.new(mix.outputs[0],p.inputs['Base Color'])
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
