"""Pre-Blender completeness audit. Structural success never means visual fidelity."""
import json
from pathlib import Path
root=Path(__file__).parent
plan=json.loads((root/'geometry-plan.json').read_text())
issues=[]
if plan['camera']['status']!='VALIDATED':issues.append('Shared reference camera remains unsolved/unvalidated')
for c in plan['components']:
 if c.get('unresolved'):issues.append(c['id']+': '+c['unresolved'])
 if not any(k in c for k in ['center','path']):issues.append(c['id']+': missing explicit numeric placement or path')
 if isinstance(c.get('rotation'),str):issues.append(c['id']+': orientation rule still needs resolved numeric transform/endpoints')
 if not any(k in c for k in ['size','crossSection','diameter','plateSize','sizeRange']):issues.append(c['id']+': dimensions incomplete')
issues.append('No validated projection overlay or per-contour comparison exists for this draft')
receipt={'status':'FAIL_INCOMPLETE' if issues else 'STRUCTURALLY_COMPLETE_ONLY',
 'blenderGenerationAllowed':False,'componentGroups':len(plan['components']),
 'issues':issues,'scope':'Completeness audit only; does not validate appearance or recover unknown dimensions'}
(root/'geometry-plan-audit.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps(receipt,indent=2))
