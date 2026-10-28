"""Measure draft contour coverage, not fidelity; no reference pixels altered."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
r=Path(__file__).parent
size=(1448,1086)
def mask(polygons):
 im=Image.new('1',size);d=ImageDraw.Draw(im)
 for poly in polygons:d.polygon([tuple(p) for p in poly],fill=1)
 return np.asarray(im,dtype=bool)
body=mask([json.loads((r/'furnace-silhouette-recheck.json').read_text())['contourPixels']])
records=json.loads((r/'furnace-reference-records.json').read_text())['records']
opening=mask([p['contourPixels'] for p in records if p['id']=='arch_void'])
original=json.loads((r/'outer-stone-contours.json').read_text())['stones']
added=json.loads((r/'masonry-gap-records.json').read_text())['stones']
arch=json.loads((r/'arch-stone-records.json').read_text())['records']
baseline=mask([p['contour'] for p in original]+[p['contourPixels'] for p in arch])
current=baseline|mask([p['contour'] for p in added])
domain=body&~opening
report={'status':'COVERAGE_DIAGNOSTIC_NOT_FIDELITY_PASS',
 'domainPixels':int(domain.sum()),
 'originalStoneCoverageFraction':float((baseline&domain).sum()/domain.sum()),
 'revisedStoneCoverageFraction':float((current&domain).sum()/domain.sum()),
 'uncoveredPixels':int((domain&~current).sum()),
 'limitations':['Includes occluded regions behind bellows, crucible and harness; not all uncovered pixels require stones',
 'Uses draft contours, not segmentation of reference',
 'Does not assess depths, intersections, silhouette fidelity or material quality']}
(r/'masonry-coverage-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
