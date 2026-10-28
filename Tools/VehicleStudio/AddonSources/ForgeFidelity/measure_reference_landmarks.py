"""Compute explicit reference relationships outside Blender; not a fidelity pass."""
import json,math
from pathlib import Path
root=Path(__file__).parent
d=json.loads((root/'reference-landmarks.json').read_text())
result={'status':'REFERENCE_MEASUREMENT_ONLY','axisAnglesDegrees':{},'elevationHypothesesDegrees':{},'bounds':{}}
for name,a in d['axes'].items():
 (x0,y0),(x1,y1)=a['endpoints']
 result['axisAnglesDegrees'][name]=math.degrees(math.atan2(y1-y0,x1-x0))
for name,e in d['ellipseEstimates'].items():
 result['elevationHypothesesDegrees'][name]=math.degrees(math.asin(e['minorDiameter']/e['majorDiameter']))
for name,points in d['contours'].items():
 result['bounds'][name]=[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
result['interpretation']='Elevation hypotheses alone do not resolve camera azimuth, focal length, depth or component transforms. Model generation remains gated.'
(root/'reference-measurements.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='bounds'},indent=2))
