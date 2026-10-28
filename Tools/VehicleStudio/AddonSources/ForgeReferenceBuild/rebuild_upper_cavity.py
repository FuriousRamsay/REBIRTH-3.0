import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'UpperCavityRebuild_v49';out.mkdir(parents=True,exist_ok=True)
spec=json.loads((repo/'upright-hearth-plan.json').read_text());axis=Vector(spec['outwardAxis']);side=Vector((-axis.y,axis.x,0));profile=spec['openingProfileSideZ']
newProfile=[[x,z+.035*max(0,min(1,(z-.50)/.12))] for x,z in profile]
vertices=[]
for depth in [spec['backDepth']+.005,spec['frontDepth']+.12]:
    for x,z in newProfile:vertices.append(list(side*x+axis*depth+Vector((0,0,z))))
n=len(profile);faces=[list(range(n-1,-1,-1)),list(range(n,2*n))]
faces.extend([[i,(i+1)%n,(i+1)%n+n,i+n] for i in range(n)])
edges={}
for face in faces:
    for a,b in zip(face,face[1:]+face[:1]):key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
assert all(v==2 for v in edges.values())
plan={'source':'HearthLipRecession_v47/lip-recession-clay.blend','method':'Continuous upper cavity cut. Preserve profile below Z0.5 m; smoothly raise upper profile by up to35 mm, reaching full lift at Z0.62 m. Cutter starts5 mm forward of existing back wall and extends120 mm beyond original front. Only furnace_core is modified.','originalProfile':profile,'newProfile':newProfile,'cutterVertices':vertices,'cutterFaces':faces,'edgeIncidenceValidated':True,'status':'BOOLEANS_AND_CLAY_REVIEW_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('CLOSED_CUTTER_PLAN',len(vertices),len(faces));sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));core=bpy.data.objects['furnace_core'];before=(len(core.data.vertices),len(core.data.polygons))
mesh=bpy.data.meshes.new('Upper cavity continuous cutter');mesh.from_pydata(vertices,[],faces);mesh.update();bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
cutter=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(cutter)
modifier=core.modifiers.new('Continuous upper opening cut','BOOLEAN');modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
bpy.context.view_layer.objects.active=core;bpy.ops.object.modifier_apply(modifier=modifier.name);cutter.hide_render=True;cutter.hide_set(True)
assert len(core.data.vertices)>0 and len(core.data.polygons)>0
bm=bmesh.new();bm.from_mesh(core.data);edgeAudit={'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges)};bm.free()
assert not (out/'upper-cavity-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'upper-cavity-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'beforeVerticesFaces':before,'afterVerticesFaces':[len(core.data.vertices),len(core.data.polygons)],'edgeAudit':edgeAudit,'otherGeometryUnchanged':True,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
