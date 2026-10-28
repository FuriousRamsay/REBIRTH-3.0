"""Inspect saved sleeve topology and body-foot contact; no visual approval implied."""
import bpy,json,math
from pathlib import Path
from collections import Counter
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/ConnectedGrilles')
bpy.ops.wm.open_mainfile(filepath=str(P/'camper-connected-grilles.blend'))
report=json.loads((P/'connection-report.json').read_text());dg=bpy.context.evaluated_depsgraph_get()
pipes=[BVHTree.FromObject(o,dg) for o in bpy.data.collections['Reference connected window guards'].objects if (o.type=='CURVE' and 'rounded frame' in o.name) or 'transverse guard welded mesh' in o.name]
checks=[]
for row in report['sleeves']:
    o=bpy.data.objects[row['name']];edges=Counter()
    for f in o.data.polygons:
        ids=list(f.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):edges[tuple(sorted((a,b)))]+=1
    assert all(n==2 for n in edges.values()),'Open/nonmanifold sleeve '+o.name
    assert all(math.isfinite(c) for v in o.data.vertices for c in v.co),'Invalid sleeve vertex'
    inner=list(o.data.vertices)[:13*32]
    gaps=[min(b.find_nearest(v.co)[3] for b in pipes) for v in inner]
    # Parent curves use a faceted circular cross section. Include that chord
    # error; never count a sleeve's own faces as pipe support.
    assert max(gaps)<.002,'Sleeve separated from pipe '+o.name+str(max(gaps))
    checks.append({'name':o.name,'closedTwoManifold':True,'maxInnerDistanceToPipeM':max(gaps)})
hosts={n:BVHTree.FromObject(bpy.data.objects[n],dg) for n in ['body','camper_body','parts','RearDoor_camper_body'] if n in bpy.data.objects}
feet=[]
for o in bpy.data.collections['Reference connected window guards'].objects:
    if 'fitted metal foot' not in o.name:continue
    distances=[min(b.find_nearest(v.co)[3] for b in hosts.values()) for v in o.data.vertices]
    assert max(distances)<.011,'Unsupported mounting plate '+o.name
    feet.append({'name':o.name,'maxOuterFaceDistanceToSourceM':max(distances)})
parts=bpy.data.objects['parts'];mirror_faces=[]
for f in parts.data.polygons:
    c=f.center
    if abs(c.x)>.70 and 1.17<c.y<1.40 and .20<c.z<.48:mirror_faces.append(list(f.vertices))
mirror_arm=BVHTree.FromPolygons([v.co for v in parts.data.vertices],mirror_faces)
mirror=bpy.data.objects['mirrors'];mirror_surface=BVHTree.FromObject(mirror,dg);clearance=[]
for o in bpy.data.collections['Reference connected window guards'].objects:
    if 'CabSide' not in o.name or o.type!='CURVE':continue
    b=BVHTree.FromObject(o,dg)
    assert not b.overlap(mirror_surface),'Cab grille intersects original mirror '+o.name
    assert not b.overlap(mirror_arm),'Cab grille intersects mirror-arm region '+o.name
    clearance.append(o.name)
assert not any('shared perimeter' in o.name.lower() for o in bpy.data.collections['Reference connected window guards'].objects),'Redundant links remain'
out={'status':'PASS','sleeves':checks,'feet':feet,'mirrorClearanceChecked':clearance,'redundantPerimeterLinks':0,'limitations':'Checks saved meshes for closed annular sleeves, source proximity and mirror-region intersections. Does not establish reference fidelity or certify mechanical loads.'}
(P/'formed-geometry-audit.json').write_text(json.dumps(out,indent=2));print('PASS',len(checks),'closed sleeves;',len(feet),'source-fitted feet')
