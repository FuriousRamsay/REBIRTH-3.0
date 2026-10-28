"""Visible pierced feet on a common ground plane; draft reference interpretation."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
records=[
 {'id':'bellows_near_foot','outline':[[230,772],[261,755],[293,760],[315,786],[278,806],[237,791]],'hole':[270,777],'holeRadius':.016,'tabBase':[283,759],'tabTop':[283,731]},
 {'id':'front_left_foot','outline':[[691,991],[722,974],[761,981],[807,1007],[809,1029],[752,1043],[711,1018]],'hole':[758,1010],'holeRadius':.020,'tabBase':[715,985],'tabTop':[708,941]},
 {'id':'front_right_foot','outline':[[1235,845],[1261,832],[1298,836],[1340,854],[1341,876],[1291,890],[1249,873]],'hole':[1292,858],'holeRadius':.019,'tabBase':[1254,847],'tabTop':[1253,796]}
]
def floor(p):
 ray=R*((p[0]-891)/760)+U*((935-p[1])/760)
 return ray-D*(ray[2]/D[2])
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
parts=[]
frame=json.loads((r/'bellows-frame-plan.json').read_text())
attachment=json.loads((r/'attachment-depth-plan.json').read_text())
bands=json.loads((r/'upright-harness-plan.json').read_text())['parts']
targets={
 'bellows_near_foot':np.array(next(p for p in frame['records'] if p['id']=='near_post')['axisEndpoints'][0])+np.array(attachment['bellows']['translation']),
 'front_left_foot':np.array(next(p for p in bands if p['id']=='left_front_strap')['vertices'][-8:]).mean(0),
 'front_right_foot':np.array(next(p for p in bands if p['id']=='right_front_strap')['vertices'][-8:]).mean(0)}
for record in records:
 outline=np.array([floor(p) for p in record['outline']]);center=floor(record['hole']);outer=[];n=64
 for angle in np.linspace(0,math.tau,n,endpoint=False):
  direction=np.array([math.cos(angle),math.sin(angle)])
  distances=[]
  for j,p in enumerate(outline):
   edge=outline[(j+1)%len(outline),:2]-p[:2];relative=p[:2]-center[:2];den=cross(direction,edge)
   if abs(den)<1e-10:continue
   distance=cross(relative,edge)/den;segment=cross(relative,direction)/den
   if distance>0 and 0<=segment<=1:distances.append(distance)
  if not distances:raise ValueError('Foot outline does not surround hole: '+record['id'])
  outer.append(center+np.r_[direction*min(distances),0.])
 vertices=[]
 for height in [0,.014]:
  vertices.extend([(p+np.array([0,0,height])).tolist() for p in outer])
  vertices.extend([(center+np.array([record['holeRadius']*math.cos(t),record['holeRadius']*math.sin(t),height])).tolist() for t in np.linspace(0,math.tau,n,endpoint=False)])
 faces=[]
 for j in range(n):
  k=(j+1)%n
  faces.extend([[j,k,2*n+k,2*n+j],[n+j,3*n+j,3*n+k,n+k],[2*n+j,2*n+k,3*n+k,3*n+j],[j,n+j,n+k,k]])
 parts.append({'id':record['id'],'vertices':vertices,'faces':faces,'source':record,'groundZ':0,'thickness':.014})
 # Upright tab location and height from the pixel rise, preserving true verticality.
 base=floor(record['tabBase']);height=(record['tabBase'][1]-record['tabTop'][1])/(760*math.cos(e))
 side=R*.039;depth=np.array([math.sin(a),math.cos(a),0.])*.009
 tab=[]
 for z in [.014,height]:
  for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]:tab.append((base+x*side+y*depth+np.array([0,0,z])).tolist())
 faces=[[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]]
 parts.append({'id':record['id']+'_upright','vertices':tab,'faces':faces,'height':height,'inferred':'Tab orientation and width; parent attachment not yet connected'})
 start=base+np.array([0,0,height-.004]);end=targets[record['id']];axis=end-start;length=np.linalg.norm(axis);axis/=length
 across=np.cross(axis,D);across/=np.linalg.norm(across);normal=np.cross(axis,across)
 connector=[]
 for p in [start,end]:
  for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]:connector.append((p+across*x*.024+normal*y*.008).tolist())
 parts.append({'id':record['id']+'_connection','vertices':connector,'faces':faces,'endpoints':[start.tolist(),end.tolist()],'length':float(length),'inferred':'Straight mounting strap candidate; exact bent outline requires reference review'})
report={'status':'MOUNTING_FEET_DRAFT_NOT_FIDELITY_PASS','parts':parts,'groundZ':0,
 'limitations':['Manual foot outlines approximate','Tab bases need contact and silhouette inspection','No filleted bend or upright rivets yet','Frame bottom and furnace straps need actual connections','Hidden mounts not inferred yet'],'blenderAllowed':False}
(r/'mounting-feet-plan.json').write_text(json.dumps(report));print('Generated',len(parts),'foot and tab solids')
