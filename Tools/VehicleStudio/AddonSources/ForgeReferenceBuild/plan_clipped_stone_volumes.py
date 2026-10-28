import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'ClippedStoneVolumes_v81';out.mkdir(parents=True,exist_ok=True);locations=json.loads((root/'RawDefectLocations_v79/locations.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/'NozzleWall_v80/nozzle-wall-clay.blend'));parts=[]
for record in locations['objects']:
    if record['empty'] or not record['object'].startswith('stone_'):continue
    obj=bpy.data.objects[record['object']];bm=bmesh.new()
    for vertex in obj.data.vertices:bm.verts.new(vertex.co)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bm.verts.ensure_lookup_table();result=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    unused=[item for item in set(result.get('geom_unused',[])+result.get('geom_interior',[])) if isinstance(item,bmesh.types.BMVert)]
    if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.index_update();assert all(e.is_manifold for e in bm.edges) and all(f.calc_area()>1e-12 for f in bm.faces),obj.name+' invalid hull topology'
    if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    assert bm.calc_volume(signed=True)>1e-12,obj.name+' has no valid solid volume'
    vertices=[list(v.co) for v in bm.verts];faces=[[v.index for v in f.verts] for f in bm.faces];original=[list(v.co) for v in obj.data.vertices]
    boundsDrift=max(abs(min(v[k] for v in original)-min(v[k] for v in vertices)) for k in range(3));boundsDrift=max(boundsDrift,max(abs(max(v[k] for v in original)-max(v[k] for v in vertices)) for k in range(3)));assert boundsDrift<.00001,f'{obj.name} bounds drift {boundsDrift}'
    parts.append({'object':obj.name,'originalVertices':original,'vertices':vertices,'faces':faces,'signedVolume':bm.calc_volume(signed=True),'boundsDriftMeters':boundsDrift,'concavitiesMayBeFilled':True});bm.free()
assert len(parts)==10
plan={'source':'NozzleWall_v80/nozzle-wall-clay.blend','method':'Ten closed convex stone volumes from actual clipped source vertices, precision weld within1 micrometer before hull construction; extreme-position change limited to10 micrometers and measured per object. Pose and existing material fixed. Freeze rejected modifier stack in hidden backup. No automatic fidelity approval: filled concavities may overlap neighboring parts.','parts':parts,'status':'CONTACT_AND_CLAY_REVIEW_REQUIRED'};path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([{'object':p['object'],'vertices':len(p['vertices']),'faces':len(p['faces']),'volume':p['signedVolume'],'boundsDrift':p['boundsDriftMeters']} for p in parts]))
