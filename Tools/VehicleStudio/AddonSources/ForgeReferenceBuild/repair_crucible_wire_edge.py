import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrucibleTopology_v71';audit=json.loads((out/'edge-audit.json').read_text());assert len(audit['edges'])==1 and audit['edges'][0]['isWire'] and audit['edges'][0]['linkedFaces']==0
plan={'source':audit['source'],'object':audit['object'],'removeEdge':audit['edges'][0],'method':'Remove only unrendered32 mm axial wire edge below floor. Preserve all vertices and faces, rim, pose and hardware.','status':'TOPOLOGY_VERIFICATION_REQUIRED'};path=out/'repair-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('WIRE_EDGE_REPAIR_PLAN_SAVED');sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));obj=bpy.data.objects[plan['object']];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();bm.verts.index_update();bm.edges.ensure_lookup_table()
edge=bm.edges[plan['removeEdge']['edgeIndex']];assert edge.is_wire and not edge.link_faces
assert sorted(v.index for v in edge.verts)==sorted(plan['removeEdge']['vertexIndices'])
beforeVertices=[list(v.co) for v in bm.verts];beforeFaces=[[v.index for v in face.verts] for face in bm.faces];beforeVolume=bm.calc_volume(signed=True)
bm.edges.remove(edge);assert [list(v.co) for v in bm.verts]==beforeVertices and [[v.index for v in face.verts] for face in bm.faces]==beforeFaces
report={'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(face.calc_area()<1e-12 for face in bm.faces),'signedVolume':bm.calc_volume(signed=True),'renderedVerticesFacesUnchanged':True,'fidelity':'No new appearance assertion; previous clay remains relevant because rendered geometry is unchanged.'}
assert report['nonManifoldEdges']==0 and report['zeroAreaFaces']==0 and abs(report['signedVolume']-beforeVolume)<1e-12
bm.to_mesh(obj.data);bm.free();assert not (out/'crucible-clean-clay.blend').exists();bpy.ops.wm.save_as_mainfile(filepath=str(out/'crucible-clean-clay.blend'));(out/'repair-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
