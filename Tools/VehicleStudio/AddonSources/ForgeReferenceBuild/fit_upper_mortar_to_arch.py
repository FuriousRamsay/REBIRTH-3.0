import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'UpperArchContact_v48';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'HearthLipRecession_v47/lip-recession-clay.blend'))
spec=json.loads((repo/'upright-hearth-plan.json').read_text());axis=Vector(spec['outwardAxis']);side=Vector((-axis.y,axis.x,0));profile=sorted(spec['openingProfileSideZ'][4:],key=lambda p:p[0]);deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not obj.name.startswith(('stone_arch_','stone_jamb_')):continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices);vertices.extend(obj.matrix_world@v.co for v in mesh.vertices)
    faces.extend([[start+i for i in p.vertices] for p in mesh.polygons]);ev.to_mesh_clear()
tree=BVHTree.FromPolygons(vertices,faces);core=bpy.data.objects['furnace_core'];inverse=core.matrix_world.inverted();changes=[]
for vertex in core.data.vertices:
    p=core.matrix_world@vertex.co;x=p.dot(side);depth=p.dot(axis)
    if not .04<x<.49 or not -.085<depth<.065 or not .58<p.z<.74:continue
    ceiling=None
    for a,b in zip(profile,profile[1:]):
        if a[0]<=x<=b[0]:ceiling=a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);break
    if ceiling is None or abs(p.z-ceiling)>.035:continue
    hit,n,index,d=tree.ray_cast(p+Vector((0,0,.001)),Vector((0,0,1)),.05)
    if hit is None or n.z>-.15:continue
    movement=hit.z-p.z+.001
    if not .001<movement<.040:continue
    target=p+Vector((0,0,movement));changes.append({'index':vertex.index,'from':list(p),'to':list(target),'stoneContact':list(hit),'movementMeters':movement})
assert changes,'No supported contact; do not invent geometry'
before=[v.co.copy() for v in core.data.vertices];after=before.copy();indices={c['index'] for c in changes}
for c in changes:after[c['index']]=inverse@Vector(c['to'])
flips=[]
for polygon in core.data.polygons:
    if not any(i in indices for i in polygon.vertices):continue
    a,b,c=list(polygon.vertices)[:3];old=(before[b]-before[a]).cross(before[c]-before[a]);new=(after[b]-after[a]).cross(after[c]-after[a])
    if old.length>1e-10 and new.length>1e-10 and old.normalized().dot(new.normalized())<=0:flips.append(polygon.index)
plan={'source':'HearthLipRecession_v47/lip-recession-clay.blend','method':'Upper profile corridor only. Cast upward to evaluated arch/jamb stone underside, raise core edge 1 mm into stone contact. Maximum 40 mm lift; unchanged stones and topology.','vertexChanges':changes,'flippedFaceScreen':flips,'status':'REJECTED_FACE_FLIPS' if flips else 'CLAY_REVIEW_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps({'plannedVertices':len(changes),'flips':len(flips)}));sys.exit(0)
assert json.loads(path.read_text())==plan and not flips
for c in changes:core.data.vertices[c['index']].co=inverse@Vector(c['to'])
core.data.update();assert not (out/'upper-arch-contact-clay.blend').exists()
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'upper-arch-contact-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
