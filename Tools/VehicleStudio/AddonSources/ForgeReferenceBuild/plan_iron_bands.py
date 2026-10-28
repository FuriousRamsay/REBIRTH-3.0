"""Closed ribbon solids following traced harness paths on provisional furnace mass."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text())
traces=json.loads((r/'harness-reference-recheck.json').read_text());mass=json.loads((r/'furnace-mass-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']]);R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
surface=np.array(mass['vertices'][:512]);pixels=np.stack([891+760*(surface@R),935-760*(surface@U)],axis=1);depths=surface@D
triangles=[]
stones=json.loads((r/'arch-stones-plan.json').read_text())
outer=json.loads((r/'outer-stones-plan.json').read_text())
for obj in [mass]+stones['parts']+outer['parts']:
 v=np.array(obj['vertices'])
 for face in obj['faces']:
  for i in range(1,len(face)-1):triangles.append(v[[face[0],face[i],face[i+1]]])
triangles=np.array(triangles);tp=np.stack([891+760*(triangles@R),935-760*(triangles@U)],axis=2);td=triangles@D
x0,y0=tp[:,0,0],tp[:,0,1];x1,y1=tp[:,1,0],tp[:,1,1];x2,y2=tp[:,2,0],tp[:,2,1]
den=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2);valid=np.abs(den)>1e-8
safe=np.where(valid,den,1);fallbacks=[]
def depth_at(p):
 w0=((y1-y2)*(p[0]-x2)+(x2-x1)*(p[1]-y2))/safe
 w1=((y2-y0)*(p[0]-x2)+(x0-x2)*(p[1]-y2))/safe;w2=1-w0-w1
 inside=valid&(w0>=-1e-8)&(w1>=-1e-8)&(w2>=-1e-8)
 if np.any(inside):return float(np.max((w0*td[:,0]+w1*td[:,1]+w2*td[:,2])[inside]))
 fallbacks.append(p.tolist())
 distances=np.linalg.norm(pixels-p,axis=1);ids=np.argsort(distances)[:8];weights=1/np.maximum(distances[ids],1)**2
 return float(np.sum(depths[ids]*weights)/sum(weights))
def lift(p,depth):return (R*((p[0]-891)/760)+U*((935-p[1])/760)+D*depth).tolist()
parts=[]
for trace in traces['records']:
 if 'pathPixels' not in trace:continue
 path=np.array(trace['pathPixels'],float);samples=[]
 for i in range(len(path)-1):
  for t in np.linspace(0,1,9,endpoint=False):samples.append((1-t)*path[i]+t*path[i+1])
 samples.append(path[-1]);vertices=[]
 offset=.008+(.018 if trace['id']=='left_front_strap' else 0)
 for i,p in enumerate(samples):
  tangent=samples[min(i+1,len(samples)-1)]-samples[max(i-1,0)];normal=np.array([-tangent[1],tangent[0]])/np.linalg.norm(tangent)
  for thickness in [0,.012]:
   for side in [-1,1]:
    q=p+normal*side*trace['widthPixels']/2;vertices.append(lift(q,depth_at(q)+offset+thickness))
 faces=[]
 for i in range(len(samples)-1):
  for j,k in [(0,1),(1,3),(3,2),(2,0)]:faces.append([4*i+j,4*i+k,4*(i+1)+k,4*(i+1)+j])
 faces.extend([[0,2,3,1],[len(vertices)-4,len(vertices)-3,len(vertices)-1,len(vertices)-2]])
 parts.append({'id':trace['id'],'vertices':vertices,'faces':faces,'thickness':.012,'sourcePath':trace['pathPixels'],'projectedWidthPixels':trace['widthPixels'],'depthOffsetAboveBulk':offset})
report={'status':'HARNESS_PATH_VOLUMES_NOT_FIDELITY_PASS','parts':parts,'surfaceMethod':'Projected triangle intersection with frontmost planned bulk or arch stone','unsupportedSampleCount':len(fallbacks),'unsupportedSamplePixels':fallbacks,'limitations':['Paths inherited from unvalidated manual trace','Samples outside supporting geometry still use fallback; foot transitions unresolved','Crossing crown band raised but junctions require inspection','Beveled edges and rivets not included'],'blenderAllowed':False}
(r/'iron-bands-plan.json').write_text(json.dumps(report));print('Planned %d band solids.'%len(parts))
