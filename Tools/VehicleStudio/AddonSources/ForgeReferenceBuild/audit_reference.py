"""Read-only numerical checks on fresh image observations, not visual acceptance."""
import hashlib,json,math
from pathlib import Path
from PIL import Image
root=Path(__file__).parent
plan=json.loads((root/'plan.json').read_text())
reference=Path(plan['referencePath'])
size=Image.open(reference).size
issues=[];records={};measurements={}
for name in ['left-reference-records.json','furnace-reference-records.json']:
 data=json.loads((root/name).read_text())
 if list(size)!=data.get('referenceSize',data.get('imageSize')):issues.append(name+': image size mismatch')
 for r in data['records']:
  if r['id'] in records:issues.append('Duplicate '+r['id'])
  records[r['id']]=r
  for key in ['contourPixels','frontContour','outerContour','innerRimContour','axisPixels','centerlinePixels','pathPixels']:
   for x,y in r.get(key,[]):
    if not (0<=x<size[0] and 0<=y<size[1]):issues.append(r['id']+': out-of-image coordinate')
  if 'axisPixels' in r:
   (x,y),(xx,yy)=r['axisPixels'];measurements[r['id']]={'lengthPx':math.hypot(xx-x,yy-y),'angleDegrees':math.degrees(math.atan2(yy-y,xx-x))}
sha=hashlib.sha256(reference.read_bytes()).hexdigest()
if sha.lower()!=plan['referenceSha256'].lower():issues.append('Reference hash changed')
def bounds(points):
 return [min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
cup=bounds(records['crucible_body']['outerContour'])
body=bounds(records['body_silhouette']['contourPixels'])
fold=bounds(records['fold_envelope']['outerContour'])
report={'status':'NUMERICAL_REFERENCE_AUDIT_ONLY','sourceSize':size,'sha256':sha,'dataErrors':issues,
 'axisMeasurements':measurements,
 'projectedRatios':{'crucibleToFurnaceWidth':(cup[2]-cup[0])/(body[2]-body[0]),'crucibleToFurnaceHeight':(cup[3]-cup[1])/(body[3]-body[1]),'leatherToFurnaceWidth':(fold[2]-fold[0])/(body[2]-body[0])},
 'frameShaftAngleDifferenceDegrees':abs(measurements['upper_shaft']['angleDegrees']-measurements['lower_shaft']['angleDegrees']),
 'remaining':['Visual contour validation','Shared camera fit','Explicit 3D per-shape specification','Individual masonry and remaining hardware inventory'],
 'blenderGenerationAllowed':False}
(root/'reference-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='axisMeasurements'},indent=2))
