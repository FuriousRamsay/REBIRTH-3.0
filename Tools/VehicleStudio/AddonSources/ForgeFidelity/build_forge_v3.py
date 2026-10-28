"""Feature-authored forge candidate; visual gate precedes production export."""
import bpy, math, random, json
from mathutils import Vector
from pathlib import Path
OUT=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v3');OUT.mkdir(parents=True,exist_ok=True)
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
 bpy.ops.mesh.primitive_cube_add(size=2,location=loc);o=bpy.context.object;o.name=name
 for v in o.data.vertices:
  v.co.x*=random.uniform(.80,1.12);v.co.y*=random.uniform(.85,1.10);v.co.z*=random.uniform(.86,1.08)
 o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bevel=o.modifiers.new('Chipped rock profile','BEVEL');bevel.width=min(size)*.22;bevel.segments=2
 bpy.ops.object.modifier_apply(modifier=bevel.name)
 o.rotation_euler=(random.uniform(-.10,.10),random.uniform(-.10,.10),random.uniform(-.10,.10));o.data.materials.append(m);return o
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
  rock('Masonry_%d_%d'%(row,i),(x,y,z),(.053,.045,.045))
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
# Bellows fan: staggered leather ring profiles joined into a real pleated envelope.
verts=[];sections=9;corners=[(-1,-1),(1,-1),(1,1),(-1,1)]
for i in range(sections):
 x=-.30-.19*i/(sections-1);spread=.023+.055*i/(sections-1);factor=1.08 if i%2==0 else .83
 for y,z in corners: verts.append((x,y*spread*factor,.31+z*(.023+.065*i/(sections-1))*factor))
faces=[]
for i in range(sections-1):
 for j in range(4):faces.append((4*i+j,4*i+(j+1)%4,4*(i+1)+(j+1)%4,4*(i+1)+j))
mesh('Fan leather bellows',verts,faces,leather)
for z in [.25,.37]: cube('Bellows wooden cheek',(-.39,0,z),(.25,.17,.027),wood,.014)
for y in [-.105,.105]:cube('Bellows shaped rear timber rail',(-.49,y,.31),(.026,.026,.23),wood,.007)
rod('Bellows nozzle',(-.30,0,.31),(-.235,0,.31),.019,iron)
# Hollow crucible with explicit inner wall and floor.
verts=[];rings=[(.065,.055),(.065,.20),(.049,.20),(.049,.07)]
for r,z in rings:
 for i in range(24):t=2*math.pi*i/24;verts.append((-.19+r*math.cos(t),-.29+r*math.sin(t),z))
faces=[]
for j in range(3):
 for i in range(24):faces.append((24*j+i,24*j+(i+1)%24,24*(j+1)+(i+1)%24,24*(j+1)+i))
mesh('Hollow crucible',verts,faces,iron)
# Controlled inspection scene; no inherited lighting.
world=bpy.data.worlds.new('Neutral inspection world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.045,.045,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;bpy.context.scene.world=world
for name,loc,power,size in [('Key',(-1,-2,2),65,1.2),('Fill',(1,-1,1),22,1),('Rim',(0,1,1.5),35,.8)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((-.1,0,.25))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.05,-1.8,1.05));cam=bpy.context.object;cam.rotation_euler=(Vector((-.09,-.015,.29))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=60;bpy.context.scene.camera=cam
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'forge.blend'));scene.render.filepath=str(OUT/'geometry-review.png');bpy.ops.render.render(write_still=True)
(OUT/'inspection.json').write_text(json.dumps({'status':'GEOMETRY_REVIEW_NOT_ACCEPTED','camera':list(cam.location),'lens':cam.data.lens,'exposure':0,'display':'AgX','objects':len(scene.objects),'maps':'Procedural diagnostic materials only; no final UV bake/export'},indent=2))


