"""Connected frame dimensions from shared camera; hidden depth explicitly inferred."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent
camera=json.loads((r/'frame-orientation-candidate.json').read_text())
parts={p['id']:p for p in json.loads((r/'left-reference-records.json').read_text())['records']}
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
def lift(pixel,depth):return R*((pixel[0]-891)/760)+U*((935-pixel[1])/760)+D*depth
def project(v):return [891+760*float(R@v),935-760*float(U@v)]
upper=lift(parts['upper_shaft']['axisPixels'][0],0)
# Keep posts in the same Y plane; solve lower point using X and Z.
q=parts['lower_shaft']['axisPixels'][0]
x=((q[0]-891)/760-R[1]*upper[1])/R[0]
z=((935-q[1])/760-U[0]*x-U[1]*upper[1])/U[2]
lower=np.array([x,upper[1],z])
length=205/(760*R[0]);offset=np.array([length,0,0])
up_axis=(upper-lower);height=np.linalg.norm(up_axis);up_axis/=height
width=44/760;depth=.065
records=[]
for name,start,end,diameter in [('upper_shaft',upper,upper+offset,25/760),('lower_shaft',lower,lower+offset,24/760)]:
 records.append({'id':name,'shape':'stepped round shaft','axisEndpoints':[start.tolist(),end.tolist()],'diameter':diameter,'length':float(np.linalg.norm(end-start)),'connections':['near_post_bearing','far_post_bearing']})
for name,translation in [('near_post',np.zeros(3)),('far_post',offset)]:
 start=lower+translation-up_axis*.05;end=upper+translation+up_axis*.045
 records.append({'id':name,'shape':'beveled rectangular timber','axisEndpoints':[start.tolist(),end.tolist()],'crossSection':[width,depth],'bevelRadius':.007,'axisUnit':up_axis.tolist(),'connections':['upper_shaft','lower_shaft'],'hiddenGeometry':'Far lower post partly occluded; extend coherently to lower shaft, not traced as visible'})
for i,center in enumerate([upper,upper+offset,lower,lower+offset]):
 records.append({'id':f'bearing_{i}','shape':'washer, short bearing boss and domed head','center':center.tolist(),'axis':[1,0,0],'washerOuterRadius':.028,'washerThickness':.009,'bossRadius':.020,'bossLength':.016,'headDomeHeight':.010})
errors={}
for name,start in [('upper_shaft',upper),('lower_shaft',lower)]:
 observed=parts[name]['axisPixels'][1];predicted=project(start+offset)
 errors[name]={'predictedFarEndpoint':predicted,'observedFarEndpoint':observed,'distancePixels':math.dist(predicted,observed)}
report={'status':'CONNECTED_FRAME_DIMENSION_PLAN_NOT_FULL_FIDELITY_PASS','cameraElevationDegrees':math.degrees(e),'cameraAzimuthDegrees':math.degrees(a),'units':'same provisional units as nozzle; 760 pixels/unit','records':records,'endpointChecks':errors,'postLeanFromVerticalDegrees':math.degrees(math.acos(up_axis[2])),'depthAuthority':'Near upper bearing depth fixed to zero as a provisional assembly datum; full forge attachment remains unresolved','remaining':['Verify post outlines including thickness','Fit leather envelope between frame and throat','Add lower bracket and grounded foot','Check far-post visibility and common support height'],'blenderAllowed':False}
(r/'bellows-frame-plan.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='records'},indent=2))
