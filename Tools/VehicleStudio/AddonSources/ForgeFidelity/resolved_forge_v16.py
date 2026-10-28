"""Feature-authored forge candidate; visual gate precedes production export."""
import bpy, math, random, json
from mathutils import Vector
from pathlib import Path
OUT=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v16');OUT.mkdir(parents=True,exist_ok=True)
if (OUT/'forge.blend').exists(): raise RuntimeError('Preserve candidate; use new version')
random.seed(31);bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mat(name,color,metal=0,rough=.8):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 return m
stone=mat('Rough dark fieldstone',(.16,.15,.13));iron=mat('Pitted forged iron',(.075,.065,.05),.8,.6);wood=mat('Worn oak rails',(.15,.075,.035));leather=mat('Folded stitched leather',(.09,.035,.014));black=mat('Recessed soot',(.018,.014,.012));coal=mat('Charred coal',(.035,.025,.02))
for m,scale,strength in [(stone,75,.32),(iron,160,.17),(wood,35,.12),(leather,120,.16)]:
 n=m.node_tree.nodes;links=m.node_tree.links;noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=scale;noise.inputs['Detail'].default_value=4
 bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=strength;bump.inputs['Distance'].default_value=.003;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],n.get('Principled BSDF').inputs['Normal'])
def cube(name,loc,size,m,bevel=.006):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m)
 if bevel: mod=o.modifiers.new('Authored edge easing','BEVEL');mod.width=bevel;mod.segments=2;o.modifiers.new('Planar normals','WEIGHTED_NORMAL')
 return o
def rock(name,loc,size,m=stone):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1,location=loc)
 o=bpy.context.object;o.name=name
 # Coherent low-frequency deformation makes chipped fieldstone rather than independent noisy vertices.
 seed=Vector((random.uniform(0,10),random.uniform(0,10),random.uniform(0,10)))
 from mathutils import noise
 for v in o.data.vertices:
  p=v.co.copy(); fac=1+.22*noise.noise_vector(p*2.4+seed).x+.08*noise.noise(p*7+seed)
  v.co=p*fac
 # Clip the deformed sphere against irregular fracture planes.
 for axis in range(3):
  low=random.uniform(-.91,-.72);high=random.uniform(.72,.91)
  for vertex in o.data.vertices:vertex.co[axis]=max(low,min(high,vertex.co[axis]))
 o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.rotation_euler=(random.uniform(-.35,.35),random.uniform(-.35,.35),random.uniform(-.35,.35))
 o.data.materials.append(m)
 for poly in o.data.polygons:poly.use_smooth=False
 return o
def rod(name,a,b,r,m):
 a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=(b-a).length,location=(a+b)/2);o=bpy.context.object;o.name=name;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();o.data.materials.append(m);return o
def mesh(name,verts,faces,m):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.data.materials.append(m);return o
# Front is -Y; individual angular rocks preserve silhouette and actual cavity.
cube('Hearth footing',(0,0,.045),(.53,.43,.09),black)
for row,(z,rx,ry) in enumerate([( .13,.235,.18),(.22,.235,.18),(.31,.22,.175),(.40,.185,.15),(.48,.125,.10)]):
 count=14 if row<3 else 11
 for i in range(count):
  t=2*math.pi*(i+(row%2)*.5)/count;x=rx*math.cos(t);y=ry*math.sin(t)
  if y<-.10 and abs(x)<.145 and z<.39: continue
  rock('Masonry_%d_%d'%(row,i),(x,y,z),(.061,.054,.052))
# Arch voussoirs, explicit inner ring and dark back wall.
for i in range(9):
 t=math.pi*i/8;o=rock('Arch wedge_%d'%i,(.145*math.cos(t),-.17,.26+.145*math.sin(t)),(.044,.052,.050));o.rotation_euler[1]=t-math.pi/2
cube('Inner soot wall',(0,.14,.23),(.30,.02,.30),black)
for i in range(17): rock('Coal_%d'%i,(random.uniform(-.105,.105),random.uniform(-.13,.08),.10),(.022,.018,.016),coal)
# Horizontal strap around lower assembly, vertical front strips, bent dome ribs.
for y in [-.225,.225]: cube('Lower iron strap',(0,y,.09),(.52,.026,.037),iron)
for x in [-.26,.26]: cube('Side iron strap',(x,0,.09),(.026,.45,.037),iron)
for x in [-.195,.195]:
 cube('Front vertical strap',(x,-.245,.23),(.033,.018,.33),iron)
 for z in [.09,.20,.34]:rod('Front strap rivet',(x,-.256,z),(x,-.272,z),.009,iron)
for angle in [0,math.pi/2,math.pi]:
 verts=[]
 for j in range(10):
  t=.08+(math.pi/2-.08)*j/9;r=.305*math.cos(t);z=.09+.45*math.sin(t)
  for side in [-1,1]:verts.append((r*math.cos(angle)-side*.017*math.sin(angle),r*.82*math.sin(angle)+side*.017*math.cos(angle),z))
 rib=mesh('Bent dome iron rib',verts,[(2*j,2*j+1,2*j+3,2*j+2) for j in range(9)],iron);mod=rib.modifiers.new('Strip thickness','SOLIDIFY');mod.thickness=.013
for x in [-.245,.245]:
 for y in [-.20,.20]:
  foot=cube('Securing foot',(x,y,.018),(.09,.075,.024),iron)
  rod('Foot bolt',(x,y,.03),(x,y,.044),.012,iron)
# Annular top vent, not a capped primitive.
bpy.ops.mesh.primitive_torus_add(major_radius=.065,minor_radius=.017,major_segments=32,minor_segments=8,location=(0,0,.55));bpy.context.object.name='Open iron exhaust collar';bpy.context.object.data.materials.append(iron)
# Hinged leather fan, narrow throat against masonry and broad rear operating frame.
profile=[(-.28,.026),(-.34,.050),(-.42,.083),(-.50,.100),(-.55,.085)]
def cheek(name,upper):
 verts=[]
 for x,w in profile:
  z=.31+(abs(x+.28)*.32 if upper else -abs(x+.28)*.32)
  for y in [-w,w]:verts.append((x,y,z))
 obj=mesh(name,verts,[(2*j,2*j+1,2*j+3,2*j+2) for j in range(len(profile)-1)],wood)
 mod=obj.modifiers.new('Wood board thickness','SOLIDIFY');mod.thickness=.018
 bevel=obj.modifiers.new('Rounded worn board edges','BEVEL');bevel.width=.004;bevel.segments=3
 return obj
cheek('Fan shaped upper oak board',True);cheek('Fan shaped lower oak board',False)
for side in [-1,1]:
 verts=[];rows=7
 for row in range(rows):
  t=row/(rows-1)
  for x,w in profile:
   h=abs(x+.28)*.32
   y=side*(w+(.009 if row%2==0 else -.004))
   verts.append((x,y,.31-h+2*h*t))
 faces=[]
 for row in range(rows-1):
  for j in range(len(profile)-1):faces.append((row*5+j,row*5+j+1,(row+1)*5+j+1,(row+1)*5+j))
 obj=mesh('Pleated fan leather side',verts,faces,leather)
 for poly in obj.data.polygons:poly.use_smooth=True
 for row in [0,2,4,6]:
  t=row/(rows-1)
  for j in range(4):
   x,w=profile[j];x2,w2=profile[j+1]
   a=(x,side*(w+.010),.31-abs(x+.28)*.32+2*abs(x+.28)*.32*t)
   b=(x2,side*(w2+.010),.31-abs(x2+.28)*.32+2*abs(x2+.28)*.32*t)
   rod('Raised leather fold seam',a,b,.0025,leather)
for y in [-.108,.108]:
 cube('Oak operating upright',(-.55,y,.31),(.027,.028,.25),wood,.005)
 for z in [.20,.42]:
  rod('Bellows rear cross shaft',(-.57,-.13,z),(-.57,.13,z),.012,iron)
  rod('Operating hinge bolt',(-.55,y-.015,z),(-.55,y+.015,z),.011,iron)
rod('Bellows throat nozzle',(-.30,0,.31),(-.235,0,.31),.022,iron)
# Hollow crucible with explicit inner wall and floor.
verts=[];rings=[(.065,.055),(.065,.20),(.049,.20),(.049,.07)]
for r,z in rings:
 for i in range(24):t=2*math.pi*i/24;verts.append((-.19+r*math.cos(t),-.29+r*math.sin(t),z))
faces=[]
for j in range(3):
 for i in range(24):faces.append((24*j+i,24*j+(i+1)%24,24*(j+1)+(i+1)%24,24*(j+1)+i))
mesh('Hollow crucible',verts,faces,iron)
# Straps explicitly follow the stepped masonry envelope, instead of a guessed dome curve.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Bent dome iron rib'): bpy.data.objects.remove(obj,do_unlink=True)
for side in [-1,1]:
 points=[(side*.19,-.24,.10),(side*.19,-.235,.30),(side*.14,-.195,.42),(side*.09,-.13,.51),(side*.055,-.05,.55)]
 verts=[]
 for x,y,z in points:
  for w in [-.018,.018]: verts.append((x+w,y,z))
 rib=mesh('Profile fitted front strap',verts,[(2*j,2*j+1,2*j+3,2*j+2) for j in range(len(points)-1)],iron)
 thick=rib.modifiers.new('Strap thickness','SOLIDIFY');thick.thickness=.016
 for x,y,z in points[1:]:rod('Dome rivet',(x,y-.008,z),(x,y-.025,z),.009,iron)
# Colour and relief are authored by material, never baked camera highlights.
for material,c0,c1,scale in [(stone,(.025,.023,.02,1),(.20,.18,.14,1),24),(iron,(.018,.018,.016,1),(.16,.075,.025,1),45),(leather,(.025,.009,.003,1),(.12,.045,.015,1),60)]:
 nodes=material.node_tree.nodes;links=material.node_tree.links
 tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=scale;tex.inputs['Detail'].default_value=6;tex.inputs['Roughness'].default_value=.8
 ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.25;ramp.color_ramp.elements[0].color=c0;ramp.color_ramp.elements[1].position=.75;ramp.color_ramp.elements[1].color=c1
 links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],nodes.get('Principled BSDF').inputs['Base Color'])
# Warm coal detail stays separate from reusable masonry albedo.
ember=mat('Localized ember cracks',(.18,.035,.004),0,.9)
p=ember.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Color'].default_value=(1,.13,.008,1);p.inputs['Emission Strength'].default_value=2
for obj in bpy.context.scene.objects:
 if obj.name.startswith('Coal_') and random.random()<.35: obj.data.materials.clear();obj.data.materials.append(ember)
bpy.ops.object.light_add(type='POINT',location=(0,-.04,.20));bpy.context.object.data.energy=1.2;bpy.context.object.data.color=(1,.18,.025);bpy.context.object.data.shadow_soft_size=.06

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


# Close the crown around the vent without capping the vent opening.
for i in range(12):
 t=2*math.pi*i/12
 rock('Crown infill stone_%d'%i,(.10*math.cos(t),.082*math.sin(t),.505),(.033,.032,.027),stone)
bpy.context.view_layer.update()
stones=[o for o in bpy.context.scene.objects if o.name.startswith(('Masonry_','Arch wedge','Crown infill'))]
deps=bpy.context.evaluated_depsgraph_get()
def surface_y(x,z):
 best=None
 for original in stones:
  obj=original.evaluated_get(deps);inverse=obj.matrix_world.inverted()
  origin=inverse@Vector((x,-1,z));direction=(inverse.to_3x3()@Vector((0,1,0))).normalized()
  hit,point,normal,index=obj.ray_cast(origin,direction)
  if hit:
   world=obj.matrix_world@point
   if best is None or world.y<best:best=world.y
 return best

# Smooth ellipse fitted outside each course. Cross braces must remain manufactured iron strips.
bands=[o for o in bpy.context.scene.objects if o.name.startswith('Curved horizontal iron cross brace')]
for obj in bands:
 obj.scale=(1,1,1)
 z=sum(v.co.z for v in obj.data.vertices)/len(obj.data.vertices)
 rx,ry=(.245,.208) if z>.35 else (.287,.225)
 for j in range(41):
  t=math.pi+math.pi*j/40
  for k,h in enumerate([-.016,.016]):obj.data.vertices[2*j+k].co=(rx*math.cos(t),ry*math.sin(t),z+h)
 for poly in obj.data.polygons:poly.use_smooth=True
 bevel=obj.modifiers.new('Rounded forged strip edges','BEVEL');bevel.width=.002;bevel.segments=3
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Domed brace rivet'):
  x,y,z=obj.location;rx,ry=(.245,.208) if z>.35 else (.287,.225)
  t=math.atan2(y/ry,x/rx);obj.location=(rx*math.cos(t),ry*math.sin(t)-.007,z)
# Smooth front ribs retain the stepped reference profile while eliminating hard folds.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Profile fitted front strap'):
  bevel=obj.modifiers.new('Rib softened edges','BEVEL');bevel.width=.002;bevel.segments=3
  for poly in obj.data.polygons:poly.use_smooth=True

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


thread=mat('Waxed flax stitches',(.13,.085,.045),0,.9)
# Sewn seams follow all four raised fold edges, with separate short stitches.
for side in [-1,1]:
 for row in [0,2,4,6]:
  t=row/6
  for j in range(4):
   x,w=profile[j];x2,w2=profile[j+1]
   for k in range(7):
    f=(k+.3)/7;px=x+(x2-x)*f;pw=w+(w2-w)*f
    pz=.31-abs(px+.28)*.32+2*abs(px+.28)*.32*t
    rod('Individual leather seam stitch',(px,side*(pw+.013),pz-.0015),(px+.0025,side*(pw+.013),pz+.0015),.00065,thread)
# Directional wood grain is physically aligned to each board's long X axis.
nodes=wood.node_tree.nodes;links=wood.node_tree.links;p=nodes.get('Principled BSDF')
coords=nodes.new('ShaderNodeTexCoord');stretch=nodes.new('ShaderNodeVectorMath');stretch.operation='MULTIPLY';stretch.inputs[1].default_value=(3,120,18)
grain=nodes.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=1;grain.inputs['Detail'].default_value=5
links.new(coords.outputs['Generated'],stretch.inputs[0]);links.new(stretch.outputs[0],grain.inputs['Vector'])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.012,.004,.001,1);ramp.color_ramp.elements[1].color=(.14,.065,.023,1)
links.new(grain.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.001;bump.inputs['Strength'].default_value=.5
links.new(grain.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
# Fine leather pores with sparse creases, separate from wood.
nodes=leather.node_tree.nodes;links=leather.node_tree.links;p=nodes.get('Principled BSDF')
pores=nodes.new('ShaderNodeTexVoronoi');pores.inputs['Scale'].default_value=180
bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0007;bump.inputs['Strength'].default_value=.45
links.new(pores.outputs['Distance'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
p.inputs['Roughness'].default_value=.58
# Bright coals have a charred crust; emission is restricted to irregular cracks.
nodes=ember.node_tree.nodes;links=ember.node_tree.links;p=nodes.get('Principled BSDF')
crack=nodes.new('ShaderNodeTexVoronoi');crack.feature='DISTANCE_TO_EDGE';crack.inputs['Scale'].default_value=8
mask=nodes.new('ShaderNodeValToRGB');mask.color_ramp.elements[0].position=.035;mask.color_ramp.elements[0].color=(1,1,1,1);mask.color_ramp.elements[1].position=.075;mask.color_ramp.elements[1].color=(0,0,0,1)
links.new(crack.outputs['Distance'],mask.inputs[0]);power=nodes.new('ShaderNodeMath');power.operation='MULTIPLY';power.inputs[1].default_value=5
links.new(mask.outputs[0],power.inputs[0]);links.new(power.outputs[0],p.inputs['Emission Strength']);p.inputs['Base Color'].default_value=(.008,.005,.003,1)
# Exposed iron edges use geometry curvature as a separate wear signal.
nodes=iron.node_tree.nodes;links=iron.node_tree.links;p=nodes.get('Principled BSDF')
geometry=nodes.new('ShaderNodeNewGeometry');edge=nodes.new('ShaderNodeValToRGB')
edge.color_ramp.elements[0].position=.54;edge.color_ramp.elements[0].color=(0,0,0,1);edge.color_ramp.elements[1].position=.65;edge.color_ramp.elements[1].color=(.12,.12,.12,1)
links.new(geometry.outputs['Pointiness'],edge.inputs[0]);previous=p.inputs['Base Color'].links[0].from_socket
mix=nodes.new('ShaderNodeMixRGB');mix.inputs[2].default_value=(.075,.063,.05,1);links.new(edge.outputs[0],mix.inputs[0]);links.new(previous,mix.inputs[1]);links.new(mix.outputs[0],p.inputs['Base Color'])


image=bpy.data.images.load('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/VehicleStudio/AddonSources/ForgeFidelity/Textures/stone-albedo-v1.png');image.colorspace_settings.name='sRGB';image.pack()
nodes=stone.node_tree.nodes;links=stone.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexImage');tex.name='Authored stone albedo';tex.image=image;tex.projection='BOX';tex.projection_blend=.22;tex.extension='REPEAT'
coords=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=3
links.new(coords.outputs['Object'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
# This image is colour data only. Existing relief remains separately authored; no fake normal map claim.


# All stones share a fixed physical mapping frame rather than repeating local object coordinates.
bpy.ops.object.empty_add(type='PLAIN_AXES',location=(0,0,0));mapping_frame=bpy.context.object;mapping_frame.name='Stone material mapping frame'
for node in stone.node_tree.nodes:
 if node.type=='TEX_COORD':node.object=mapping_frame
nodes=stone.node_tree.nodes;links=stone.node_tree.links;p=nodes.get('Principled BSDF')
previous=p.inputs['Base Color'].links[0].from_socket
info=nodes.new('ShaderNodeObjectInfo');variation=nodes.new('ShaderNodeValToRGB')
variation.color_ramp.elements[0].color=(.45,.41,.36,1);variation.color_ramp.elements[1].color=(1,.95,.87,1)
links.new(info.outputs['Random'],variation.inputs[0]);mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.55
links.new(previous,mix.inputs[1]);links.new(variation.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],p.inputs['Base Color'])
# Rear hinge plates, operating pins, and a secured nozzle flange.
for y in [-.108,.108]:
 cube('Bellows iron hinge cheek',(-.566,y,.31),(.008,.034,.245),iron,.003)
 for z in [.20,.31,.42]:
  rod('Bellows hinge pin',(-.58,y,z),(-.59,y,z),.010,iron)
 cube('Bellows support leg',(-.53,y,.135),(.026,.028,.26),iron,.003)
 cube('Bellows anchoring shoe',(-.53,y,.012),(.07,.06,.02),iron,.003)
cube('Bellows throat rectangular collar',(-.30,0,.31),(.026,.085,.10),iron,.005)
for y in [-.033,.033]:
 for z in [.274,.346]:rod('Throat flange fastener',(-.314,y,z),(-.324,y,z),.0055,iron)
# Bolt holes are real openings in each foot rather than painted dots.
for foot in [o for o in bpy.context.scene.objects if o.name.startswith('Securing foot')]:
 x,y,z=foot.location;hx=x+(.027 if x>0 else -.027)
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.009,depth=.10,location=(hx,y,z))
 cutter=bpy.context.object;bpy.context.view_layer.objects.active=foot
 mod=foot.modifiers.new('Actual mounting hole','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)


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


image=bpy.data.images.load('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/VehicleStudio/AddonSources/ForgeFidelity/Textures/iron-albedo-v1.png');image.colorspace_settings.name='sRGB';image.pack()
nodes=iron.node_tree.nodes;links=iron.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexImage');tex.name='Authored iron albedo';tex.image=image;tex.projection='BOX';tex.projection_blend=.15
bpy.ops.object.empty_add(type='PLAIN_AXES',location=(0,0,0));frame=bpy.context.object;frame.name='Iron material mapping frame'
coords=nodes.new('ShaderNodeTexCoord');coords.object=frame;scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=5
links.new(coords.outputs['Object'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
# Colour image is not a normal map. Existing pitting remains a separate relief input.

# Controlled inspection scene; no inherited lighting.
world=bpy.data.worlds.new('Neutral inspection world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.045,.045,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;bpy.context.scene.world=world
for name,loc,power,size in [('Key',(-1,-2,2),65,1.2),('Fill',(1,-1,1),22,1),('Rim',(0,1,1.5),35,.8)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((-.1,0,.25))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-1.05,-1.8,1.05));cam=bpy.context.object;cam.rotation_euler=(Vector((-.09,-.015,.29))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=60;bpy.context.scene.camera=cam
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'forge.blend'));scene.render.filepath=str(OUT/'geometry-review.png');bpy.ops.render.render(write_still=True)
(OUT/'inspection.json').write_text(json.dumps({'status':'GEOMETRY_REVIEW_NOT_ACCEPTED','camera':list(cam.location),'lens':cam.data.lens,'exposure':0,'display':'AgX','objects':len(scene.objects),'maps':'Procedural diagnostic materials only; no final UV bake/export'},indent=2))




# Close views render the same authored scene; never image-generated model proof.
for label,location,target in [
 ('bellows-detail',(-.95,-.82,.62),(-.41,-.01,.30)),
 ('hearth-detail',(-.55,-1.05,.64),(0,-.10,.25))]:
 cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.data.lens=70;scene.render.resolution_x=1100;scene.render.resolution_y=900
 scene.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
