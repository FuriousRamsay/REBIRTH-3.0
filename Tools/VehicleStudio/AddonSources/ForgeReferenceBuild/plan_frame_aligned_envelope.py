"""Bellows envelope aligned to physical frame X, preserving rejected prior candidates."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
spec=json.loads((r/'bellows-envelope-spec.json').read_text());frame=json.loads((r/'bellows-frame-plan.json').read_text());neck=json.loads((r/'nozzle-smooth-plan.json').read_text())
trace=json.loads((r/'left-reference-records.json').read_text())
ridge_paths=next(p for p in trace['records'] if p['id']=='fold_envelope')['foldRidgePaths']
a,e=map(math.radians,[frame['cameraAzimuthDegrees'],frame['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)])
posts=[p for p in frame['records'] if p['id'] in ['near_post','far_post']]
near=float(np.mean([p[0] for p in posts[0]['axisEndpoints']]))+posts[0]['crossSection'][0]/2+.006
far=float(np.mean([p[0] for p in posts[1]['axisEndpoints']]))-posts[1]['crossSection'][0]/2-.028
end_center=neck['controlPoints'][0][0];end_width=.058
def on_x(pixel,x):
 y=(R[0]*x-(pixel[0]-891)/760)/(-R[1]);z=((935-pixel[1])/760-U[0]*x-U[1]*y)/U[2]
 return np.array([x,y,z])
def project(v):v=np.array(v);return (891+760*float(R@v),935-760*float(U@v))
def power(v):return math.copysign(abs(v)**.35,v)
rings=[]
for i in range(41):
 t=i/40;k=min(4,int(t*5));f=t*5-k;lo,hi=spec['sectionMeasurements'][k:k+2]
 x=(1-t)*near+t*(end_center-end_width/2);width=(1-t)*(far-near)+t*end_width
 upper=on_x((1-f)*np.array(lo['upperPixel'])+f*np.array(hi['upperPixel']),x)
 lower=on_x((1-f)*np.array(lo['lowerPixel'])+f*np.array(hi['lowerPixel']),x)
 center=(upper+lower)/2+np.array([width/2,0,0]);vertical=(upper-lower)/2
 ring=[]
 for s in np.linspace(0,math.tau,256,endpoint=False):
  vertex=center+power(math.cos(s))*vertical+np.array([width/2*power(math.sin(s)),0,0])
  px,py=project(vertex)
  ridges=[float(np.interp(px,[p[0] for p in path],[p[1] for p in path])) for path in ridge_paths]
  bulge=sum(math.exp(-((py-y)/15)**2) for y in ridges)
  # Suppress at both attachments; relief is geometry, not a texture substitute.
  relief=.024*math.sin(math.pi*t)**2*bulge*abs(math.sin(s))
  vertex[0]+=math.copysign(relief,math.sin(s))
  ring.append(vertex.tolist())
 rings.append(ring)
vertices=[v for ring in rings for v in ring];faces=[]
for i in range(40):
 for j in range(256):faces.append([i*256+j,i*256+(j+1)%256,(i+1)*256+(j+1)%256,(i+1)*256+j])
faces.extend([list(range(255,-1,-1)),list(range(10240,10496))])
im=Image.new('RGB',(1000,900),'#f4f4ef');d=ImageDraw.Draw(im)
def screen(v):x,y=project(v);return ((x-130)*1.6+20,(y-200)*1.6+50)
for post in posts:d.line([screen(v) for v in post['axisEndpoints']],fill='#98642c',width=round(post['crossSection'][0]*760*1.6))
for j in range(0,256,8):d.line([screen(ring[j]) for ring in rings],fill='#167c96',width=2)
for ring in rings[::8]:d.line([screen(v) for v in ring+[ring[0]]],fill='#496474',width=2)
d.text((20,15),'Frame-aligned bellows with shallow reference-guided folds; boards pending',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');im.save(out/'bellows-frame-aligned.png')
report={'status':'FRAME_ALIGNED_BASE_VOLUME_REQUIRES_REVIEW','rearXBounds':[min(v[0] for v in rings[0]),max(v[0] for v in rings[0])],'nominalPostInsets':{'near':.006,'far':.028},'vertices':vertices,'faces':faces,'limitations':['Clearance uses post midheight; leaning-post corner check remains','Far/top boundary must be compared to reference','Linear longitudinal sampling needs smooth shoulder','Reference-guided shallow folds added; board geometry and fold fidelity remain unvalidated'],'blenderAllowed':False}
(r/'bellows-frame-aligned-plan.json').write_text(json.dumps(report))
print(json.dumps({k:v for k,v in report.items() if k not in ['vertices','faces']},indent=2))



