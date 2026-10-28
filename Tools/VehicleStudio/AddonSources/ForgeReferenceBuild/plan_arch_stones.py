"""Closed individual arch-stone planning solids from traced irregular outlines."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text());traces=json.loads((r/'arch-stone-records.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
def lift(p,depth):return (R*((p[0]-891)/760)+U*((935-p[1])/760)+D*depth).tolist()
parts=[]
parameters=json.loads((r/'arch-face-parameters.json').read_text())['stones']
for record in traces['records']:
 outline=np.array(record['contourPixels'],float);center=outline.mean(0);n=len(outline);vertices=[]
 settings=parameters[record['id']];slopes=np.tan(np.radians(settings['tilt']))
 def face_depth(point,depth):
  delta=(point-center)/760
  return depth+(settings['depth']-.063)+delta[0]*slopes[0]-delta[1]*slopes[1]
 # Broad face bounded by chamfered transitions; depth is a declared hypothesis.
 profiles=[(.92,-.12),(1,.020),(.975,.040),(.93,.055),(.86,.063)]
 for shrink,depth in profiles:
  for point in outline:
   p=center+(point-center)*shrink;vertices.append(lift(p,face_depth(p,depth)))
 vertices.append(lift(center,settings['depth']));faces=[]
 for row in range(len(profiles)-1):
  for j in range(n):faces.append([row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j])
 front=(len(profiles)-1)*n
 for j in range(n):faces.append([front+j,front+(j+1)%n,len(profiles)*n])
 faces.append(list(range(n-1,-1,-1)))
 parts.append({'id':record['id'],'vertices':vertices,'faces':faces,'neighbours':record['neighbours'],'depthRange':[-.12,.063],'frontBevelFraction':.14,'faceProfile':'broad planar face with four-stage rounded transition; fracture planes pending'})
report={'status':'INDIVIDUAL_ARCH_STONE_VOLUMES_NOT_FIDELITY_PASS','parts':parts,'limitations':['Contours are draft observations and must be rechecked against original','Uniform provisional depth must conform to furnace surface','Irregular fracture planes and inter-stone contact remain unfinished','Hearth opening may conflict with traced stones; inspect combined view'],'blenderAllowed':False}
(r/'arch-stones-plan.json').write_text(json.dumps(report));print('Planned %d individually outlined stones.'%len(parts))
