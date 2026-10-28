import bpy,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'CamperMount'
def signature():
    rows={}
    for o in bpy.data.objects:
        if o.type!='MESH' or not o.name.startswith(('Continuous curved moldboard','Individually placed turned spike','Short overlapping cutting tooth','Surface-conforming spike flange','Curved blade rib','Upper curved blade saddle')):continue
        h=hashlib.sha256()
        for v in o.data.vertices:h.update(struct.pack('fff',*v.co))
        for p in o.data.polygons:
            for i in p.vertices:h.update(struct.pack('I',i))
        rows[o.name]=h.hexdigest()
    return rows
bpy.ops.wm.open_mainfile(filepath=str(P/'PloughClay/ApprovedPloughGeometry_PendingCamperMount/plough-clay-study.blend'));before=signature()
bpy.ops.wm.open_mainfile(filepath=str(O/'camper-mounted-plough.blend'));after=signature();assert before==after,'Approved blade/rib/tooth geometry changed'
dg=bpy.context.evaluated_depsgraph_get();bvhs={}
def bvh(o):
    if o.name not in bvhs:bvhs[o.name]=BVHTree.FromObject(o,dg)
    return bvhs[o.name]
receivers=[];anchors=[]
for o in bpy.data.objects:
    if o.name.startswith(('Receiver locking through-bolt','Source underbody anchor shank')):
        vs=[v.co for v in o.data.vertices];n=len(vs)//2;a=sum(vs[:n],Vector())/n;b=sum(vs[n:],Vector())/n;d=(b-a).normalized();origin=a-d*.025
        if o.name.startswith('Receiver locking'):
            hits=[]
            for key in ['first_component','second_component']:
                target=bpy.data.objects[o[key]];hit,_,_,dist=bvh(target).ray_cast(origin,d,.4);assert hit is not None,'Receiver fixing misses '+target.name;hits.append(dist)
            assert hits[1]>hits[0];receivers.append(o.name)
        else:
            hit,_,_,dist=bvh(bpy.data.objects['parts']).ray_cast(origin,d,.4);assert hit is not None,'Underbody anchor misses source metal';anchors.append(o.name)
assert len(receivers)==4 and len(anchors)==8
seats=0
for o in bpy.data.collections['Camper mounted plough receivers'].objects:
    if 'seat_support' not in o:continue
    target=bpy.data.objects[o['seat_support']]
    for v in list(o.data.vertices)[:o['seat_vertex_count']]:
        _,_,_,distance=bvh(target).find_nearest(v.co);assert distance<.001,'Mount washer not seated '+o.name
    seats+=1
# Broad-phase bounds against the source straight-ahead tyres, in model metres.
tyres=[o for o in bpy.data.objects if o.name.startswith('80sCamperTruck_wheel_F')]
collisions=[]
for o in bpy.data.collections['Camper mounted plough receivers'].objects:
    if o.type!='MESH':continue
    lo=Vector(tuple(min(v.co[i] for v in o.data.vertices) for i in range(3)));hi=Vector(tuple(max(v.co[i] for v in o.data.vertices) for i in range(3)))
    for tyre in tyres:
        tl=Vector(tuple(min(v.co[i] for v in tyre.data.vertices) for i in range(3)));th=Vector(tuple(max(v.co[i] for v in tyre.data.vertices) for i in range(3)))
        if all(hi[i]>tl[i] and lo[i]<th[i] for i in range(3)):collisions.append((o.name,tyre.name))
assert not collisions,'New mount intersects straight-ahead tyre envelope '+str(collisions)
report={'status':'PASS_SOURCE_MESH_CONNECTION_CHECKS','approvedGeometryPreserved':len(before),'receiverJoints':len(receivers),'sourceUnderbodyAnchors':len(anchors),'washerSeats':seats,'straightTyreEnvelopeIntersections':collisions,'limits':'Visual game asset adaptation; not a manufacturer mounting specification or suspension/steering sweep qualification','installed':False}
(O/'mount-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
