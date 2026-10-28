import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CoreWeldPlan_v51';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'HearthLipRecession_v47/lip-recession-clay.blend'))
obj=bpy.data.objects['furnace_core'];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();bm.verts.index_update()
mapping=bmesh.ops.find_doubles(bm,verts=list(bm.verts),dist=.00001)['targetmap']
pairs=[{'sourceIndex':a.index,'targetIndex':b.index,'sourcePosition':list(a.co),'targetPosition':list(b.co),'distanceMeters':(a.co-b.co).length} for a,b in mapping.items()]
assert all(p['distanceMeters']<=.00001001 for p in pairs)
before={'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges)}
bmesh.ops.weld_verts(bm,targetmap=mapping)
after={'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces)}
report={'source':'HearthLipRecession_v47/lip-recession-clay.blend','object':'furnace_core','method':'In-memory weld test only, maximum10 micrometers. Explicit duplicate pairs, no hole filling and no source edit.','pairs':pairs,'before':before,'after':after,'status':'TOPOLOGY_REVIEW_REQUIRED'}
path=out/'weld-plan.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));bm.free();print(json.dumps({'duplicatePairs':len(pairs),'before':before,'after':after}))
