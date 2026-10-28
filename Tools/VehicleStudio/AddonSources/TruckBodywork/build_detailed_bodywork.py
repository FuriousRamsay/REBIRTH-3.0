"""Actual Blender mesh authoring; see TRUCK_BODYWORK_DETAILED_SPEC_20261020.md.
Unity source coordinates are kept in helper arguments, converted at mesh creation.
No game launch, bundle replacement, or AI-generated review images.
"""
import bpy, bmesh, json, math, sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.geometry import tessellate_polygon
from math import sin, cos, pi, sqrt

SOURCE=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkDetailed_20261020')
OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkRearSmooth_20261020')
ORIGINAL=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/OriginalCamperClay_20261020/original-camper.json')
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def material(name,value,roughness=.46,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(value,value,value,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(value,value,value,1);p.inputs['Roughness'].default_value=roughness;p.inputs['Metallic'].default_value=metal
 return m
PAINT=material('Neutral clay - formed body',.48);STEEL=material('Neutral clay - steel',.28,.38,.3);CAST=material('Neutral clay - cast alloy',.33,.5,.15);RUBBER=material('Neutral clay - rubber',.12,.63);GLASS=material('Neutral clay - glazing',.19,.22,.05);LENS=material('Neutral clay - lamps',.36,.28)
gp=GLASS.node_tree.nodes.get('Principled BSDF');gp.inputs['Base Color'].default_value=(.72,.72,.72,1);gp.inputs['Transmission Weight'].default_value=1.0;gp.inputs['Roughness'].default_value=.07;gp.inputs['IOR'].default_value=1.45
WHEEL=PAINT.copy();WHEEL.name='Native wheel clay with original relief';TIRE=RUBBER.copy();TIRE.name='Native tire clay with original tread relief'
normal_image=bpy.data.images.load('E:/_Haven/shared/REBIRTHVehicleStudio_Camper/Assets/Perfectvertex/80sCamperTruck/Art/Textures/wheel_Normal.png',check_existing=True);normal_image.colorspace_settings.name='Non-Color'
for mat in [WHEEL,TIRE]:
 tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=normal_image;n=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(tex.outputs['Color'],n.inputs['Color']);mat.node_tree.links.new(n.outputs['Normal'],mat.node_tree.nodes['Principled BSDF'].inputs['Normal'])
COLLECTIONS={}
for name in ['NativeTruckReference','TruckCommonStructure','PickupOnlyInfill','Studio']:
 c=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(c);COLLECTIONS[name]=c
CURRENT='NativeTruckReference'
FLOOR_FACES=[]
def U(p):return Vector((p[0],-p[2],p[1]))
def mesh(name,verts,faces,mat=PAINT,smooth=False,bevel=0,solid=0):
 m=bpy.data.meshes.new(name);m.from_pydata([U(v) for v in verts],[],faces);m.update()
 o=bpy.data.objects.new(name,m);COLLECTIONS[CURRENT].objects.link(o);m.materials.append(mat)
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 for p in m.polygons:p.use_smooth=smooth
 if solid:
  mod=o.modifiers.new('Sheet thickness','SOLIDIFY');mod.thickness=solid;mod.offset=-1
 if bevel:
  mod=o.modifiers.new('Manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=3;mod.limit_method='ANGLE'
  mod=o.modifiers.new('Face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
 return o
def native():
 d=json.loads((SOURCE/'native-truck.json').read_text())
 for part in d['parts']:
  v=[tuple(p[a] for a in ('x','y','z')) for p in part['vertices']];t=part['triangles'];name=part['path'].split('/')[-1]
  faces=[tuple(t[i:i+3]) for i in range(0,len(t),3)]
  # Rear plate belongs to the camper. Keep the front original plate.
  if name=='licence_plate':faces=[f for f in faces if sum(v[i][2] for i in f)/3>0]
  if '--survey' not in sys.argv and name=='parts':
   # Read the original bed surface for the pickup overlay, but preserve ALL source
   # triangles: floor, underside, axles and both exhaust outlets stay common.
   for f in faces:
    ps=[Vector(v[i]) for i in f];c=sum(ps,Vector())/3;n=(ps[1]-ps[0]).cross(ps[2]-ps[0]).normalized()
    if c.z<-.42 and abs(c.y-.670)<.004 and abs(n.y)>.98:
     FLOOR_FACES.append([tuple(p) for p in ps])
  if not faces:continue
  o=mesh(name,v,faces,GLASS if name=='windows' else LENS if 'glass' in name or 'Lenses' in name or name=='lights' else RUBBER if name=='interior' else PAINT,True)
  normals=[U(tuple(n[a] for a in ('x','y','z'))) for n in part['normals']]
  # Preserve native shading and winding against the exported source normals.
  for poly in o.data.polygons:
   n=sum((normals[i] for i in poly.vertices),Vector())
   if poly.normal.dot(n)<0:poly.flip()
  o.data.update()
  if len(normals)==len(v):o.data.normals_split_custom_set_from_vertices(normals)
  if part.get('uv') and len(part['uv'])==len(v):
   layer=o.data.uv_layers.new(name='Original Unity UV')
   for loop in o.data.loops:
    uv=part['uv'][loop.vertex_index];layer.data[loop.index].uv=(uv['x'],uv['y'])
  if 'wheel_' in name:
   o.data.materials[0]=WHEEL;o.data.materials.append(TIRE)
   # Native wheel material separation inferred by tire radial envelope only in review.
   center=Vector((sum(p[0] for p in v)/len(v),.375,1.289 if '_F' in name else -1.620))
   for poly in o.data.polygons:
    p=sum((Vector(v[i]) for i in poly.vertices),Vector())/len(poly.vertices)
    if sqrt((p.y-center.y)**2+(p.z-center.z)**2)>.255:poly.material_index=1
native()

def loft(name,loops,mat=PAINT,cap=True,smooth=True,bevel=0,solid=0):
 n=len(loops[0]);v=[p for loop in loops for p in loop];f=[]
 for i in range(len(loops)-1):
  for j in range(n):f.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
 if cap:f.extend([tuple(range(n-1,-1,-1)),tuple((len(loops)-1)*n+j for j in range(n))])
 return mesh(name,v,f,mat,smooth,bevel,solid)
def rounded(w,h,r,steps=6):
 pts=[];r=min(r,w/2,h/2)
 for cx,cy,start in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
  for i in range(steps+1):
   a=math.radians(start+90*i/steps);pts.append((cx+r*cos(a),cy+r*sin(a)))
 return pts
def panel(name,center,w,h,depth,r=.015,mat=PAINT,axis='z',bevel=.001):
 # Profile loops form a rounded rectangular slab; local dimensions lie in its face.
 def point(u,v,d):
  x,y,z=center
  return (x+u,y+v,z+d) if axis=='z' else (x+u,y+d,z+v) if axis=='y' else (x+d,y+v,z+u)
 loops=[[point(u,v,d) for u,v in rounded(w,h,r)] for d in [-depth/2,depth/2]]
 return loft(name,loops,mat,True,False,bevel)
def ring(name,center,spec,axis='z',mat=PAINT,cap=False):
 # Each contour is (width,height,corner radius,depth); quad rings are actual pressed reveals.
 x,y,z=center
 def p(u,v,d):return (x+u,y+v,z+d) if axis=='z' else (x+u,y+d,z+v) if axis=='y' else (x+d,y+v,z+u)
 loops=[[p(u,v,d) for u,v in rounded(w,h,r)] for w,h,r,d in spec]
 if cap=='end':
  n=len(loops[0]);faces=[]
  for i in range(len(loops)-1):
   for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
  faces.append(tuple((len(loops)-1)*n+j for j in range(n)))
  return mesh(name,[v for l in loops for v in l],faces,mat,False,0,.0015)
 return loft(name,loops,mat,cap,True,0,.0015 if not cap else 0)

def stamped_sheet(name,center,width,height,count,axis='z',recess=.009):
 # One continuous sheet: each cell has a planar margin, rounded shoulder and inset field.
 # Only the inset contour is capped; the outside contour is never covered by an n-gon.
 pieces=[];cell=width/count
 for i in range(count):
  x,y,z=center;u=-width/2+cell*(i+.5)
  at=(x+u,y,z) if axis=='z' else (x,y,z+u)
  pieces.append(ring(name+' cell',at,[(cell,height,.0001,0),(cell-.038,height-.054,.014,0),(cell-.055,height-.071,.020,recess*.4),(cell-.068,height-.084,.022,recess)],axis,PAINT,'end'))
 bpy.ops.object.select_all(action='DESELECT')
 for o in pieces:o.select_set(True)
 bpy.context.view_layer.objects.active=pieces[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
 bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00025);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
 return obj
def tube(name,points,radius,mat=STEEL,sections=12,closed=False,wall=0):
 pts=[Vector(p) for p in points];loops=[];ref=Vector((1,0,0))
 for i,p in enumerate(pts):
  direction=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
  if abs(direction.dot(ref))>.9:ref=Vector((0,0,1))
  a=direction.cross(ref).normalized();b=direction.cross(a).normalized()
  loops.append([tuple(p+radius*(a*cos(2*pi*j/sections)+b*sin(2*pi*j/sections))) for j in range(sections)])
 return loft(name,loops,mat,not wall,True,0,wall)
def smoothpath(points,steps=5):
 pts=[Vector(p) for p in points];out=[]
 for i in range(len(pts)-1):
  p0,p1,p2,p3=pts[max(0,i-1)],pts[i],pts[i+1],pts[min(len(pts)-1,i+2)]
  for j in range(steps):
   t=j/steps;out.append(tuple(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)))
 return out+[tuple(pts[-1])]
def bolt(name,p,axis=(0,1,0),r=.010):
 a=Vector(axis).normalized();p=Vector(p)
 return tube(name,[p-a*.003,p+a*.007],r,STEEL,6)
def casting(name,sections,mat=CAST,n=16):
 # Distinct elliptical cast sections along Z, not a sphere or cylinder.
 loops=[]
 for z,y,rx,ry in sections:loops.append([(rx*cos(2*pi*j/n),y+ry*sin(2*pi*j/n),z) for j in range(n)])
 return loft(name,loops,mat,True,True,.003)

def corrugated_floor():
 global CURRENT
 CURRENT='PickupOnlyInfill'
 def clip(poly,axis,bound,greater):
  out=[]
  for a,b in zip(poly,poly[1:]+poly[:1]):
   ina=(a[axis]>=bound) if greater else (a[axis]<=bound);inb=(b[axis]>=bound) if greater else (b[axis]<=bound)
   if ina:out.append(a)
   if ina!=inb:
    t=(bound-a[axis])/(b[axis]-a[axis]);out.append(tuple(a[k]+t*(b[k]-a[k]) for k in range(3)))
  return out
 # Clip native floor topology at the rib shoulders, preserving its wheel-tub cutouts.
 xs=[-.84,.84]
 for rib in range(-7,8):
  xs.extend([rib*.102+q for q in [-.051,-.032,-.025,-.020,-.015,.015,.020,.025,.032,.051]])
 xs=sorted(set(xs));zs=[-2.69,-2.66,-2.63,-2.59,-.54,-.50,-.46,-.42]
 verts=[];faces=[]
 for original in FLOOR_FACES:
  for a,b in zip(xs,xs[1:]):
   if a>max(v[0] for v in original) or b<min(v[0] for v in original):continue
   cut=clip(clip(original,0,a,True),0,b,False)
   if len(cut)<3:continue
   for c,d in zip(zs,zs[1:]):
    polygon=clip(clip(cut,2,c,True),2,d,False)
    if len(polygon)<3:continue
    ids=[]
    for x,y,z in polygon:
     phase=abs((x+.051)%.102-.051)
     rise=.012 if phase<.015 else .012*(.5+.5*cos(pi*(phase-.015)/.010)) if phase<.025 else 0
     fade=min(1,max(0,(z+2.66)/.07),max(0,(-.46-z)/.06))
     ids.append(len(verts));verts.append((x,y+.002+rise*fade,z))
    faces.append(tuple(ids))
 o=mesh('Continuous pressed load floor - native tub cutouts',verts,faces,PAINT,False,0,.002)
 # Weld clipping boundaries so the sheet is a real continuous surface.
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00003);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 for f in bm.faces:
  if f.normal.z<0:f.normal_flip()
 bm.to_mesh(o.data);bm.free()
 # Connected recessed fields, matching the reference's narrow pressed-sheet pattern.
 stamped_sheet('Continuous bed front wall stamping',(0,.898,-.437),1.54,.438,8,recess=.010)
 for side in [-1,1]:
  # Each wall's pressed fields are continuous; the existing wheel tub covers lower fields.
  stamped_sheet('Continuous inner bedside '+str(side),(side*.781,.905,-1.565),2.20,.44,11,axis='x',recess=side*.009)
  # Folded threshold ends and bed-floor mounting heads, kept out of tire envelope.
  for z in [-.61,-2.48]:bolt('Bed sill carriage bolt',(side*.61,.681,z),r=.008)
 panel('Tailgate threshold',(0,.675,-2.664),1.54,.065,.025,.008,axis='y')

def tailgate_bumper():
 global CURRENT
 CURRENT='PickupOnlyInfill'
 # Measured native rear jamb at x=.7514: shoulder curves forward from
 # z=-2.769 at y=.99 to z=-2.699 at y=1.160. Leave a 6 mm side reveal.
 profile=[(.685,-2.749),(.6953,-2.7541),(.7047,-2.7577),(.7156,-2.7614),(.7356,-2.7653),(.7731,-2.7683),(.8663,-2.7689),(.9493,-2.7684),(.9873,-2.7682),(1.0068,-2.7595),(1.0279,-2.7454),(1.0502,-2.7302),(1.0802,-2.7192),(1.1361,-2.7054),(1.1604,-2.6987),(1.1651,-2.6914),(1.168,-2.6835)]
 # Shape-preserving cubic Hermite sampling removes visible facets while
 # interpolating every measured section point without overshooting the jamb.
 measured_profile=list(profile)
 h=[b[0]-a[0] for a,b in zip(profile,profile[1:])]
 d=[(b[1]-a[1])/step for a,b,step in zip(profile,profile[1:],h)]
 slopes=[d[0]]
 for i in range(1,len(profile)-1):
  if d[i-1]*d[i]<=0:slopes.append(0.0)
  else:
   w1=2*h[i]+h[i-1];w2=h[i]+2*h[i-1]
   slopes.append((w1+w2)/(w1/d[i-1]+w2/d[i]))
 slopes.append(d[-1]);dense=[]
 for i,((y0,z0),(y1,z1)) in enumerate(zip(profile,profile[1:])):
  for j in range(8):
   t=j/8;step=y1-y0
   z=(2*t**3-3*t*t+1)*z0+(t**3-2*t*t+t)*step*slopes[i]+(-2*t**3+3*t*t)*z1+(t**3-t*t)*step*slopes[i+1]
   dense.append((y0+t*step,z))
 profile=dense+[profile[-1]]
 def face_z(y):
  for (a,za),(b,zb) in zip(profile,profile[1:]):
   if y<=b:return za+(zb-za)*max(0,min(1,(y-a)/(b-a)))
  return profile[-1][1]
 # Continuous closed formed skin, including its curved shoulder and rolled top.
 outline=[(y,z+.001) for y,z in profile]+[(1.169,-2.675),(1.165,-2.662)]+[(y,z+.034) for y,z in reversed(profile[:-1])]
 loops=[[(x,y,z) for y,z in outline] for x in [-.7454,-.7374,.7374,.7454]]
 body=loft('Tailgate folded outer shell',loops,PAINT,True,True,.002)
 for mod in list(body.modifiers):
  if mod.type=='WEIGHTED_NORMAL':body.modifiers.remove(mod)
 for poly in body.data.polygons:
  if len(poly.vertices)>4:poly.use_smooth=False
 body.data.set_sharp_from_angle(angle=math.radians(50))
 hz=face_z(1.028)+.001
 cutter=panel('Handle pocket cutter',(0,1.028,hz),.205,.075,.090,.009)
 bpy.context.view_layer.objects.active=body
 mod=body.modifiers.new('Recessed handle aperture','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
 ring('Handle pocket stamped lip',(0,1.028,hz-.003),[(.216,.084,.010,0),(.200,.070,.009,.004),(.183,.054,.007,.018)],mat=RUBBER,cap=False)
 panel('Handle pocket shadow',(0,1.028,hz+.018),.183,.054,.004,.007,RUBBER)
 panel('Tailgate lift latch',(0,1.034,hz+.005),.158,.026,.012,.004,STEEL)
 # Warp the inner pressed skin to the same measured section instead of leaving
 # a flat panel disconnected from the outer shell.
 inner=stamped_sheet('Tailgate inner pressed sheet',(0,.917,0),1.43,.425,7,recess=.009)
 for v in inner.data.vertices:
  y=v.co.z;v.co.y=-(face_z(y)+.037-v.co.y)
 inner.data.update()
 for side in [-1,1]:
  tube('Tailgate hinge barrel',[(side*.61-.055,.688,-2.722),(side*.61+.055,.688,-2.722)],.012,STEEL,16)
  panel('Tailgate hinge leaf',(side*.61,.698,-2.720),.080,.033,.004,.006,STEEL)
  panel('Tailgate side latch plate',(side*.742,1.048,-2.711),.033,.073,.003,.006,STEEL,axis='x')
  # Lamps fitted to the original small rear apertures.
  ring('Rear lamp bezel',(side*.817,.885,-2.751),[(.105,.286,.011,0),(.091,.270,.009,-.005),(.081,.253,.007,-.009)],mat=RUBBER,cap=True)
  for y,h in [(.963,.081),(.877,.078),(.802,.059)]:
   panel('Rear lamp lens segment',(side*.817,y,-2.763),.079,h,.008,.007,LENS if y>.82 else GLASS)
   for dy in [-.018,-.006,.006,.018]:panel('Fine lens flute',(side*.817,y+dy,-2.768),.073,.0014,.0014,.0005,LENS)
 # Close the lower rear body behind the bumper; no exposed diagonal/open wedge.
 panel('Formed rear lower sill',(0,.646,-2.690),1.56,.080,.040,.012,PAINT)
 # Adapt the actual native front bumper: its rolled cross-section and round
 # corner returns give both ends of this truck the same design language.
 source=json.loads((SOURCE/'native-truck.json').read_text())
 part=next(p for p in source['parts'] if p['path'].endswith('/parts'))
 v=part['vertices'];t=part['triangles'];parent=list(range(len(v)))
 def find(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 for i in range(0,len(t),3):
  for a in t[i+1:i+3]:parent[find(a)]=find(t[i])
 groups={}
 for i in t:groups.setdefault(find(i),set()).add(i)
 selected=set()
 for ids in groups.values():
  if max(v[i]['z'] for i in ids)>2.07 and min(v[i]['y'] for i in ids)>.49 and max(v[i]['y'] for i in ids)<.71:selected.update(ids)
 faces=[tuple(t[i:i+3]) for i in range(0,len(t),3) if all(j in selected for j in t[i:i+3])]
 used=sorted(selected);remap={old:new for new,old in enumerate(used)}
 # Remove only the donor front bumper's plan-view bow. The main rear span
 # becomes straight; beyond the start of each corner the correction is constant
 # so the approved round return and full width are retained.
 bow=[(0,2.11897),(.00383,2.11897),(.12910,2.11310),(.21321,2.10840),(.28241,2.09986),(.55520,2.05761),(.76579,2.00535)]
 def unbow(x):
  ax=abs(x)
  for (a,za),(b,zb) in zip(bow,bow[1:]):
   if ax<=b:
    derivative=(za-zb)/(b-a)
    return 2.11897-(za+(zb-za)*(ax-a)/(b-a)),derivative*(-1 if x<0 else 1)
  return 2.11897-bow[-1][1],0
 verts=[(v[i]['x']*1.04,v[i]['y']-.04,-2.810+(2.1243300437927246-v[i]['z']-unbow(v[i]['x'])[0])) for i in used]
 bumper=mesh('Rear bumper - straight span rounded returns',verts,[tuple(remap[i] for i in f) for f in faces],STEEL,True)
 norms=[]
 for i in used:
  n=part['normals'][i];slope=unbow(v[i]['x'])[1]
  norms.append(U(((n['x']-slope*n['z'])/1.04,n['y'],-n['z'])).normalized())
 (OUT/'bumper-straightening.json').write_text(json.dumps({'source_lip_samples_x_z':bow,'corrected_lip_depths':[z+unbow(x)[0] for x,z in bow],'straight_span_half_width_m':.76579*1.04,'corner_width_scale_unchanged':1.04,'corner_translation_m':unbow(.85)[0],'tailgate_measured_stations':len(measured_profile),'tailgate_smooth_stations':len(profile)},indent=2))
 for poly in bumper.data.polygons:
  if poly.normal.dot(sum((norms[i] for i in poly.vertices),Vector()))<0:poly.flip()
 bumper.data.update();bumper.data.normals_split_custom_set_from_vertices(norms)
 # Plate stays on the face; no large central step or raised tread pads.
 panel('Rear registration plate',(0,.570,-2.814),.310,.114,.003,.005,RUBBER)
 for x in [-.13,.13]:bolt('Plate screw',(x,.610,-2.817),(0,0,-1),.004)
 for side in [-1,1]:
  panel('Bumper bracket web',(side*.46,.565,-2.688),.14,.064,.006,.008,STEEL,axis='x')
  bolt('Bumper mount bolt',(side*.464,.565,-2.688),(side,0,0),.009)
 (OUT/'rear-fit-measurements.json').write_text(json.dumps({'tailgate_half_width_m':.7454,'native_jamb_half_width_m':.7514,'side_gap_m':.006,'tailgate_top_m':1.169,'native_bedside_top_m':1.1714,'section_y_z_m':profile,'rear_bumper_source':'original front bumper connected mesh components','bumper_source_vertices':len(used),'bumper_rearmost_z_m':-2.810,'previous_bumper_rearmost_z_m':-2.855,'reduction_in_rear_projection_m':.045},indent=2))

def correct_reference_cab():
 global CURRENT
 CURRENT='TruckCommonStructure'
 # Retain outer native rear window frame; remove only its interior dividers.
 for name in ['parts','body']:
  o=bpy.data.objects.get(name)
  if not o:continue
  bm=bmesh.new();bm.from_mesh(o.data);remove=[]
  for f in bm.faces:
   c=f.calc_center_median();x,y,z=c.x,c.z,-c.y
   if abs(x)<.56 and 1.311<y<1.636 and -.401<z<-.337:remove.append(f)
  bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(o.data);bm.free()
 o=bpy.data.objects.get('windows');bm=bmesh.new();bm.from_mesh(o.data);points=[];remove=[]
 for f in bm.faces:
  if all(-v.co.y<-.30 for v in f.verts):
   remove.append(f);points.extend([(v.co.x,v.co.z,-v.co.y) for v in f.verts])
 bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(o.data);bm.free()
 # Convex outline of the existing panes gives their exact original outer aperture.
 ps=sorted(set((round(x,5),round(y,5)) for x,y,z in points))
 def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 lo=[];hi=[]
 for p in ps:
  while len(lo)>=2 and cross(lo[-2],lo[-1],p)<=0:lo.pop()
  lo.append(p)
 for p in reversed(ps):
  while len(hi)>=2 and cross(hi[-2],hi[-1],p)<=0:hi.pop()
  hi.append(p)
 outline=lo[:-1]+hi[:-1]
 mesh('Single clear rear cab pane',[(x,y,-.376+(y-1.309)*.092) for x,y in outline],[tuple(range(len(outline)))],GLASS,False,0,.002)
 # Rectangular mirror housings follow the concept while preserving original mounting position.
 for name in ['parts','mirrors']:
  o=bpy.data.objects.get(name);bm=bmesh.new();bm.from_mesh(o.data);remove=[]
  for f in bm.faces:
   c=f.calc_center_median();x,y,z=c.x,c.z,-c.y
   if abs(x)>.889 and 1.18<y<1.38 and .23<z<.44:remove.append(f)
  bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(o.data);bm.free()
 for side in [-1,1]:
  panel('Rectangular door mirror housing',(side*.976,1.285,.336),.151,.168,.039,.014,RUBBER)
  panel('Door mirror glass',(side*.976,1.285,.313),.128,.143,.003,.009,STEEL)
  tube('Door mirror mounting stalk',[(side*.867,1.23,.335),(side*.929,1.27,.335)],.012,STEEL,12)

if '--survey' not in sys.argv:
 corrugated_floor();tailgate_bumper();correct_reference_cab()

def verify_model():
 source=json.loads((SOURCE/'native-truck.json').read_text());report={'native_wheels':[], 'geometry_checks':{},'review_only':True}
 for p in source['parts']:
  name=p['path'].split('/')[-1]
  if 'wheel_' not in name:continue
  obj=bpy.data.objects[name];expected=[U(tuple(v[a] for a in ('x','y','z'))) for v in p['vertices']]
  error=max((obj.data.vertices[i].co-v).length for i,v in enumerate(expected))
  report['native_wheels'].append({'name':name,'vertices':len(expected),'max_position_error_m':error,'preserved':error<1e-6})
 # Compare actual source triangle coordinates, independent of winding and vertex
 # reindexing by bmesh. The bounds exclude camper-specific rear steps/fixtures.
 original=json.loads(ORIGINAL.read_text())
 source_parts=next(p for p in original['parts'] if p['path']=='80sCamperTruck/parts')
 def key(points):return tuple(sorted(tuple(round(x,6) for x in v) for v in points))
 obj=bpy.data.objects['parts']
 actual={key([obj.data.vertices[i].co for i in poly.vertices]) for poly in obj.data.polygons}
 expected=[];outlets={-1:[],1:[]}
 for i in range(0,len(source_parts['triangles']),3):
  ids=source_parts['triangles'][i:i+3]
  ps=[source_parts['vertices'][j] for j in ids]
  coords=[U(tuple(v[a] for a in ('x','y','z'))) for v in ps]
  if all(v['y']<.70 and -2.65<v['z']<1.79 for v in ps):expected.append(key(coords))
  c={a:sum(v[a] for v in ps)/3 for a in ('x','y','z')}
  if .79<abs(c['x'])<.89 and .48<c['y']<.60 and -2.60<c['z']<-2.02:
   outlets[-1 if c['x']<0 else 1].append(key(coords))
 report['original_underside']={'source':str(ORIGINAL),'triangles_checked':len(expected),'missing_or_moved':sum(f not in actual for f in expected)}
 report['original_exhaust_outlets']={str(side):{'triangles_checked':len(faces),'missing_or_moved':sum(f not in actual for f in faces)} for side,faces in outlets.items()}
 rejected=('Swept C-section frame rail','Rear fuel tank','Continuous swept exhaust','Oval muffler','Rear axle','Front suspension','Transmission','Engine sump','Lower engine','Bellhousing','Propeller','Rear leaf')
 report['geometry_checks']['no_replacement_chassis']=not any(o.name.startswith(rejected) for o in bpy.data.objects)
 report['geometry_checks']['original_underside_preserved']=len(expected)>0 and report['original_underside']['missing_or_moved']==0
 report['geometry_checks']['both_original_exhaust_outlets_preserved']=all(v['triangles_checked']>0 and v['missing_or_moved']==0 for v in report['original_exhaust_outlets'].values())
 report['geometry_checks']['no_rejected_pad_objects']=not any(o.name.startswith(('Front bulkhead pressing','Bedside stamped field','Bumper tread pad')) for o in bpy.data.objects)
 report['geometry_checks']['original_wheel_uvs_present']=all(len(bpy.data.objects[r['name']].data.uv_layers)>0 for r in report['native_wheels'])
 report['geometry_checks']['four_original_wheels_retained']=len(report['native_wheels'])==4 and all(r['preserved'] for r in report['native_wheels'])
 report['mesh_objects']=sum(o.type=='MESH' for o in bpy.data.objects)
 report['base_polygons']=sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH')
 (OUT/'geometry-review.json').write_text(json.dumps(report,indent=2))
 assert report['geometry_checks']['four_original_wheels_retained']
 assert report['geometry_checks']['original_underside_preserved']
 assert report['geometry_checks']['both_original_exhaust_outlets_preserved']
 assert report['geometry_checks']['no_replacement_chassis']
 assert report['geometry_checks']['original_wheel_uvs_present']

def studio():
 global CURRENT
 CURRENT='Studio';sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=28;sc.cycles.use_denoising=True
 sc.render.resolution_x=1500;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
 sc.world.color=(.16,.16,.16);sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.25,.28,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.35
 sc.view_settings.view_transform='AgX';sc.view_settings.exposure=-1.5;sc.render.image_settings.file_format='PNG'
 cam=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));COLLECTIONS['Studio'].objects.link(cam);cam.data.type='ORTHO';sc.camera=cam
 lights=[]
 for name,power,size in [('Key',1500,5),('Fill',850,4),('Rim',1700,3)]:
  l=bpy.data.lights.new(name,'AREA');l.energy=power;l.shape='DISK';l.size=size;o=bpy.data.objects.new(name,l);COLLECTIONS['Studio'].objects.link(o);lights.append(o)
 return cam,lights
def render(name,direction,target=(0,.85,-.35),scale=6.0,roll=0):
 sc=bpy.context.scene;cam=sc.camera;at=U(target);delta=U(direction);cam.location=at+delta
 forward=(at-cam.location).normalized();up=U((0,0,1)) if abs(direction[1])>7 else U((0,1,0));right=forward.cross(up).normalized();up=right.cross(forward)
 rotation=Matrix((right,up,-forward)).transposed()
 if roll:rotation=rotation@Matrix.Rotation(math.radians(roll),3,'Z')
 cam.rotation_euler=rotation.to_euler();cam.data.ortho_scale=scale
 for obj,loc in zip(LIGHTS,[at-forward*4-right*3+up*4,at-forward*2+right*4+up,at+forward*3-right+up*3]):
  obj.location=loc;obj.rotation_euler=(at-loc).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)

CAM,LIGHTS=studio()
if '--survey' in sys.argv:
 render('native-rear',(-5,3,-6),scale=5.8)
 render('native-bottom',(0,-8,0),scale=5.8)
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'native-reference.blend'))
else:
 verify_model()
 bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'truck-bodywork-detailed.blend'))
 render('rear-three-quarter',(5,3,-6),scale=5.9)
 render('opposite-rear-three-quarter',(-5,3,-6),scale=5.9)
 render('rear',(0,0,-7),target=(0,.85,-.35),scale=2.6)
 render('rear-detail',(3,1.6,-6),target=(0,.86,-2.45),scale=2.4)
 render('bottom',(0,-8,0),scale=5.9,roll=90)
 render('underside-three-quarter',(2,-9,-2),target=(0,.65,-.35),scale=6.1,roll=-112)
 # Store a useful review camera and hide studio overlays in the editable viewport.
 # Restore the exterior camera without another expensive render.
 CAM.location=U((0,.85,-.35))+U((5,3,-6));CAM.rotation_euler=(U((0,.85,-.35))-CAM.location).to_track_quat('-Z','Y').to_euler();CAM.data.ortho_scale=5.9
 for obj in COLLECTIONS['Studio'].objects:obj.hide_set(True)
 for area in bpy.context.screen.areas if bpy.context.screen else []:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=6;area.spaces.active.region_3d.view_location=U((0,.8,-.35))
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'truck-bodywork-detailed.blend'))
 (OUT/'build-complete.json').write_text(json.dumps({'source':'native-truck.json','objects':len(bpy.data.objects),'added_objects':sum(len(COLLECTIONS[n].objects) for n in ['PickupOnlyInfill','TruckCommonStructure']),'native_wheels':[o.name for o in COLLECTIONS['NativeTruckReference'].objects if 'wheel_' in o.name],'status':'CLAY REVIEW - NOT INSTALLED'},indent=2))
