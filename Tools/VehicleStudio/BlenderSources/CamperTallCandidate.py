import bpy,json,math,collections,copy
from mathutils import Vector
from pathlib import Path
P=Path('E:/_Haven/shared/REBIRTHTemp/CamperTallCandidate_20261014');(P/'Output').mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mix(a,b,t):return {'p':[a['p'][i]+(b['p'][i]-a['p'][i])*t for i in range(3)],'uv':[a['uv'][i]+(b['uv'][i]-a['uv'][i])*t for i in range(2)],'n':[a['n'][i]+(b['n'][i]-a['n'][i])*t for i in range(3)]}
def clip(poly,axis,k,below):
 out=[]
 for a,b in zip(poly,poly[1:]+poly[:1]):
  av=a['p'][axis]-k;bv=b['p'][axis]-k;ai=av<=0 if below else av>=0;bi=bv<=0 if below else bv>=0
  if ai:out.append(copy.deepcopy(a))
  if ai!=bi:out.append(mix(a,b,av/(av-bv)))
 return out

def subtract(poly):
 kept=[]
 for axis,k,below in [(0,-.40,True),(0,.40,False),(1,.70,True),(1,2.60,False)]:
  if len(poly)<3:break
  q=clip(poly,axis,k,below)
  if len(q)>=3:kept.append(q)
  poly=clip(poly,axis,k,not below)
 return kept

def doory(y):return y if y<=.71 else y+.626*(y-.71)/.46 if y<1.17 else y+.626
report=[]
for file in (P/'Input').glob('*.json'):
 d=json.load(open(file,encoding='utf-8-sig'));name=d['name'];outv=[];outu=[];outn=[];faces=[]
 def add(poly):
  if len(poly)<3:return
  start=len(outv)
  outv.extend(v['p'][:] for v in poly);outu.extend(v['uv'][:] for v in poly);outn.extend(v['n'][:] for v in poly)
  for i in range(1,len(poly)-1):faces.append([start,start+i,start+i+1])
 def finish(poly):
  if name in ['camper_body','camper_interior'] and all(v['p'][2]<-3.20 for v in poly):
   for q in subtract(poly):add(q)
  else:add(poly)
 def at(i):
  p=d['vertices'][i].copy()
  if name.startswith('RearDoor_'):p['x']*=.40/.36
  elif p['z']< -3.20 and abs(p['x'])<.60:
   a=abs(p['x']);p['x']=math.copysign(a*.40/.36 if a<=.36 else .40+(a-.36)*.20/.24,p['x'])
  u=d['uv'][i] if len(d['uv']) else {'x':0,'y':0};n=d['normals'][i];return {'p':[p['x'],p['y'],p['z']],'uv':[u['x'],u['y']],'n':[n['x'],n['y'],n['z']]}
 # Connected decorative components crossing the wall cut remain intact.
 parent=list(range(len(d['vertices'])))
 def find(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 points={}
 for i,v in enumerate(d['vertices']):
  key=tuple(round(v[a],5) for a in ['x','y','z'])
  if key in points:parent[find(i)]=find(points[key])
  else:points[key]=i
 for j in range(0,len(d['triangles']),3):
  a,b,c=d['triangles'][j:j+3];parent[find(b)]=find(a);parent[find(c)]=find(a)
 components=collections.defaultdict(list)
 for i,v in enumerate(d['vertices']):components[find(i)].append(v)
 decor=set()
 if name=='camper_interior':
  for key,vs in components.items():
   lo=min(v['y'] for v in vs);hi=max(v['y'] for v in vs)
   if lo<2.42<hi and hi-lo<.90:decor.add(key)
 def floor_parts(poly):
  # Remove the central old floor AND its former riser. Rebuild a single surface below.
  keep=[];q=poly
  for axis,k,below in [(0,-.58,True),(0,.58,False)]:
   outside=clip(q,axis,k,below)
   if len(outside)>=3:keep.append(outside)
   q=clip(q,axis,k,not below)
   if len(q)<3:break
  return keep
 for j in range(0,len(d['triangles']),3):
  poly=[at(i) for i in d['triangles'][j:j+3]]
  if name=='camper_interior' and find(d['triangles'][j]) in decor:
   add(poly);continue
  if name=='camper_interior' and all(.65<v['p'][1]<1.05 and v['n'][1]>.15 for v in poly) and all(abs(v['p'][0])<.60 for v in poly):
   for q in floor_parts(poly):add(q)
   continue
  if name.startswith('RearDoor_'):
   for v in poly:v['p'][1]=doory(v['p'][1])
   add(poly);continue
  frame=name=='camper_body' and all(abs(v['p'][0])<.60 and .69<v['p'][1]<2.02 and v['p'][2]<-3.26 for v in poly)
  if frame:
   for v in poly:v['p'][1]=doory(v['p'][1])
   add(poly);continue
  lower=clip(poly,1,2.42,True);upper=clip(poly,1,2.42,False)
  if len(lower)>=3:finish(lower)
  if len(upper)>=3:
   for v in upper:v['p'][1]+=.40
   finish(upper)
  cuts=[]
  for a,b in zip(poly,poly[1:]+poly[:1]):
   if (a['p'][1]-2.42)*(b['p'][1]-2.42)<0:cuts.append(mix(a,b,(2.42-a['p'][1])/(b['p'][1]-a['p'][1])))
  if len(cuts)==2:
   a,b=cuts
   for k in range(4):
    lo=k*.10;hi=(k+1)*.10
    def raised(v,y):q={'p':v['p'][:],'uv':v['uv'][:],'n':v['n'][:]};q['p'][1]+=y;return q
    quad=[raised(a,lo),raised(b,lo),raised(b,hi),raised(a,hi)]
    # Map the extension to clean plank/siding atlas regions, not a stretched scanline.
    side=abs(a['p'][0]-b['p'][0])<abs(a['p'][2]-b['p'][2])
    for v in quad:
     horizontal=(v['p'][2]+3.42)/4.14 if side else (v['p'][0]+.91)/1.82
     if name=='camper_interior':v['uv']=[.62+.35*horizontal,.945+.014*((k%2)+(v['p'][1]-2.42-lo)/.10)]
     elif not (math.dist(a['p'],b['p'])<.04 and min(abs(a['p'][0]),abs(b['p'][0]))>.87):v['uv']=[.64+.33*horizontal,.85+.105*(v['p'][1]-2.42)/.40]
    normal=(Vector(poly[1]['p'])-Vector(poly[0]['p'])).cross(Vector(poly[2]['p'])-Vector(poly[0]['p']))
    qnormal=(Vector(quad[1]['p'])-Vector(quad[0]['p'])).cross(Vector(quad[2]['p'])-Vector(quad[0]['p']))
    if normal.dot(qnormal)<0:quad.reverse()
    finish(quad)
 if name=='camper_interior':
  # Six strips across the full original aisle floor, with bounded UVs in a clean carpet patch.
  # This also bridges the former 3cm-wide step at z=-2.84.
  zs=sorted(set([-3.317,-2.851,-2.819,-2.60,-2.20,-.626]+[-3.317+i*.10 for i in range(1,27)]))
  def floory(z):return .72+.19*max(0,min(1,(z+2.60)/.40))
  def oldy(z):
   if z<=-2.851:return .700+.008*(z+3.317)/.466
   if z<=-2.819:return .708+.168*(z+2.851)/.032
   return .876+.039*(z+2.819)/2.193
  for z0,z1 in zip(zs,zs[1:]):
   for i in range(6):
    x0=-.58+i*(1.16/6);x1=x0+1.16/6
    coords=[(x0,z0),(x0,z1),(x1,z1),(x1,z0)]
    q=[{'p':[x,floory(z),z],'uv':[.225+.05*(x-x0)/.20,.12+.05*(z-z0)/.20],'n':[0,1,0]} for x,z in coords]
    n=(Vector(q[1]['p'])-Vector(q[0]['p'])).cross(Vector(q[2]['p'])-Vector(q[0]['p'])).normalized()
    for v in q:v['n']=list(n)
    add(q)
   for x in [-.58,.58]:
    q=[{'p':[x,y,z],'uv':[.24,.14],'n':[1 if x<0 else -1,0,0]} for z,y in [(z0,oldy(z0)),(z0,floory(z0)),(z1,floory(z1)),(z1,oldy(z1))]]
    if x>0:q.reverse()
    add(q)
 mesh=bpy.data.meshes.new(name+'_Tall');mesh.from_pydata(outv,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);uv=mesh.uv_layers.new(name='UVMap')
 for face in mesh.polygons:
  for li in face.loop_indices:uv.data[li].uv=outu[mesh.loops[li].vertex_index]
 # Blender validates the authored mesh; preserve UV corners and export to existing rig.
 mesh.validate(clean_customdata=False);mesh.update()
 result={'name':name,'vertices':[{'x':v.co.x,'y':v.co.y,'z':v.co.z} for v in mesh.vertices],'uv':[{'x':u[0],'y':u[1]} for u in outu],'normals':[{'x':n[0],'y':n[1],'z':n[2]} for n in outn],'triangles':[i for face in mesh.polygons for i in face.vertices]}
 json.dump(result,open(P/'Output'/(name+'.json'),'w'));report.append(name+': vertices='+str(len(mesh.vertices))+' triangles='+str(len(mesh.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'CamperTallGeometry.blend'));(P/'blender-receipt.txt').write_text('\n'.join(report)+'\nRoof +.40m through new wall band at2.42m; existing upper window and roof coordinates retained relative to each other. Rear door1.90m, unscaled upper door features. Original source/installed assets untouched. Revision2: cut above all window surrounds; deep-copy clipping vertices; independently mapped new wall band; continuous aisle carpet UVs and .72 to .91 floor profile.')




