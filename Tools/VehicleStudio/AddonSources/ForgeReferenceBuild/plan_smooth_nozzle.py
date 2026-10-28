"""Continuous neck candidate with flange-constrained terminal tangent."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
old=json.loads((r/'nozzle-sweep-plan.json').read_text())
flange=json.loads((r/'flange-orientation-audit.json').read_text())
camera=old['camera'];a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
ratio=flange['draftTraceFittedMinorMajorRatio'];theta=flange['fittedEllipseCenterRadiiRotation'][4]
# Ellipse alone admits two depth signs. Entry must approach the visible face,
# so the terminal direction into the flange points away from the observer.
normal=math.sqrt(1-ratio**2)*(math.cos(theta)*R-math.sin(theta)*U)-ratio*D
p0=np.array(old['sections'][0]['center']);p3=np.array(old['sections'][-1]['center'])
t0=np.array(old['sections'][0]['tangent']);length=np.linalg.norm(p3-p0)
original_end=p3.copy();candidates=[]
for shift in np.linspace(-.16,0,17):
 end=original_end+D*shift;span=np.linalg.norm(end-p0)
 for first in [.25,.4,.55,.7]:
  for last in [.25,.4,.55,.7]:
   c1=p0+t0*span*first;c2=end-normal*span*last
   maximum=0.
   for t in np.linspace(0,1,81):
    velocity=3*(1-t)**2*(c1-p0)+6*(1-t)*t*(c2-c1)+3*t*t*(end-c2)
    acceleration=6*(1-t)*(c2-2*c1+p0)+6*t*(end-2*c2+c1)
    radius=(22+2*t+1.5*math.sin(math.pi*t))/760
    maximum=max(maximum,float(np.linalg.norm(np.cross(velocity,acceleration))/max(np.linalg.norm(velocity)**3,1e-12)*radius))
   if maximum<.7:candidates.append((abs(shift)+.01*maximum,shift,maximum,c1,c2,end))
if not candidates:raise RuntimeError('No nonfolding connection candidate; revise attachment plan')
_,depth_shift,search_curvature,p1,p2,p3=min(candidates,key=lambda c:c[0])
(r/'nozzle-depth-inference.json').write_text(json.dumps({'status':'INFERRED_NOT_FIDELITY_PASS','depthShift':float(depth_shift),'maxRadiusTimesCurvature':search_curvature,'reason':'Choose nearest tested hidden depth with radius-curvature below 0.7 and unchanged endpoint image location','remaining':'Furnace contact and complete reference silhouette unvalidated'},indent=2))
sections=[];curvature=[]
for t in np.linspace(0,1,41):
 center=(1-t)**3*p0+3*(1-t)**2*t*p1+3*(1-t)*t*t*p2+t**3*p3
 velocity=3*(1-t)**2*(p1-p0)+6*(1-t)*t*(p2-p1)+3*t*t*(p3-p2)
 acceleration=6*(1-t)*(p2-2*p1+p0)+6*t*(p3-2*p2+p1)
 tangent=velocity/np.linalg.norm(velocity);side=np.cross(tangent,D);side/=np.linalg.norm(side);other=np.cross(tangent,side)
 radius=(22+2*t+1.5*math.sin(math.pi*t))/760
 curvature.append(float(np.linalg.norm(np.cross(velocity,acceleration))/np.linalg.norm(velocity)**3*radius))
 ring=[(center+radius*(math.cos(s)*side+math.sin(s)*other)).tolist() for s in np.linspace(0,math.tau,48,endpoint=False)]
 sections.append({'t':float(t),'center':center.tolist(),'radius':radius,'ringVertices':ring})
im=Image.new('RGB',(900,700),'#f4f4ef');d=ImageDraw.Draw(im)
def xy(v):v=np.array(v);return ((891+760*(R@v)-495)*5+40,(935-760*(U@v)-480)*5+50)
for j in range(0,48,4):d.line([xy(s['ringVertices'][j]) for s in sections],fill='#176b99',width=2)
for s in sections[::10]:d.line([xy(v) for v in s['ringVertices']+[s['ringVertices'][0]]],fill='#a96028',width=2)
d.text((15,15),'Smooth nozzle planning projection; flange-constrained end tangent',fill='black')
d.text((15,665),'Inferred depth; reference silhouette and attachment fit still unvalidated.',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');im.save(out/'nozzle-smooth-plan.png')
report={'status':'SMOOTH_CONNECTION_CANDIDATE_NOT_FIDELITY_PASS','controlPoints':[p.tolist() for p in [p0,p1,p2,p3]],'terminalNormal':normal.tolist(),'projectedFlangeRatio':abs(float(normal@D)),'maxRadiusTimesCurvature':max(curvature),'localSweepFoldRisk':max(curvature)>=1,'sections':sections,'limitations':['Normal derives from draft outer flange trace including thickness','Absolute assembly attachment depth unresolved','Visual reference silhouette match not established'],'blenderAllowed':False}
(r/'nozzle-smooth-plan.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='sections'},indent=2))
