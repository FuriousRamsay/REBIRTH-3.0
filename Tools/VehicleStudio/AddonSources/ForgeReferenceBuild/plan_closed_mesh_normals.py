import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'ClosedNormals_v73';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CrucibleTopology_v71/crucible-clean-clay.blend'));parts=[]
for obj in bpy.context.scene.objects:
    if obj.hide_render or obj.type!='MESH' or obj.name=='Inspection ground':continue
    bm=bmesh.new();bm.from_mesh(obj.data);before=bm.calc_volume(signed=True)
    if before>=-1e-12 or any(not e.is_manifold for e in bm.edges) or any(f.calc_area()<1e-12 for f in bm.faces):bm.free();continue
    bm.verts.index_update();beforeFaces=[[v.index for v in f.verts] for f in bm.faces];bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));after=bm.calc_volume(signed=True);assert after>0
    parts.append({'object':obj.name,'beforeVolume':before,'afterVolume':after,'beforeFaces':beforeFaces,'afterFaces':[[v.index for v in f.verts] for f in bm.faces]});bm.free()
plan={'source':'CrucibleTopology_v71/crucible-clean-clay.blend','method':'Reorient only closed, nondegenerate negative-volume meshes. Preserve vertices, face count and pose. Explicit face winding before/after. Signed volume may change because prior mixed winding invalidates that measurement. Excludes open or zero-area meshes pending separate repair.','parts':parts,'status':'NORMALS_AND_VISUAL_REVIEW_REQUIRED'};path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps({'plannedObjects':len(parts),'names':[p['object'] for p in parts]}))
