import bpy,bmesh,json,sys
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'FullGeometryAudit_v72';out.mkdir(parents=True,exist_ok=True)
source='CrucibleTopology_v71/crucible-clean-clay.blend'
if '--closed-patches' in sys.argv:
    source='ClippedStoneVolumes_v81/closed-patches-clay.blend';out=root/'FullGeometryAudit_v82';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/source));deps=bpy.context.evaluated_depsgraph_get();records=[]
for obj in bpy.context.scene.objects:
    if obj.hide_render or obj.type not in ('MESH','CURVE') or obj.name=='Inspection ground':continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();bm=bmesh.new();bm.from_mesh(mesh)
    records.append({'object':obj.name,'type':obj.type,'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'wireEdges':sum(e.is_wire for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True)})
    bm.free();ev.to_mesh_clear()
issues=[r for r in records if r['nonManifoldEdges'] or r['zeroAreaFaces'] or r['signedVolume']<=0]
report={'source':source,'visibleObjects':len(records),'totalVertices':sum(r['vertices'] for r in records),'totalFaces':sum(r['faces'] for r in records),'objects':records,'issues':issues,'scope':'Evaluated mesh topology and volume, including curve conversion. Intentional thin sheets must be classified separately. This does not prove visual fidelity, collisions or UV quality.'}
path=out/'geometry-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps({'visibleObjects':len(records),'totalVertices':report['totalVertices'],'totalFaces':report['totalFaces'],'issues':issues}))
