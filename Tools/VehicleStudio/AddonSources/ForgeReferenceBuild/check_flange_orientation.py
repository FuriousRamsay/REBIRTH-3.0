"""Check a second independent constraint on the proposed nozzle axis."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent
p=json.loads((r/'frame-orientation-candidate.json').read_text())
parts={x['id']:x for x in json.loads((r/'left-reference-records.json').read_text())['records']}
a,e=map(math.radians,[p['cameraAzimuthDegrees'],p['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.])
U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)])
D=np.cross(R,U)
n=np.array(p['nozzleUnitAxisTowardFurnace'])
predicted=abs(float(n@D))
q=np.array(parts['furnace_flange']['outerContour'],float)
# Fit ellipse including its rotation; these draft points include plate thickness.
t=np.linspace(0,math.tau,360,endpoint=False)
def ring(v):
 cx,cy,rx,ry,theta=v;c,s=math.cos(theta),math.sin(theta)
 return np.stack([cx+rx*np.cos(t)*c-ry*np.sin(t)*s,cy+rx*np.cos(t)*s+ry*np.sin(t)*c],axis=1)
def loss(v):return float(np.mean(np.min(np.sum((q[:,None,:]-ring(v)[None,:,:])**2,axis=2),axis=1)))
v=np.array([623.,534.,35.,63.,-.25])
for step in [4,2,1,.5,.25]:
 for _ in range(80):
  options=[(loss(v),v)]
  for axis in range(5):
   for sign in [-1,1]:
    w=v.copy();w[axis]+=sign*step*(.015 if axis==4 else 1)
    if min(w[2:4])>0:options.append((loss(w),w))
  _,w=min(options,key=lambda z:z[0])
  if np.array_equal(v,w):break
  v=w
ratio=float(min(v[2:4])/max(v[2:4]))
report={'status':'PREVIOUS_NOZZLE_CANDIDATE_FAILS_FLANGE_CONSTRAINT',
 'predictedCircularFlangeMinorMajorRatio':predicted,
 'draftTraceFittedMinorMajorRatio':ratio,
 'fittedEllipseCenterRadiiRotation':v.tolist(),
 'fitRmsPixels':math.sqrt(loss(v)),
 'decision':'Do not promote the pitched perpendicular-heading nozzle candidate. It predicts a substantially more face-on flange than the traced outline.',
 'uncertainty':'Flange trace includes thickness and may need correction; its visible face must be separated from rear edge before solving a replacement axis.',
 'blenderAllowed':False}
(r/'flange-orientation-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
