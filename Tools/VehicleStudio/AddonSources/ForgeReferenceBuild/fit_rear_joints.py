"""Fit solid rear stones to bounded neighboring cells with recessed joint gaps."""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
source=root/'RearSurfaceFit_v10';out=root/'RearJointFit_v11';out.mkdir(parents=True,exist_ok=True)
records=json.loads((source/'donor-shape-receipt.json').read_text())['parts']
centres={p['name']:Vector(p['position']) for p in records};schedule=[]
for p in records:
    c=centres[p['name']];near=sorted((q for q in records if q['name']!=p['name']),key=lambda q:(centres[q['name']]-c).length)[:8]
    planes=[]
    for q in near:
        delta=centres[q['name']]-c
        if delta.length<.00001:raise ValueError('Coincident masonry cell seeds')
        n=delta.normalized();planes.append({'neighbor':q['name'],'point':list((c+centres[q['name']])/2-n*.002),'normal':list(n)})
    schedule.append({'name':p['name'],'planes':planes})
(out/'joint-planes-plan.json').write_text(json.dumps({'parts':schedule,'pairGap':.004,'status':'Validated distinct seeds; geometry/clay review pending'},indent=2))
bpy.ops.wm.open_mainfile(filepath=str(source/'rear-solids.blend'))
checks=[]
for entry in schedule:
    o=bpy.data.objects[entry['name']];bm=bmesh.new();bm.from_mesh(o.data)
    for plane in entry['planes']:
        result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=Vector(plane['point']),plane_no=Vector(plane['normal']),clear_outer=True)
        boundary=[g for g in result['geom_cut'] if isinstance(g,bmesh.types.BMEdge) and g.is_valid and len(g.link_faces)==1]
        if boundary:bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    violations=sum(1 for v in bm.verts for p in entry['planes'] if (v.co-Vector(p['point'])).dot(Vector(p['normal']))>.00001)
    checks.append({'object':o.name,'vertices':len(bm.verts),'faces':len(bm.faces),'planeViolations':violations,'boundaryEdges':sum(1 for e in bm.edges if len(e.link_faces)!=2)})
    bm.to_mesh(o.data);bm.free()
assert all(c['vertices'] and not c['planeViolations'] for c in checks), 'Invalid stone cell after trimming'
(out/'joint-fit-receipt.json').write_text(json.dumps({'checks':checks,'status':'GEOMETRY_TRIM_SAVED_VISUAL_REVIEW_PENDING','limits':'Checks plane constraints and edge closure only; does not prove appearance or all front/rear transitions'},indent=2))
sc=bpy.context.scene;sc.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'packed.blend'))
sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
cam=sc.camera;target=Vector((-.05,.14,.46));a=math.radians(116.064);e=math.radians(25)
direction=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
cam.location=target+direction*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1
sc.render.resolution_x=1440;sc.render.resolution_y=1080;sc.render.filepath=str(out/'side-clay.png');bpy.ops.render.render(write_still=True)
