"""Resolve a coherent frame/nozzle orientation hypothesis before mesh creation."""
import json,math
from pathlib import Path
r=Path(__file__).parent
e=math.radians(json.loads((r/'shared-elevation-audit.json').read_text())['proposedSharedElevationDegrees'])
parts={p['id']:p for p in json.loads((r/'left-reference-records.json').read_text())['records']}
def slope(name):
 a,b=parts[name]['axisPixels'];return (b[1]-a[1])/(b[0]-a[0])
shaft=slope('upper_shaft');nozzle=slope('nozzle_neck')
# Choose the frame shaft as local X. This fixes basis, not an absolute world bearing.
az=math.atan(-shaft/math.sin(e))
# A nozzle heading perpendicular to X can rise while appearing to fall in screen Y.
rise=(math.sin(e)*math.cos(az)-nozzle*math.sin(az))/math.cos(e)
pitch=math.atan(rise)
length=math.sqrt(1+rise*rise)
report={
 'status':'COHERENT_ORIENTATION_CANDIDATE_NOT_FULL_PLAN_PASS',
 'assumptions':['Weak perspective, zero roll','Rear frame shafts horizontal and parallel','Shaft direction defines local positive X','Nozzle heading perpendicular to shaft, with pitch allowed'],
 'cameraElevationDegrees':math.degrees(e),'cameraAzimuthDegrees':math.degrees(az),
 'shaftUnitAxis':[1,0,0],
 'nozzleUnitAxisTowardFurnace':[0,-1/length,rise/length],
 'nozzlePitchDegrees':math.degrees(pitch),
 'observedSlopes':{'upperShaft':shaft,'lowerShaft':slope('lower_shaft'),'nozzle':nozzle},
 'projectedSlopes':{'shaft':-math.sin(e)*math.tan(az),'nozzle':(math.sin(e)*math.cos(az)-rise*math.cos(e))/math.sin(az)},
 'meaning':'The previously inconsistent slope constraints admit a coherent pitched nozzle. This is an explicit geometric hypothesis, not evidence of the hidden reference geometry.',
 'remaining':['Verify throat and flange planes against this nozzle axis','Specify frame dimensions and connected endpoints','Check perspective sensitivity and upright lean','Validate complete projected assembly'],
 'blenderAllowed':False}
(r/'frame-orientation-candidate.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
