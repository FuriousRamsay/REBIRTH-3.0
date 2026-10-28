import bpy,bmesh,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'SidePatchVolume_v62';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'LocalCrownContacts_v59/crown-top-face-clay.blend'));obj=bpy.data.objects['stone_side_patch_09'];bm=bmesh.new()
for vertex in obj.data.vertices:bm.verts.new(vertex.co)
bm.verts.ensure_lookup_table();result=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
unused=[item for item in set(result.get('geom_unused',[])+result.get('geom_interior',[])) if isinstance(item,bmesh.types.BMVert)]
if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.ensure_lookup_table();bm.verts.index_update();assert all(e.is_manifold for e in bm.edges)
vertices=[list(v.co) for v in bm.verts];faces=[[v.index for v in f.verts] for f in bm.faces]
plan={'source':'LocalCrownContacts_v59/crown-top-face-clay.blend','object':obj.name,'method':'Closed convex hull from actual patch vertices. Preserve all extreme positions, object pose, materials and modifiers; replaces open clipped patch. Concavities may change, requiring visual review.','vertices':vertices,'faces':faces,'nonManifoldEdges':0,'signedVolume':bm.calc_volume(signed=True),'status':'CLAY_REVIEW_REQUIRED'};bm.free();path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps({'vertices':len(vertices),'faces':len(faces),'volume':plan['signedVolume']}));sys.exit(0)
assert json.loads(path.read_text())==plan
backup=obj.copy();backup.data=obj.data.copy();backup.name='Preserved open side patch09';bpy.context.collection.objects.link(backup);backup.hide_render=True;backup.hide_set(True)
mesh=bpy.data.meshes.new('Closed side patch09');mesh.from_pydata(vertices,[],faces);mesh.update()
for material in obj.data.materials:mesh.materials.append(material)
obj.data=mesh;assert not (out/'side-patch-volume-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'side-patch-volume-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
