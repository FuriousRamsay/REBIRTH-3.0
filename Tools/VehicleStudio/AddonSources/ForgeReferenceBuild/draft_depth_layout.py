"""Explicit inverse-projection planning study, never a validated model generator."""
import json,math
from pathlib import Path
root=Path(__file__).parent
a=math.radians(30);e=math.radians(24);scale=760;cx=891;cy=935
def lift(p,y):
 x=((p[0]-cx)/scale+math.sin(a)*y)/math.cos(a)
 z=((cy-p[1])/scale-math.sin(a)*math.sin(e)*x-math.cos(a)*math.sin(e)*y)/math.cos(e)
 return [x,y,z]
def project(p):
 x,y,z=p
 return [cx+scale*(math.cos(a)*x-math.sin(a)*y),cy-scale*(math.sin(a)*math.sin(e)*x+math.cos(a)*math.sin(e)*y+math.cos(e)*z)]
records=[];maximum=0
for filename,plane in [('arch-stone-records.json',-.40),('left-reference-records.json',-.18),('connection-outlines.json',-.18)]:
 for r in json.loads((root/filename).read_text())['records']:
  key=next((k for k in ['contourPixels','frontContour','outerContour','axisPixels','centerlinePixels'] if k in r),None)
  if key is None:continue
  y=-.42 if r['id'].startswith(('crucible','upper_hoop','lower_hoop','front_retainer','side_retainer')) else plane
  points=[lift(p,y) for p in r[key]]
  lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
  for p,q in zip(r[key],points):maximum=max(maximum,math.dist(p,project(q)))
  records.append({'id':r['id'],'source':filename,'sourceContour':key,'frontSurfacePoints':points,'center':[(l+h)/2 for l,h in zip(lo,hi)],'frontSurfaceBounds':[lo,hi],'depthPlaneY':y,'depthAuthority':'unvalidated hypothesis','rotation':'unresolved; points lie on temporary planning plane, not final part orientation','thickness':None})
result={'status':'DEPTH_HYPOTHESIS_ONLY_DO_NOT_BUILD','cameraHypothesis':{'type':'orthographic planning approximation','azimuthDegrees':30,'elevationDegrees':24,'pixelsPerUnit':scale,'imageOrigin':[cx,cy]},'records':records,'roundTripMaxPixels':maximum,'warning':'Near-zero round-trip error is algebraic by construction, NOT evidence of camera or shape fidelity. Surface points are not closed solids. Independently validate depths, orientations and attachments before Blender.','blenderAllowed':False}
(root/'depth-layout-draft.json').write_text(json.dumps(result,indent=2))
print(json.dumps({'records':len(records),'roundTripMaxPixels':maximum,'status':result['status']}))
