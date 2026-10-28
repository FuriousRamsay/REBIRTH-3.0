"""Sample envelope faces against oriented post boxes; clearance screen, not exhaustive proof."""
import json
from pathlib import Path
import numpy as np
r=Path(__file__).parent
f=json.loads((r/'bellows-frame-plan.json').read_text());b=json.loads((r/'bellows-frame-aligned-plan.json').read_text())
vertices=np.array(b['vertices']);samples=[vertices]
for face in b['faces']:
 points=vertices[face];samples.append(points.mean(axis=0)[None,:])
 samples.append((points+np.roll(points,1,axis=0))/2)
samples=np.concatenate(samples);results=[]
for p in f['records']:
 if p['id'] not in ['near_post','far_post']:continue
 start,end=np.array(p['axisEndpoints']);axis=end-start;length=np.linalg.norm(axis);axis/=length
 side=np.array([axis[2],0,-axis[0]]);other=np.cross(axis,side)
 basis=np.stack([side,other,axis],axis=1)
 local=(samples-(start+end)/2)@basis
 extent=np.array([*p['crossSection'],length])/2
 q=np.abs(local)-extent
 signed=np.linalg.norm(np.maximum(q,0),axis=1)+np.minimum(np.max(q,axis=1),0)
 results.append({'post':p['id'],'penetratingSamples':int(sum(signed<0)),'minimumSignedDistance':float(min(signed)),'sampleCount':len(samples)})
report={'status':'SAMPLED_CLEARANCE_CHECK','results':results,'interpretation':'Negative distances identify intersection. Positive samples do not prove triangle-level separation; this is a planning screen.','blenderAllowed':False}
(r/'bellows-clearance-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
