"""Combined projection of current numeric geometry, without reference-image editing."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
frame=json.loads((r/'bellows-frame-plan.json').read_text())
neck=json.loads((r/'nozzle-smooth-plan.json').read_text())
traces=json.loads((r/'left-reference-records.json').read_text())
a,e=map(math.radians,[frame['cameraAzimuthDegrees'],frame['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)])
im=Image.new('RGB',(1448,1086),'#f4f4ef');d=ImageDraw.Draw(im)
def project(p):p=np.array(p);return (891+760*float(R@p),935-760*float(U@p))
for record in frame['records']:
 if 'axisEndpoints' in record:
  start,end=map(np.array,record['axisEndpoints'])
  if 'crossSection' in record:
   axis=np.array(record['axisUnit']);side=np.array([axis[2],0,-axis[0]]);other=np.cross(axis,side)
   w,h=record['crossSection'];rings=[]
   for center in [start,end]:
    ring=[center+sx*w/2*side+sy*h/2*other for sx,sy in [(-1,-1),(1,-1),(1,1),(-1,1)]];rings.append(ring)
    d.line([project(p) for p in ring+[ring[0]]],fill='#98622c',width=2)
   for i in range(4):d.line([project(rings[0][i]),project(rings[1][i])],fill='#98622c',width=2)
  else:d.line([project(start),project(end)],fill='#444444',width=max(1,round(record['diameter']*760)))
 else:
  x,y=project(record['center']);rad=record['washerOuterRadius']*760
  d.ellipse((x-rad,y-rad,x+rad,y+rad),outline='#555555',width=2)
for j in range(0,48,6):d.line([project(s['ringVertices'][j]) for s in neck['sections']],fill='#1b719a',width=2)
for name in ['fold_envelope','throat_collar','furnace_flange']:
 record=next(p for p in traces['records'] if p['id']==name);p=record['outerContour']
 d.line([tuple(v) for v in p+[p[0]]],fill='#a9a9a9',width=2)
d.text((25,25),'3D frame and neck projection. Gray outlines = unmodeled reference traces.',fill='black')
d.text((25,1040),'Planning diagram only. Bearings shown schematically; hidden edges remain visible.',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');im.save(out/'left-assembly-projection.png')
print(str(out/'left-assembly-projection.png'))
