"""Silhouette-constrained furnace bulk with a recessed hearth; hidden depth inferred."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text())
records={p['id']:p for p in json.loads((r/'furnace-reference-records.json').read_text())['records']}
records['body_silhouette']['contourPixels']=json.loads((r/'furnace-silhouette-recheck.json').read_text())['contourPixels']
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
origin=np.array([1070.,550.])
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def boundary(poly,direction):
 hits=[]
 for i,p in enumerate(poly):
  p=np.array(p,float);v=np.array(poly[(i+1)%len(poly)],float)-p;den=cross(direction,v)
  if abs(den)<1e-9:continue
  t=cross(p-origin,v)/den;s=cross(p-origin,direction)/den
  if t>0 and 0<=s<=1:hits.append(t)
 if not hits:raise ValueError('Contour is not radial about hearth center')
 return origin+direction*min(hits)
def lift(p,depth):return (R*((p[0]-891)/760)+U*((935-p[1])/760)+D*depth).tolist()
n=128;outer=[];inner=[]
for t in np.linspace(0,math.tau,n,endpoint=False):
 direction=np.array([math.cos(t),math.sin(t)])
 outer.append(boundary(records['body_silhouette']['contourPixels'],direction));inner.append(boundary(records['arch_void']['contourPixels'],direction))
vertices=[]
for weight,depth in [(0,-.35),(.33,-.10),(.66,.015),(1,.035)]:
 for p,q in zip(outer,inner):vertices.append(lift((1-weight)*p+weight*q,depth))
# A real hearth enters horizontally; camera-ray extrusion hides all reveal faces.
# Azimuth is inferred opposite the horizontal viewing direction, pending side review.
inward=-D.copy();inward[2]=0;inward/=np.linalg.norm(inward)
for p in inner:vertices.append((np.array(lift(p,.035))+inward*.335).tolist())
for p in outer:vertices.append(lift(p,-.55))
faces=[]
for first,second in [(0,1),(1,2),(2,3),(3,4),(5,0)]:
 for j in range(n):faces.append([first*n+j,first*n+(j+1)%n,second*n+(j+1)%n,second*n+j])
faces.extend([list(range(4*n,5*n)),list(range(6*n-1,5*n-1,-1))])
report={'status':'FURNACE_BULK_PLAN_NOT_MASONRY_OR_FIDELITY_PASS','vertices':vertices,'faces':faces,'basis':'Shared frame camera basis','depthAuthority':'Depth rings inferred to establish a volumetric hearth and body; not measured from concept','hearthRecessDepth':.335,'limitations':['Draft silhouette and opening contours require corrected visual review','Bulk is an envelope for individually shaped stones, not the finished stone surface','Back and side dimensions must be checked against attachments','Front-surface depth profile must be assessed in secondary view'],'blenderAllowed':False}
(r/'furnace-mass-plan.json').write_text(json.dumps(report));print('Generated furnace bulk: %d vertices, %d faces.'%(len(vertices),len(faces)))
