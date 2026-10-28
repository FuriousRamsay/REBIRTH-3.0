import bpy,bmesh,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'SmallCoreLoopRepair_v53';out.mkdir(parents=True,exist_ok=True)
audit=json.loads((root/'CoreBoundaryMap_v52/boundary-map.json').read_text());selected=[g for g in audit['groups'] if g['isSimpleClosedLoop'] and g['vertexCount']==18 and max(b-a for a,b in zip(g['boundsMin'],g['boundsMax']))<.06]
plan={'source':audit['source'],'method':'Cap only fifteen measured 18-edge loops below60 mm span. Preserve positions and main branched hearth boundary. These surface repairs require render review; no indiscriminate hole filling.','groups':selected,'status':'TOPOLOGY_AND_CLAY_REVIEW_REQUIRED'}
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('PLANNED_SMALL_LOOPS',len(selected));sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));obj=bpy.data.objects['furnace_core'];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();bm.verts.index_update()
indices={v['index'] for g in selected for v in g['vertices']};edges=[e for e in bm.edges if e.is_boundary and all(v.index in indices for v in e.verts)];assert len(edges)==18*len(selected)
result=bmesh.ops.holes_fill(bm,edges=edges,sides=18);newFaces=result['faces'];assert len(newFaces)==len(selected)
for face in newFaces:face.material_index=0
report={'filledLoops':len(newFaces),'remainingBoundaryEdges':sum(e.is_boundary for e in bm.edges),'remainingNonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'mainHearthBoundaryPreserved':True,'fidelity':'VISUAL_REVIEW_PENDING'}
bm.to_mesh(obj.data);bm.free();assert not (out/'small-loop-repair-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'small-loop-repair-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
