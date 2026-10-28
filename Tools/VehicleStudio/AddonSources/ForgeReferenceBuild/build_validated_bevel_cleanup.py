import bpy,bmesh,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'ValidatedBevelCleanup_v78';out.mkdir(parents=True,exist_ok=True);trial=json.loads((root/'BevelMeshCleanup_v76/geometry-plan.json').read_text())
parts=[p for p in trial['parts'] if p['after']['nonManifoldEdges']==0 and p['after']['zeroAreaFaces']==0 and p['after']['volume']>0];assert len(parts)==6
plan={'source':trial['source'],'method':'Apply only six validated precision-cleanup meshes; preserve evaluated bounds within1 micrometer, material slots and smoothing flags. Keep original objects and modifier stacks hidden. Other failed cases unchanged.','parts':parts,'status':'APPEARANCE_REVIEW_REQUIRED'};path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([p['object'] for p in parts]));sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));deps=bpy.context.evaluated_depsgraph_get();receipts=[]
for part in parts:
    obj=bpy.data.objects[part['object']];ev=obj.evaluated_get(deps);original=ev.to_mesh();oldBounds=[[min(v.co[k] for v in original.vertices),max(v.co[k] for v in original.vertices)] for k in range(3)];ev.to_mesh_clear()
    newBounds=[[min(v[k] for v in part['vertices']),max(v[k] for v in part['vertices'])] for k in range(3)];drift=max(abs(a-b) for old,new in zip(oldBounds,newBounds) for a,b in zip(old,new));assert drift<.000001
    backup=obj.copy();backup.data=obj.data.copy();backup.name='Preserved bevel mesh '+obj.name;bpy.context.collection.objects.link(backup);backup.hide_render=True;backup.hide_set(True)
    mesh=bpy.data.meshes.new(obj.name+' validated bevel');mesh.from_pydata(part['vertices'],[],part['faces']);mesh.update()
    for material in obj.data.materials:mesh.materials.append(material)
    for face,material,smooth in zip(mesh.polygons,part['faceMaterialIndices'],part['faceSmoothFlags']):face.material_index=material;face.use_smooth=smooth
    obj.modifiers.clear();obj.data=mesh;bm=bmesh.new();bm.from_mesh(mesh);assert all(e.is_manifold for e in bm.edges) and all(f.calc_area()>=1e-12 for f in bm.faces);assert bm.calc_volume(signed=True)>0;bm.free();receipts.append({'object':obj.name,'evaluatedBoundsDriftMeters':drift})
assert not (out/'validated-bevel-clay.blend').exists();bpy.ops.wm.save_as_mainfile(filepath=str(out/'validated-bevel-clay.blend'));(out/'receipt.json').write_text(json.dumps({'repaired':receipts,'failedCasesUnchanged':True,'fidelity':'Not an appearance/fidelity pass'},indent=2));print(json.dumps(receipts))
