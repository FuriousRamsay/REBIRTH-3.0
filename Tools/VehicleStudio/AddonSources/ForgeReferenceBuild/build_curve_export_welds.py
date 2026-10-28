import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CurveExportWelds_v74';plan=json.loads((out/'export-plan.json').read_text())
assert all(p['after']['nonManifoldEdges']==0 and p['after']['zeroAreaFaces']==0 and p['after']['signedVolume']>0 for p in plan['parts'])
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));deps=bpy.context.evaluated_depsgraph_get();receipts=[]
for part in plan['parts']:
    old=bpy.data.objects[part['object']];assert old.type=='CURVE';ev=old.evaluated_get(deps);mesh=bpy.data.meshes.new_from_object(ev,depsgraph=deps);bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table();bm.verts.index_update()
    mapping=bmesh.ops.find_doubles(bm,verts=list(bm.verts),dist=.0000001)['targetmap'];pairs=sorted((a.index,b.index) for a,b in mapping.items());assert pairs==sorted((p['sourceIndex'],p['targetIndex']) for p in part['weldPairs'])
    bmesh.ops.weld_verts(bm,targetmap=mapping);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges) and all(f.calc_area()>=1e-12 for f in bm.faces);assert bm.calc_volume(signed=True)>0
    bm.to_mesh(mesh);bm.free();name=old.name;old.name='Preserved capped curve '+name;old.hide_render=True;old.hide_set(True)
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.matrix_world=old.matrix_world.copy();receipts.append({'object':name,'vertices':len(mesh.vertices),'faces':len(mesh.polygons)})
assert not (out/'welded-seams-clay.blend').exists();bpy.ops.wm.save_as_mainfile(filepath=str(out/'welded-seams-clay.blend'));(out/'build-receipt.json').write_text(json.dumps({'convertedObjects':receipts,'originalCurvesPreservedHidden':True,'maximumWeldDistanceMeters':.0000001,'fidelity':'Appearance comparison still required, no full gate pass'},indent=2));print('CONVERTED_AND_VERIFIED',len(receipts))
