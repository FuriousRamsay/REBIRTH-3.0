import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BevelMeshCleanup_v76';out.mkdir(parents=True,exist_ok=True);classes=json.loads((root/'MasonryDefectClasses_v75/classification.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/classes['source']));deps=bpy.context.evaluated_depsgraph_get();parts=[]
for record in classes['objects']:
    if record['category']!='MODIFIER_OUTPUT':continue
    obj=bpy.data.objects[record['object']];ev=obj.evaluated_get(deps);mesh=ev.to_mesh();bm=bmesh.new();bm.from_mesh(mesh);beforeVertices=[list(v.co) for v in bm.verts]
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.index_update()
    after={'vertices':len(bm.verts),'faces':len(bm.faces),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'volume':bm.calc_volume(signed=True)}
    parts.append({'object':obj.name,'before':record['evaluated'],'after':after,'vertices':[list(v.co) for v in bm.verts],'faces':[[v.index for v in f.verts] for f in bm.faces],'faceMaterialIndices':[f.material_index for f in bm.faces],'faceSmoothFlags':[f.smooth for f in bm.faces]});bm.free();ev.to_mesh_clear()
plan={'source':classes['source'],'method':'Freeze evaluated bevel output, weld/dissolve only degeneracies within0.1 micrometer, recalculate outward normals. No broad bevel removal. Source shapes and modifiers preserved as hidden backups on build.','parts':parts,'status':'VALIDATION_REQUIRED'};path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps({'cases':len(parts),'passing':sum(p['after']['nonManifoldEdges']==0 and p['after']['zeroAreaFaces']==0 and p['after']['volume']>0 for p in parts),'failures':[{'object':p['object'],'after':p['after']} for p in parts if p['after']['nonManifoldEdges'] or p['after']['zeroAreaFaces'] or p['after']['volume']<=0]}))
