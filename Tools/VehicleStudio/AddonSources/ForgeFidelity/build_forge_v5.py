"""Shape revision: irregular fieldstone and hinged fan bellows, not square blocks."""
from pathlib import Path
source = Path(__file__).with_name('build_forge_v4.py').read_text()
source = source.replace('ForgeFidelity/v4', 'ForgeFidelity/v5')
start = source.index('def rock(')
end = source.index('def rod(', start)
source = source[:start] + '''def rock(name,loc,size,m=stone):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1,location=loc)
 o=bpy.context.object;o.name=name
 # Coherent low-frequency deformation makes chipped fieldstone rather than independent noisy vertices.
 seed=Vector((random.uniform(0,10),random.uniform(0,10),random.uniform(0,10)))
 from mathutils import noise
 for v in o.data.vertices:
  p=v.co.copy(); fac=1+.22*noise.noise_vector(p*2.4+seed).x+.08*noise.noise(p*7+seed)
  v.co=p*fac
 o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.rotation_euler=(random.uniform(-.35,.35),random.uniform(-.35,.35),random.uniform(-.35,.35))
 o.data.materials.append(m)
 for poly in o.data.polygons:poly.use_smooth=True
 return o
''' + source[end:]
source = source.replace("(.053,.045,.045)", "(.061,.054,.052)")
start = source.index('# Bellows fan:')
end = source.index('# Hollow crucible', start)
source = source[:start] + '''# Hinged leather fan, narrow throat against masonry and broad rear operating frame.
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
''' + source[end:]
# Remove the former stitches floating above rectangular boards.
start=source.index('for y in [-.076,.076]:')
end=source.index('# Controlled inspection scene',start)
source=source[:start]+source[end:]
# Hearth illumination is local, independent of albedo and studio lights.
source=source.replace('# Controlled inspection scene', "bpy.ops.object.light_add(type='POINT',location=(0,-.04,.20));bpy.context.object.data.energy=1.2;bpy.context.object.data.color=(1,.18,.025);bpy.context.object.data.shadow_soft_size=.06\n# Controlled inspection scene")
exec(compile(source, __file__, 'exec'))
