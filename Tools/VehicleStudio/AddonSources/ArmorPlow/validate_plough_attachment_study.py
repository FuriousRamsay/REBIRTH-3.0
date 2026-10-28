"""Independent physical checks on the saved Blender study, not its builder data."""
import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PloughClay')
bpy.ops.wm.open_mainfile(filepath=str(P/'plough-clay-study.blend'))
dg=bpy.context.evaluated_depsgraph_get()
body=bpy.data.objects['Continuous curved moldboard'];bar=bpy.data.objects['Continuous lower cutting-edge backing bar']
body_bvh=BVHTree.FromObject(body,dg);bar_bvh=BVHTree.FromObject(bar,dg)
teeth=[o for o in bpy.data.objects if o.name.startswith('Short overlapping cutting tooth ')]
tooth_bvhs=[BVHTree.FromObject(o,dg) for o in teeth]
shanks=[o for o in bpy.data.objects if o.name.startswith('Through-bolt shank')]
assert len(teeth)==8 and len(shanks)==16
results=[]
for obj in shanks:
    verts=[v.co for v in obj.data.vertices];n=len(verts)//2
    a=sum(verts[:n],Vector())/n;b=sum(verts[n:],Vector())/n;direction=(b-a).normalized();origin=a-direction*.025
    hits={}
    for name,bvhs in [('tooth',tooth_bvhs),('backingBar',[bar_bvh]),('moldboard',[body_bvh])]:
        distances=[hit[3] for bvh in bvhs if (hit:=bvh.ray_cast(origin,direction,.20))[0] is not None]
        assert distances,'Unbacked tooth bolt '+obj.name+' misses '+name
        hits[name]=min(distances)
    assert hits['tooth']<hits['backingBar']<hits['moldboard'], 'Incorrect material stack '+obj.name
    results.append({'shank':obj.name,'rayHitDistancesM':hits})
max_distance=0;count=0
for obj in bpy.data.objects:
    if not obj.name.startswith(('Surface-conforming spike flange ','Flat curved bolted strap','Continuous lower cutting-edge backing bar')):continue
    for vertex in list(obj.data.vertices)[:len(obj.data.vertices)//2]:
        q,n,face,d=body_bvh.find_nearest(vertex.co)
        assert d<.0015,'Plate backing separated from blade '+obj.name
        max_distance=max(max_distance,d);count+=1
seats=[]
for washer in bpy.data.objects:
    if 'seat_support' not in washer:continue
    support=bpy.data.objects[washer['seat_support']];bvh=BVHTree.FromObject(support,dg);distances=[]
    for v in list(washer.data.vertices)[:washer['seat_vertex_count']]:
        q,n,face,d=bvh.find_nearest(v.co);distances.append(d)
    assert max(distances)<.001,'Washer overhang/separation '+washer.name+' on '+support.name
    seats.append({'washer':washer.name,'support':support.name,'maximumFootprintDistanceM':max(distances)})
assert len(seats)==42,'Missing edge/brace/pivot/rib seating checks'
joints=[];saddle_samples=0
for obj in bpy.data.objects:
    if 'joint_first' in obj:
        vs=[v.co for v in obj.data.vertices];n=len(vs)//2;a=sum(vs[:n],Vector())/n;b=sum(vs[n:],Vector())/n;axis=(b-a).normalized();origin=a-axis*.025;hits=[]
        for key in ['joint_first','joint_second']:
            target=bpy.data.objects[obj[key]];bvh=BVHTree.FromObject(target,dg);hit,_,_,distance=bvh.ray_cast(origin,axis,.20)
            assert hit is not None,'Upright fastener has no mating component '+obj.name+' '+key
            hits.append(distance)
        assert hits[1]>hits[0],'Upright/tab stack order incorrect'
        joints.append({'bolt':obj.name,'first':obj['joint_first'],'second':obj['joint_second'],'entryDistancesM':hits})
    if 'blade_contact_vertex_count' in obj:
        for v in list(obj.data.vertices)[:obj['blade_contact_vertex_count']]:
            _,_,_,d=body_bvh.find_nearest(v.co)
            assert d<.001,'Saddle separated from blade'
            saddle_samples+=1
assert len(joints)==6 and saddle_samples>800,'Missing blade/upright joint checks'
report={'status':'PASS_SAVED_MESH_ATTACHMENT_CHECKS_ONLY','toothCount':len(teeth),'fixingCount':len(shanks),'fixings':results,'washerSeats':seats,'uprightRibJoints':joints,'saddleContactSamples':saddle_samples,'plateBackingSamples':count,'maximumBackingDistanceM':max_distance,'artisticAcceptance':False,'installed':False}
(P/'saved-mesh-attachment-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
