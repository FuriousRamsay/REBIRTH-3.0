"""Plan bounded stone infill from measured exterior backing locations."""
import json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/MasonryJointFit_v1')
samples=json.loads((root/'exposure-comparison.json').read_text())['results'][1]['exposedBackingSamples']
# Exclude collar, feet and chamber perimeter: the depth classifier alone was insufficient.
samples=[s for s in samples if 220<s['pixel'][1]<900 and not (950<s['pixel'][0]<1260 and 380<s['pixel'][1]<740)]
pending={tuple(s['pixel']):s for s in samples};groups=[]
while pending:
    start=next(iter(pending));queue=[start];group=[]
    while queue:
        key=queue.pop()
        if key not in pending:continue
        group.append(pending.pop(key))
        for dx,dy in [(10,0),(-10,0),(0,10),(0,-10)]:
            if (key[0]+dx,key[1]+dy) in pending:queue.append((key[0]+dx,key[1]+dy))
    if len(group)>=2:
        # Split larger connected patches into bounded blocks; no giant filler sheet.
        cells={}
        for s in group:cells.setdefault((s['pixel'][0]//60,s['pixel'][1]//60),[]).append(s)
        groups.extend(g for g in cells.values() if len(g)>=4 and min(max(s['pixel'][k] for s in g)-min(s['pixel'][k] for s in g) for k in range(2))>=10)
plan={'source':'joint-fit.blend','mode':'Measured exterior gap infill; neighbor boolean trim; clay review required',
      'units':'Existing normalized model units','rearDepth':.095,'frontRelief':.035,'sampleBuffer':.009,
      'pieces':[{'id':f'gap_infill_{i:02d}','samples':g,'pixelBounds':[[min(s['pixel'][k] for s in g),max(s['pixel'][k] for s in g)] for k in range(2)]} for i,g in enumerate(groups)],
      'validation':{'allSeedsObservedExteriorCoreHits':True,'maxPixelBlockWidth':60,'collarFeetAndChamberExcluded':True,'minimumSamplesPerPiece':4,'neighborIntersections':'Must trim and inspect','shapeFidelity':'NOT_APPROVED','hiddenDepth':'Inferred'}}
(root/'gap-infill-plan-v3.json').write_text(json.dumps(plan,indent=2));print(json.dumps({'pieces':len(groups),'seedSamples':sum(len(g) for g in groups),'validation':plan['validation']}))
