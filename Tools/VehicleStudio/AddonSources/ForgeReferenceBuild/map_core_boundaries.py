import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CoreBoundaryMap_v52';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'HearthLipRecession_v47/lip-recession-clay.blend'))
obj=bpy.data.objects['furnace_core'];bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();bm.verts.index_update();edges=[e for e in bm.edges if e.is_boundary];adj={}
for e in edges:
    a,b=e.verts;adj.setdefault(a.index,set()).add(b.index);adj.setdefault(b.index,set()).add(a.index)
unseen=set(adj);groups=[]
while unseen:
    start=min(unseen);stack=[start];indices=set()
    while stack:
        i=stack.pop()
        if i in indices:continue
        indices.add(i);unseen.discard(i);stack.extend(adj[i]-indices)
    coords=[obj.matrix_world@bm.verts[i].co for i in indices]
    groups.append({'id':len(groups),'vertexCount':len(indices),'edgeCount':sum(len(adj[i]) for i in indices)//2,'isSimpleClosedLoop':all(len(adj[i])==2 for i in indices),'boundsMin':[min(p[k] for p in coords) for k in range(3)],'boundsMax':[max(p[k] for p in coords) for k in range(3)],'vertices':[{'index':i,'position':list(obj.matrix_world@bm.verts[i].co),'neighbors':sorted(adj[i])} for i in sorted(indices)]})
report={'source':'HearthLipRecession_v47/lip-recession-clay.blend','boundaryEdges':len(edges),'groups':groups,'status':'BOUNDARY_ROLES_REQUIRE_INSPECTION_NO_FILL_AUTHORIZED_BY_COUNT'}
path=out/'boundary-map.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));bm.free();print(json.dumps([{k:v for k,v in g.items() if k!='vertices'} for g in groups]))
