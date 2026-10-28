"""Hollow crucible and collar volume plans from corrected image-space dimensions."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
def lift(x,y,depth):return R*((x-891)/760)+U*((935-y)/760)+D*depth
def revolve(name,center,profile):
 n=96;vertices=[]
 for radius,height in profile:
  for t in np.linspace(0,math.tau,n,endpoint=False):vertices.append((center+np.array([radius*math.cos(t),radius*math.sin(t),height])).tolist())
 faces=[]
 for i in range(len(profile)):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,((i+1)%len(profile))*n+(j+1)%n,((i+1)%len(profile))*n+j])
 return {'id':name,'center':center.tolist(),'axis':[0,0,1],'radialHeightProfile':profile,'vertices':vertices,'faces':faces}
# Height zero is the top lip plane. The crucible's outer lip overhangs its body.
cup_profile=[(.127,-.330),(.135,-.315),(.141,-.080),(.151,-.045),(.154,-.018),(.149,0),(.116,0),(.114,-.030),(.105,-.298),(0,-.298),(0,-.330)]
collar_profile=[(.145,-.110),(.150,-.100),(.150,-.012),(.145,0),(.120,0),(.118,-.012),(.118,-.110)]
parts=[revolve('crucible',lift(555.5,664.75,.08),cup_profile),revolve('top_collar',lift(891,103,-.20),collar_profile)]
report={'status':'ROUND_COMPONENTS_PLANNED_NOT_FIDELITY_PASS','parts':parts,'authority':'Rim sizes from traces; depths and profiles inferred. No prior model reused.','limitations':['Crucible pole vertices require welding','Lip stone irregularity and external ironwork not included','Collar placement against crown needs contact check','Global ground plane and furnace bulk depth remain provisional'],'blenderAllowed':False}
(r/'round-components-plan.json').write_text(json.dumps(report));print('Created hollow crucible and open collar profiles.')
