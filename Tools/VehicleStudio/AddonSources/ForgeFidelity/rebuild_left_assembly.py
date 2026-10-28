"""Reference-led replacement of left assembly. Clay geometry, unaccepted."""
import bpy,math,json
from mathutils import Vector
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/LeftAssembly_02')
if out.exists():raise RuntimeError('Preserve earlier evidence')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayGate_v23/clay-baseline.blend')
clay=bpy.data.materials['Geometry review neutral clay']
for o in list(bpy.context.scene.objects):
 if o.name.startswith(('Pleated fan','Fan shaped','Raised leather','Individual leather','Oak operating','Bellows','Operating hinge','Leather')):bpy.data.objects.remove(o,do_unlink=True)
angle=math.radians(25)
def pt(v):
 x,y,z=v;return Vector((-.245+x*math.cos(angle)-y*math.sin(angle),-.065+x*math.sin(angle)+y*math.cos(angle),z))
def finish(o,name):
 o.name='Reference left '+name;o.data.materials.append(clay);return o
def box(name,loc,size,r=.004):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pt(loc));o=finish(bpy.context.object,name);o.dimensions=size;o.rotation_euler.z=angle
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 m=o.modifiers.new('Rounded structural edges','BEVEL');m.width=r;m.segments=3
 return o
def rod(name,a,b,r):
 a,b=pt(a),pt(b);d=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r,depth=d.length,location=(a+b)/2)
 o=finish(bpy.context.object,name);o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 m=o.modifiers.new('Eased end edges','BEVEL');m.width=.002;m.segments=3
 for p in o.data.polygons:p.use_smooth=True
 return o
# Nozzle is a visible assembly rather than a hidden rod: furnace flange,
# thick tube, and rectangular leather compression collar.
rod('furnace flange',(-.009,0,.285),(.009,0,.285),.048)
rod('air tube',(-.085,0,.285),(0,0,.285),.026)
box('compression collar',(-.091,0,.285),(.025,.083,.087),.009)
for y in [-.031,.031]:
 for z in [.255,.315]:rod('collar fastener',(-.102,y,z),(-.113,y,z),.007)
for k in range(8):
 t=k*math.tau/8
 rod('flange fastener',(-.015,.038*math.cos(t),.285+.038*math.sin(t)),(-.024,.038*math.cos(t),.285+.038*math.sin(t)),.005)
# Two stout rear uprights, upper/lower cross shafts, washer and boss stacks.
for y in [-.083,.083]:
 box('timber upright',(-.31,y,.285),(.031,.035,.326),.006)
 box('upright iron strap',(-.329,y,.285),(.006,.024,.28),.002)
 box('frame foot',(-.31,y,.099),(.068,.066,.014),.004)
 box('short foot neck',(-.31,y,.119),(.026,.035,.035),.003)
 for z in [.154,.414]:
  rod('bearing washer',(-.31,y-.024,z),(-.31,y+.024,z),.019)
  rod('bearing domed boss',(-.31,y-.031,z),(-.31,y+.031,z),.011)
for z in [.154,.414]:rod('rear cross shaft',(-.31,-.113,z),(-.31,.113,z),.012)
# Four distinct full-volume leather lobes converge to the compression collar.
# Each lobe has an elliptical closed perimeter, instead of a flat fan sheet.
for k in range(4):
 verts=[];faces=[];nu=32;nv=32
 for i in range(nu+1):
  t=i/nu;x=-.30+.196*t
  center=.285+(k-1.5)*.060*(1-t)
  halfheight=.025*(1-t)+.008*t
  width=.071*(1-t)+.030*t+.009*math.sin(math.pi*t)
  for j in range(nv):
   a=j*math.tau/nv;verts.append(tuple(pt((x,width*math.cos(a),center+halfheight*math.sin(a)))))
 for i in range(nu):
  for j in range(nv):faces.append((i*nv+j,i*nv+(j+1)%nv,(i+1)*nv+(j+1)%nv,(i+1)*nv+j))
 faces.extend([tuple(reversed(range(nv))),tuple(nu*nv+j for j in range(nv))])
 mesh=bpy.data.meshes.new('Closed rounded lobe');mesh.from_pydata(verts,[],faces);mesh.update()
 o=bpy.data.objects.new('Reference left leather lobe',mesh);bpy.context.collection.objects.link(o);mesh.materials.append(clay)
 for p in mesh.polygons:p.use_smooth=True
scene=bpy.context.scene;scene.render.filepath=str(out/'clay-left-assembly.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge-clay.blend'))
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'UNACCEPTED_LEFT_ASSEMBLY_REBUILD','orientationDegrees':25,'dimensions':'Authored estimates from single reference, not measured','added':['furnace flange and bolts','visible nozzle','compression collar','frame bearing stacks','four volumetric folds'],'remaining':'Inspect proportions and intersections against reference; rest of forge still fails'},indent=2))

