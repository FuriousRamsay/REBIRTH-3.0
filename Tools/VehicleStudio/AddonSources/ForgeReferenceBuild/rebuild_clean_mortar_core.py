import bpy,bmesh,json,sys,hashlib
from pathlib import Path
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CleanMortarCore_v55';out.mkdir(parents=True,exist_ok=True)
bodyPath=repo/'upright-furnace-envelope.json';body=json.loads(bodyPath.read_text());cutPath=root/'UpperCavityRebuild_v49/geometry-plan.json';cut=json.loads(cutPath.read_text())
plan={'sourceScene':'SmallCoreLoopRepair_v53/small-loop-repair-clay.blend','bodyPlan':str(bodyPath),'bodySha256':hashlib.sha256(bodyPath.read_bytes()).hexdigest(),'cutterPlan':str(cutPath),'cutterSha256':hashlib.sha256(cutPath.read_bytes()).hexdigest(),'method':'Replace only mortar core data with audited closed uncut envelope. Recalculate envelope/cutter normals and perform exact cavity difference before any surface displacement. Retain previous core as hidden recoverable object. Other objects unchanged.','cutterVertices':cut['cutterVertices'],'cutterFaces':cut['cutterFaces'],'status':'TOPOLOGY_AND_VISUAL_REVIEW_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('CLEAN_CORE_PLAN_SAVED');sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['sourceScene']));old=bpy.data.objects['furnace_core'];old.name='Preserved open-boundary mortar core';old.hide_render=True;old.hide_set(True)
def create(name,vertices,faces):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update();bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);return obj
core=create('furnace_core',body['vertices'],body['faces'])
for mat in old.data.materials:core.data.materials.append(mat)
cutter=create('Clean cavity cutter',plan['cutterVertices'],plan['cutterFaces'])
modifier=core.modifiers.new('Cavity in closed core','BOOLEAN');modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
bpy.context.view_layer.objects.active=core;bpy.ops.object.modifier_apply(modifier=modifier.name);cutter.hide_render=True;cutter.hide_set(True)
bm=bmesh.new();bm.from_mesh(core.data);report={'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True),'otherGeometryUnchanged':True,'fidelity':'VISUAL_REVIEW_PENDING'};bm.free()
(out/'topology-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
assert report['boundaryEdges']==0 and report['nonManifoldEdges']==0 and report['signedVolume']>0
assert not (out/'clean-core-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'clean-core-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
