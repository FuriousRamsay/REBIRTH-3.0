"""Test a weak-perspective horizontal-axis hypothesis without invoking Blender."""
import json, math
from pathlib import Path
r=Path(__file__).parent
d=json.loads((r/'left-reference-records.json').read_text())
parts={x['id']:x for x in d['records']}
def slope(name):
 a,b=parts[name]['axisPixels'];return (b[1]-a[1])/(b[0]-a[0])
# Orthogonal horizontal axes under zero-roll weak perspective obey
# image-slope product = -sin(elevation)^2. This is a testable hypothesis,
# not a claim that the generated concept must obey it exactly.
m1=slope('upper_shaft');m2=slope('nozzle_neck')
inferred=math.degrees(math.asin(math.sqrt(-m1*m2))) if m1*m2<0 and -m1*m2<=1 else None
collar=math.degrees(math.asin(87/220))
report={'status':'CAMERA_HYPOTHESIS_NOT_VALIDATED',
 'hypothesis':'rear shaft and nozzle are orthogonal horizontal axes; zero camera roll; weak perspective',
 'observedSlopes':{'shaft':m1,'nozzle':m2},
 'elevationFromAxisProductDegrees':inferred,
 'elevationFromCollarEllipseDegrees':collar,
 'differenceDegrees':None if inferred is None else abs(inferred-collar),
 'interpretation':'The two estimates disagree. Recheck traced endpoints, perspective and whether axes are orthogonal; do not turn either estimate into an accepted model transform.',
 'remainingAlternatives':['Finite perspective instead of weak perspective','Nozzle and shaft not perpendicular in concept','Nonhorizontal or irregular collar','Manual landmark errors'],
 'blenderGenerationAllowed':False}
(r/'projection-hypothesis-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
