import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CurveExportWelds_v74';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'ClosedNormals_v73/outward-normals-clay.blend'));deps=bpy.context.evaluated_depsgraph_get();parts=[]
for obj in bpy.context.scene.objects:
    if obj.hide_render or obj.type!='CURVE':continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table();bm.verts.index_update();mapping=bmesh.ops.find_doubles(bm,verts=list(bm.verts),dist=.0000001)['targetmap']
    pairs=[{'sourceIndex':a.index,'targetIndex':b.index,'distanceMeters':(a.co-b.co).length} for a,b in mapping.items()];before={'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges)}
    bmesh.ops.weld_verts(bm,targetmap=mapping);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));after={'vertices':len(bm.verts),'faces':len(bm.faces),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True)}
    parts.append({'object':obj.name,'weldPairs':pairs,'before':before,'after':after});bm.free();ev.to_mesh_clear()
report={'source':'ClosedNormals_v73/outward-normals-clay.blend','method':'Convert evaluated capped curves to mesh; weld only coincident vertices within0.1 micrometer, recalculate outward normals. Source curves preserved until validated.','parts':parts,'status':'VALIDATION_REQUIRED'};path=out/'export-plan.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps({'curves':len(parts),'passing':sum(p['after']['nonManifoldEdges']==0 and p['after']['zeroAreaFaces']==0 and p['after']['signedVolume']>0 for p in parts),'failures':[{'object':p['object'],'after':p['after']} for p in parts if p['after']['nonManifoldEdges'] or p['after']['zeroAreaFaces'] or p['after']['signedVolume']<=0]}))
