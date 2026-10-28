import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrucibleTopology_v71';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CrucibleRimBreaks_v70/rim-breaks-clay.blend'));obj=bpy.data.objects['planned_mesh_002'];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.index_update();bm.edges.index_update();records=[]
for edge in bm.edges:
    if not edge.is_manifold:records.append({'edgeIndex':edge.index,'vertexIndices':[v.index for v in edge.verts],'worldEndpoints':[list(obj.matrix_world@v.co) for v in edge.verts],'linkedFaces':len(edge.link_faces),'isWire':edge.is_wire,'isBoundary':edge.is_boundary,'lengthMeters':edge.calc_length()})
report={'source':'CrucibleRimBreaks_v70/rim-breaks-clay.blend','object':obj.name,'edges':records,'status':'CLASSIFICATION_REQUIRED_NO_EDIT'};path=out/'edge-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));bm.free();print(json.dumps(report))
