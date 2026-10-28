"""Individual outer masonry volumes; inferred depth fitted to provisional bulk."""
import json,math
from pathlib import Path
import numpy as np
from stone_volume import stone_volume
r=Path(__file__).parent;camera=json.loads((r/'bellows-frame-plan.json').read_text());source=json.loads((r/'outer-stone-contours.json').read_text());mass=json.loads((r/'furnace-mass-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']]);R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
v=np.array(mass['vertices'][:512]);pixels=np.stack([891+760*(v@R),935-760*(v@U)],axis=1);depths=v@D;parts=[]
supplement=json.loads((r/'masonry-gap-records.json').read_text())
for stone in source['stones']+supplement['stones']:
 outline=np.array(stone['contour']);center=outline.mean(0);ids=np.argsort(np.linalg.norm(pixels-center,axis=1))[:12]
 matrix=np.column_stack([(pixels[ids]-center)/760,np.ones(len(ids))]);plane=np.linalg.lstsq(matrix,depths[ids],rcond=None)[0]
 def lift(p,offset):
  depth=float(np.r_[(p-center)/760,1]@plane)+offset
  return (R*((p[0]-891)/760)+U*((935-p[1])/760)+D*depth).tolist()
 vertices,faces=stone_volume(outline,lift,stone['id'],rear=-.14,front=.105)
 parts.append({'id':stone['id'],'vertices':vertices,'faces':faces,'localDepthPlane':plane.tolist()})
report={'status':'OUTER_MASONRY_VOLUME_DRAFT_NOT_FIDELITY_PASS','parts':parts,'limitations':['Manual contours require reference check','Hidden depth inferred from bulk','Gaps, overlaps and fractures remain to resolve'],'blenderAllowed':False}
(r/'outer-stones-plan.json').write_text(json.dumps(report));print('Created %d outer stone volumes.'%len(parts))

