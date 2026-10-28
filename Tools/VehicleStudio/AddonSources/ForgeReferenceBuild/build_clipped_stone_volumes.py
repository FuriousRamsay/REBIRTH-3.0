import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'ClippedStoneVolumes_v81';plan=json.loads((out/'geometry-plan.json').read_text());assert len(plan['parts'])==10
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));receipts=[]
for part in plan['parts']:
    obj=bpy.data.objects[part['object']];assert [list(v.co) for v in obj.data.vertices]==part['originalVertices']
    backup=obj.copy();backup.data=obj.data.copy();backup.name='Preserved clipped stone '+obj.name;bpy.context.collection.objects.link(backup);backup.hide_render=True;backup.hide_set(True)
    mesh=bpy.data.meshes.new(obj.name+' closed volume');mesh.from_pydata(part['vertices'],[],part['faces']);mesh.update()
    for material in obj.data.materials:mesh.materials.append(material)
    obj.modifiers.clear();obj.data=mesh;bm=bmesh.new();bm.from_mesh(mesh);assert all(e.is_manifold for e in bm.edges) and all(f.calc_area()>1e-12 for f in bm.faces) and bm.calc_volume(signed=True)>0;bm.free();receipts.append({'object':obj.name,'boundsDriftMeters':part['boundsDriftMeters']})
assert not (out/'closed-patches-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'closed-patches-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'repaired':receipts,'originalObjectsPreservedHidden':True,'fidelity':'VISUAL_AND_CONTACT_REVIEW_PENDING'},indent=2))
