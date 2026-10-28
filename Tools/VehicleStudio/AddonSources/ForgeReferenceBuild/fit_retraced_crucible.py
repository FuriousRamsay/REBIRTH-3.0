"""Fit the corrected opening independently; preserve earlier failed estimates."""
import json, math
from pathlib import Path
import numpy as np
root=Path(__file__).parent
trace=json.loads((root/'crucible-reference-recheck.json').read_text())
points=np.array(trace['openingInnerContour'],float)
angles=np.linspace(0,math.tau,720,endpoint=False)
unit=np.stack([np.cos(angles),np.sin(angles)],axis=1)
def score(p):
    ring=unit*np.array(p[2:])+np.array(p[:2])
    distances=np.sqrt(((points[:,None,:]-ring[None,:,:])**2).sum(axis=2)).min(axis=1)
    return float(np.mean(distances**2)),distances
p=np.array([555.,665.,87.,34.])
for step in [8,4,2,1,.5,.25]:
    for iteration in range(100):
        choices=[(score(p)[0],p.copy())]
        for axis in range(4):
            for sign in [-1,1]:
                q=p.copy();q[axis]+=step*sign
                if min(q[2:])>0:choices.append((score(q)[0],q))
        value,q=min(choices,key=lambda x:x[0])
        if np.array_equal(q,p):break
        p=q
_,distances=score(p)
elevation=math.degrees(math.asin(p[3]/p[2]))
report={
 'status':'REVISED_REFERENCE_FIT_NOT_GEOMETRY_ACCEPTANCE',
 'source':'crucible-reference-recheck.json',
 'ellipseCenterPixels':p[:2].tolist(),
 'ellipseRadiiPixels':p[2:].tolist(),
 'meanContourDistancePixels':float(np.mean(distances)),
 'maxContourDistancePixels':float(max(distances)),
 'inferredElevationDegreesIfHorizontalCircleAndOrthographic':elevation,
 'earlierPlanningElevationDegrees':24,
 'elevationDifferenceDegrees':elevation-24,
 'interpretation':'A circular opening is a plausible planning primitive at this elevation. Trace uncertainty, perspective and chipped rim prevent treating the fit as a camera calibration or fidelity pass.',
 'nextRequiredCheck':'Compare this independent opening constraint with the top collar using the same camera; then specify the ceramic outer lip separately from the hoops and retainers.',
 'blenderAllowed':False}
(root/'crucible-retraced-fit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
