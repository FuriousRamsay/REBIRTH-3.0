"""Locate observed stone centers on upright volume; report unsupported traces."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent
shape=json.loads((r/'upright-furnace-envelope.json').read_text());camera=json.loads((r/'bellows-frame-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
v=np.array(shape['vertices']);triangles=[]
for face in shape['faces']:
 for i in range(1,len(face)-1):triangles.append(v[[face[0],face[i],face[i+1]]])
t=np.array(triangles);screen=np.stack([891+760*(t@R),935-760*(t@U)],axis=2);depth=t@D
x0,y0=screen[:,0,0],screen[:,0,1];x1,y1=screen[:,1,0],screen[:,1,1];x2,y2=screen[:,2,0],screen[:,2,1]
den=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2);valid=abs(den)>1e-9;safe=np.where(valid,den,1)
def hit(p):
 w0=((y1-y2)*(p[0]-x2)+(x2-x1)*(p[1]-y2))/safe
 w1=((y2-y0)*(p[0]-x2)+(x0-x2)*(p[1]-y2))/safe;w2=1-w0-w1
 inside=valid&(w0>=0)&(w1>=0)&(w2>=0)
 if not inside.any():return None
 z=w0*depth[:,0]+w1*depth[:,1]+w2*depth[:,2];z[~inside]=-np.inf;i=int(np.argmax(z))
 normal=np.cross(t[i,1]-t[i,0],t[i,2]-t[i,0]);normal/=np.linalg.norm(normal)
 if normal@D<0:normal=-normal
 point=R*((p[0]-891)/760)+U*((935-p[1])/760)+D*z[i]
 return {'position':point.tolist(),'normal':normal.tolist(),'triangle':i}
records=[]
for file,key,contour in [('outer-stone-contours.json','stones','contour'),('masonry-gap-records.json','stones','contour'),('arch-stone-records.json','records','contourPixels')]:
 for stone in json.loads((r/file).read_text())[key]:
  p=np.array(stone[contour]);center=p.mean(0)
  records.append({'id':stone['id'],'referenceCenter':center.tolist(),'surface':hit(center),'unsupportedContourPoints':[q.tolist() for q in p if hit(q) is None]})
report={'status':'SURFACE_ATTACHMENT_AUDIT_NOT_FIDELITY_PASS','records':records,'unsupportedCenters':[p['id'] for p in records if p['surface'] is None],
 'contourPointFailures':sum(len(p['unsupportedContourPoints']) for p in records),
 'remaining':'Normal/position only; no stone construction, contact or cavity test'}
(r/'upright-surface-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='records'},indent=2))
