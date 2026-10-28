import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'ClosedNormals_v73';plan=json.loads((out/'geometry-plan.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));receipts=[]
for part in plan['parts']:
    obj=bpy.data.objects[part['object']];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.index_update();beforeVertices=[list(v.co) for v in bm.verts]
    assert [[v.index for v in f.verts] for f in bm.faces]==part['beforeFaces']
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert [[v.index for v in f.verts] for f in bm.faces]==part['afterFaces'];assert [list(v.co) for v in bm.verts]==beforeVertices
    volume=bm.calc_volume(signed=True);assert volume>0;bm.to_mesh(obj.data);bm.free();receipts.append({'object':obj.name,'signedVolume':volume,'verticesUnchanged':True})
assert not (out/'outward-normals-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'outward-normals-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'reorientedMeshes':receipts,'fidelity':'VISUAL_REVIEW_PENDING','openAndDegenerateMeshesExcluded':True},indent=2))
