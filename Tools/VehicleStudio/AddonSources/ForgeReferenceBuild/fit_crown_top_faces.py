import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownTopFaces_v43';out.mkdir(parents=True,exist_ok=True)
source='CrownRivets_v41/crown-rivets-clay.blend'
if '--local-layers' in sys.argv:
    source='LocalCrownLayers_v58/local-layers-clay.blend';out=root/'LocalCrownContacts_v59';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/source))
deps=bpy.context.evaluated_depsgraph_get();parts=[]
assignments=[('harness_crown_left_cross','harness_crown_diagonal_left'),('harness_crown_front_cross','harness_crown_cross_band'),('harness_crown_right_cross','harness_crown_cross_band')]
for name,parentName in assignments:
    obj=bpy.data.objects[name];points=[obj.matrix_world@v.co for v in obj.data.vertices];base=sum(points[:48],Vector())/48;top=sum(points[-48:],Vector())/48;axis=(top-base).normalized()
    parent=bpy.data.objects[parentName];ev=parent.evaluated_get(deps);mesh=ev.to_mesh();vertices=[parent.matrix_world@v.co for v in mesh.vertices]
    faces=[list(p.vertices) for p in mesh.polygons if (parent.matrix_world.to_3x3()@p.normal).normalized().dot(axis)>.5]
    assert faces;tree=BVHTree.FromPolygons(vertices,faces);ev.to_mesh_clear()
    hit,n,index,d=tree.ray_cast(base+axis*.12,-axis,.25);method='AXIAL_TOP_FACE'
    if hit is None:hit,n,index,d=tree.find_nearest(base);method='NEAREST_TOP_FACE_POSITION_REVIEW_REQUIRED'
    assert hit is not None and n.dot(axis)>.5
    destination=hit+axis*.0005;delta=destination-base;assert delta.length<.065
    parts.append({'object':name,'parent':parentName,'axis':list(axis),'base':list(base),'contact':list(hit),'normal':list(n),'method':method,'delta':list(delta),'destination':list(obj.location+delta)})
plan={'source':source,'status':'GEOMETRY_CONTACT_CANDIDATE_NOT_FIDELITY_PASS','method':'Use top faces whose normals align above 0.5 with measured head axis. Seat base ring 0.5 mm above them. Parent assignments derived from actual contact candidates and crown layout. Nearest-face positions need visual review.','parts':parts}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps(parts));sys.exit(0)
assert json.loads(path.read_text())==plan
for part in parts:bpy.data.objects[part['object']].location=part['destination']
assert not (out/'crown-top-face-clay.blend').exists()
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'crown-top-face-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'heads':parts,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
