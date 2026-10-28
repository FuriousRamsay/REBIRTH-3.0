"""Independent rim-profile residuals against traced points; not acceptance."""
import json,math
from pathlib import Path
import numpy as np
root=Path(__file__).parent
plan=json.loads((root/'crucible-volume-plan.json').read_text())
observations=json.loads((root/'left-reference-records.json').read_text())
target=next(r for r in observations['records'] if r['id']=='crucible_body')
points=np.array(target['innerRimContour'],float)
e=math.radians(plan['camera']['elevationDegrees']);scale=plan['camera']['pixelsPerUnit']
center=np.array(plan['projectedRimCenter'])
angles=np.linspace(0,2*math.pi,720)
def residual(radius):
 ring=center+np.stack([radius*scale*np.cos(angles),radius*scale*math.sin(e)*np.sin(angles)],axis=1)
 distances=np.sqrt(((points[:,None,:]-ring[None,:,:])**2).sum(axis=2)).min(axis=1)
 return float(np.mean(distances)),float(np.max(distances))
original=plan['outerTopRadius']-plan['wallThickness']
candidates=[(residual(float(r))[0],float(r)) for r in np.linspace(.075,.12,181)]
_,best=min(candidates)
report={'status':'RIM_SHAPE_CHECK_NOT_FIDELITY_PASS','originalInnerRadius':original,'originalResidualMeanMaxPx':residual(original),'proposedInnerRadius':best,'proposedWallThickness':plan['outerTopRadius']-best,'proposedResidualMeanMaxPx':residual(best),'interpretation':'Original wall estimate gives an oversized opening relative to the independently traced inner rim. Proposed thickness is an image-fit hypothesis; irregular rim and shared camera remain unresolved.','blenderAllowed':False}
(root/'crucible-profile-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
