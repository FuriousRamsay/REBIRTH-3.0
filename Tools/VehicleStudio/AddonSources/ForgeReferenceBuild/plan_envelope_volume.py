"""Generate closed planning loft and orthogonal diagnostic views, not a Blender asset."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
spec=json.loads((r/'bellows-envelope-spec.json').read_text())
frame=json.loads((r/'bellows-frame-plan.json').read_text())
a,e=map(math.radians,[frame['cameraAzimuthDegrees'],frame['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
def lift(pixel,depth):return R*((pixel[0]-891)/760)+U*((935-pixel[1])/760)+D*depth
anchors=spec['sectionMeasurements'];rings=[]
traces=json.loads((r/'left-reference-records.json').read_text())
ridge_paths=next(p for p in traces['records'] if p['id']=='fold_envelope')['foldRidgePaths']
def smooth(values,t):
 values=np.array(values,float);i=min(4,int(t*5));f=t*5-i
 deltas=np.diff(values,axis=0);slopes=np.zeros_like(values)
 slopes[0]=deltas[0];slopes[-1]=deltas[-1]
 for j in range(1,len(values)-1):
  same=deltas[j-1]*deltas[j]>0
  denominator=deltas[j-1]+deltas[j]
  slopes[j]=np.divide(2*deltas[j-1]*deltas[j],denominator,out=np.zeros_like(denominator),where=same)
 return (2*f**3-3*f*f+1)*values[i]+(f**3-2*f*f+f)*slopes[i]+(-2*f**3+3*f*f)*values[i+1]+(f**3-f*f)*slopes[i+1]
for t in np.linspace(0,1,41):
 i=min(4,int(t*5));f=t*5-i
 lo,hi=anchors[i:i+2]
 top=smooth([s['upperPixel'] for s in anchors],t)
 bottom=smooth([s['lowerPixel'] for s in anchors],t)
 width=float(smooth([s['depthHalfWidth'] for s in anchors],t))
 ridge_fractions=[]
 for path in ridge_paths:
  x=sum([top[0],bottom[0]])/2
  y=float(np.interp(x,[p[0] for p in path],[p[1] for p in path]))
  ridge_fractions.append(float(np.clip((y-top[1])/(bottom[1]-top[1]),0,1)))
 top3,bottom3=lift(top,0),lift(bottom,0);center=(top3+bottom3)/2;vertical=(top3-bottom3)/2
 # Closed elliptical section, with shallow continuous depth corrugation.
 ring=[]
 for angle in np.linspace(0,math.tau,64,endpoint=False):
  v=math.cos(angle);fraction=(1-v)/2
  bulge=sum(math.exp(-((fraction-ridge)/.055)**2) for ridge in ridge_fractions)
  fold=1+(1-t)*(.09*bulge-.035)
  ring.append((center+v*vertical+D*(math.sin(angle)*width*fold)).tolist())
 rings.append(ring)
vertices=[v for ring in rings for v in ring];faces=[]
for i in range(40):
 for j in range(64):faces.append([i*64+j,i*64+(j+1)%64,(i+1)*64+(j+1)%64,(i+1)*64+j])
faces.extend([list(range(63,-1,-1)),list(range(40*64,41*64))])
edges={}
for face in faces:
 for j in range(len(face)):
  key=tuple(sorted((face[j],face[(j+1)%len(face)])));edges[key]=edges.get(key,0)+1
im=Image.new('RGB',(1400,800),'#f4f4ef');d=ImageDraw.Draw(im)
def xy(v,side=False):
 v=np.array(v)
 if side:return (1080+float(D@v)*1500,710-(float(U@v)-.3)*1000)
 return (80+(891+760*float(R@v)-230)*1.8,80+(935-760*float(U@v)-320)*1.8)
for side in [False,True]:
 for j in range(0,64,8):d.line([xy(ring[j],side) for ring in rings],fill='#217b91',width=2)
 for ring in rings[::8]:d.line([xy(v,side) for v in ring+[ring[0]]],fill='#97642c',width=2)
d.text((20,20),'Bellows volume plan: reference projection (left), depth diagnostic (right)',fill='black')
d.text((20,760),'Continuous closed loft. Depth inferred; fold/ridge fidelity and assembly fit not accepted.',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');im.save(out/'bellows-volume-plan-v2.png')
report={'status':'CLOSED_LOFT_CANDIDATE_NOT_FIDELITY_PASS','revision':2,'interpolation':'monotone cubic Hermite shoulder; ridges positioned from traced reference paths','vertices':vertices,'faces':faces,'topology':{'vertexCount':len(vertices),'faceCount':len(faces),'edgesWithOtherThanTwoFaces':sum(n!=2 for n in edges.values())},'limitations':['Current side depth centered on provisional view plane; must anchor within frame','Trace-derived ridges still require visual inspection against original','Boards and seam piping not included'],'blenderAllowed':False}
(r/'bellows-volume-plan-v2.json').write_text(json.dumps(report))
print(json.dumps(report['topology']))
