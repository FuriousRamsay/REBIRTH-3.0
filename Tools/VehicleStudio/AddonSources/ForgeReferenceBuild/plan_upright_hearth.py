"""Physical horizontal hearth prism subtracted from upright envelope.
Numeric planning geometry only; triangulation/welding and stone lining remain.
"""
import json
from pathlib import Path
import numpy as np
from audit_upright_surface import hit
r=Path(__file__).parent
body=json.loads((r/'upright-furnace-envelope.json').read_text());v=np.array(body['vertices'])
record=next(p for p in json.loads((r/'furnace-reference-records.json').read_text())['records'] if p['id']=='arch_void')
hits=[hit(np.array(p)) for p in record['contourPixels']]
if any(h is None for h in hits):raise ValueError('Opening trace unsupported by upright envelope')
points=np.array([h['position'] for h in hits]);center=np.array(body['center'])
out=points.mean(0)-center;out[2]=0;out/=np.linalg.norm(out)
up=np.array([0.,0.,1.]);side=np.cross(up,out)
def hull(points):
 pts=sorted(set(map(tuple,points)));low=[];high=[]
 def cross(o,a,b):return np.cross(np.array(a)-o,np.array(b)-o).item()
 for p in pts:
  while len(low)>1 and cross(low[-2],low[-1],p)<=0:low.pop()
  low.append(p)
 for p in reversed(pts):
  while len(high)>1 and cross(high[-2],high[-1],p)<=0:high.pop()
  high.append(p)
 return np.array(low[:-1]+high[:-1])
profile=hull(np.column_stack([points@side,points@up]))
front_depth=float(np.median(points@out));back_depth=front_depth-.30
planes=[]
for i,p in enumerate(profile):
 q=profile[(i+1)%len(profile)];edge=q-p;n=np.array([edge[1],-edge[0]])
 normal=side*n[0]+up*n[1];planes.append((normal,float(n@p)))
planes.append((-out,-back_depth))
def clip(poly,normal,limit,inside=True):
 result=[]
 for i,a in enumerate(poly):
  b=poly[(i+1)%len(poly)];da=float(a@normal-limit);db=float(b@normal-limit)
  keep_a=da<=1e-10 if inside else da>=-1e-10
  keep_b=db<=1e-10 if inside else db>=-1e-10
  if keep_a:result.append(a)
  if keep_a!=keep_b and abs(da-db)>1e-12:result.append(a+(b-a)*da/(da-db))
 return result
polygons=[]
for face in body['faces']:
 remaining=list(v[face])
 for normal,limit in planes:
  if len(remaining)<3:break
  outside=clip(remaining,normal,limit,False)
  if len(outside)>=3:polygons.append(outside)
  remaining=clip(remaining,normal,limit,True)
# Ray intersections in the physical opening's side/Z basis.
triangles=[]
for face in body['faces']:
 for i in range(1,len(face)-1):triangles.append(v[[face[0],face[i],face[i+1]]])
t=np.array(triangles);xy=np.stack([t@side,t@up],axis=2);depth=t@out
x0,y0=xy[:,0,0],xy[:,0,1];x1,y1=xy[:,1,0],xy[:,1,1];x2,y2=xy[:,2,0],xy[:,2,1]
den=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2);valid=abs(den)>1e-10;safe=np.where(valid,den,1)
def surface_depth(p):
 w0=((y1-y2)*(p[0]-x2)+(x2-x1)*(p[1]-y2))/safe;w1=((y2-y0)*(p[0]-x2)+(x0-x2)*(p[1]-y2))/safe;w2=1-w0-w1
 inside=valid&(w0>=-1e-8)&(w1>=-1e-8)&(w2>=-1e-8)
 if not inside.any():raise ValueError('Opening boundary exits shell')
 z=(w0*depth[:,0]+w1*depth[:,1]+w2*depth[:,2])[inside]
 return float(z.max()),float(z.min())
front=[];back=[];remaining_wall=[]
for p in profile:
 f,b=surface_depth(p);front.append(side*p[0]+up*p[1]+out*f);back.append(side*p[0]+up*p[1]+out*back_depth);remaining_wall.append(back_depth-b)
for i in range(len(profile)):polygons.append([front[i],front[(i+1)%len(profile)],back[(i+1)%len(profile)],back[i]])
polygons.append(back[::-1]);vertices=[];faces=[]
for polygon in polygons:
 faces.append(list(range(len(vertices),len(vertices)+len(polygon))));vertices.extend([p.tolist() for p in polygon])
report={'status':'PHYSICAL_HEARTH_PLAN_NOT_FIDELITY_PASS','vertices':vertices,'faces':faces,'outwardAxis':out.tolist(),
 'chamberDepth':.30,'frontDepth':front_depth,'backDepth':back_depth,'openingProfileSideZ':profile.tolist(),
 'minimumSampledRearWallThickness':min(remaining_wall),'floorHeight':float(profile[:,1].min()),
 'limitations':['Opening profile is convex hull of provisional trace','Rear wall thickness sampled only at profile vertices','Clipped boundary needs welding and topology validation','Stone lining, coals and exact sill shapes missing','Depth is inferred, not measured'],
 'blenderAllowed':False}
(r/'upright-hearth-plan.json').write_text(json.dumps(report))
print(json.dumps({k:v for k,v in report.items() if k not in ['vertices','faces','openingProfileSideZ']},indent=2))
