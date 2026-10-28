"""Independent saved-mesh checks. A pass does not establish reference fidelity."""
import bpy,json,math
from pathlib import Path
from collections import Counter
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PlateSamples')
bpy.ops.wm.open_mainfile(filepath=str(P/'reference-plate-samples.blend'))
def clearance(q,outline):
    inside=False;distance=100.
    for a,b in zip(outline,outline[1:]+outline[:1]):
        d=b-a
        if d.length_squared<1e-12:continue
        t=max(0,min(1,(q-a).dot(d)/d.length_squared));distance=min(distance,(q-a-d*t).length)
        if (a.y>q.y)!=(b.y>q.y) and q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x:inside=not inside
    return distance if inside else -distance
collection=bpy.data.collections['Reference traced plating samples'];dg=bpy.context.evaluated_depsgraph_get();plates={};rows=[]
body_surface=BVHTree.FromObject(bpy.data.objects['body'],dg)
for o in collection.objects:
    if 'panel_outline_zy' not in o:continue
    outline=[Vector((*p,0)) for p in json.loads(o['panel_outline_zy'])];plates[o.name]=(o,outline,BVHTree.FromObject(o,dg))
    if o['panel_owner']=='cab door':
        assert all(-.266<=v.co.z<=.531 and v.co.y>=.54 for v in o.data.vertices),'Plate crosses door panel '+o.name
    support_distances=[]
    for v in o.data.vertices:
        origin=v.co-Vector((.003,0,0));hit,normal,_,distance=body_surface.ray_cast(origin,Vector((-1,0,0)),.008)
        assert hit is not None,'Plate extends beyond source body skin '+o.name+str(tuple(v.co))
        support_distances.append(distance)
    assert max(support_distances)<.004,'Plate bridges body curvature '+o.name
    evaluated=o.evaluated_get(dg).to_mesh();edges=Counter()
    for f in evaluated.polygons:
        ids=list(f.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):edges[tuple(sorted((a,b)))]+=1
    assert all(n==2 for n in edges.values()),'Broken plate shell '+o.name+' '+str(Counter(edges.values()))+' zero-area='+str(sum(f.area<1e-12 for f in evaluated.polygons))
    o.evaluated_get(dg).to_mesh_clear()
    rows.append({'plate':o.name,'closedShell':True,'owner':o['panel_owner'],'maximumRearProbeDistanceM':max(support_distances),'everyBackVertexSupportedByOriginalBody':True})
washers=[]
for o in collection.objects:
    if 'seat_support' not in o:continue
    target=o['seat_support'];assert target in plates
    plate,outline,surface=plates[target]
    margins=[clearance(Vector((v.co.z,v.co.y,0)),outline) for v in o.data.vertices]
    assert min(margins)>=.001,'Washer footprint outside plate '+target+str(min(margins))
    distances=[surface.find_nearest(v.co)[3] for v in list(o.data.vertices)[:24]]
    assert max(distances)<.0005,'Unseated washer '+target
    washers.append({'washer':o.name,'plate':target,'minimumFullFootprintMarginM':min(margins),'maximumSeatGapM':max(distances)})
assert len(plates)==22,'Missing sample plates '+str(len(plates))
report={'status':'PASS','plates':rows,'washers':washers,'scope':'Saved shell topology, door bounds and complete washer footprints/contact. Reference appearance remains a visual-review requirement.'}
(P/'saved-plate-audit.json').write_text(json.dumps(report,indent=2));print('PASS',len(plates),'plates;',len(washers),'complete washer footprints')
