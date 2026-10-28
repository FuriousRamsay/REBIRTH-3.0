import bpy,bmesh,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'NozzleWall_v80';out.mkdir(parents=True,exist_ok=True)
plan={'source':'ValidatedBevelCleanup_v78/validated-bevel-clay.blend','object':'planned_mesh_113','method':'Add3 mm inward solidify wall to open swept nozzle, with end rims. Preserve original outer surface vertices, pose and hardware. Eight confirmed empty construction objects hidden, not deleted. Validate evaluated mesh and outer bounds before save.','thicknessMeters':.003,'offset':-1.0,'status':'TOPOLOGY_AND_FIT_REVIEW_REQUIRED'};path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('NOZZLE_WALL_PLAN_SAVED');sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));obj=bpy.data.objects[plan['object']];before=[list(v.co) for v in obj.data.vertices]
modifier=obj.modifiers.new('Physical inward nozzle wall','SOLIDIFY');modifier.thickness=.003;modifier.offset=-1;modifier.use_rim=True;modifier.use_even_offset=True;modifier.use_quality_normals=True
bpy.context.view_layer.update();ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();bm=bmesh.new();bm.from_mesh(mesh)
report={'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True),'outerVertexDataUnchanged':before==[list(v.co) for v in obj.data.vertices],'fidelity':'Visual wall/contact review pending'};bm.free();ev.to_mesh_clear();(out/'topology-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert report['nonManifoldEdges']==0 and report['zeroAreaFaces']==0 and report['signedVolume']>0 and report['outerVertexDataUnchanged']
hidden=[]
for candidate in bpy.context.scene.objects:
    if candidate.type=='MESH' and not candidate.hide_render and len(candidate.data.vertices)==0:candidate.hide_render=True;candidate.hide_set(True);hidden.append(candidate.name)
assert not (out/'nozzle-wall-clay.blend').exists();bpy.ops.wm.save_as_mainfile(filepath=str(out/'nozzle-wall-clay.blend'));(out/'empty-remnants-receipt.json').write_text(json.dumps({'hiddenEmptyObjects':hidden,'deletedObjects':[]},indent=2))
