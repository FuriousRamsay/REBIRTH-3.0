"""Inferred rigid depth translations against new shell; not attachment approval."""
import json
from pathlib import Path
import numpy as np
from audit_upright_surface import hit,R,U,D
r=Path(__file__).parent
cup=next(p for p in json.loads((r/'round-components-plan.json').read_text())['parts'] if p['id']=='crucible')
flange=next(p for p in json.loads((r/'nozzle-hardware-plan.json').read_text())['parts'] if p['id']=='furnace_flange')
def solve(vertices,clearance):
 deficits=[]
 for v in np.array(vertices):
  p=np.array([891+760*(v@R),935-760*(v@U)]);surface=hit(p)
  if surface: deficits.append(float((np.array(surface['position'])-v)@D)+clearance)
 amount=max([0.]+deficits);translation=D*amount
 return {'translation':translation.tolist(),'depthShift':amount,'supportedSamples':len(deficits),
  'maximumReferenceViewPixelDrift':float(760*max(abs(translation@R),abs(translation@U))),
  'clearanceAllowance':clearance,'minimumSampledPostShiftClearance':float(amount-max(deficits)+clearance) if deficits else None}
report={'status':'ATTACHMENT_DEPTH_CANDIDATE_NOT_FIDELITY_PASS',
 'crucible':solve(cup['vertices'],.035),
 # Visible/front ring only; back of mounting plate may intentionally embed.
 'bellows':solve(flange['vertices'][:128],.008),
 'authority':'Depth inferred from new volume and required occlusion; not measured from single concept',
 'limitations':['Samples against smooth envelope only, not all stones','A rigid view-depth translation also changes world height; mounting height requires review','Flange mounting back and crucible receiving bracket need physical contact verification','No proof of globally collision-free mesh'],
 'blenderAllowed':False}
(r/'attachment-depth-plan.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
