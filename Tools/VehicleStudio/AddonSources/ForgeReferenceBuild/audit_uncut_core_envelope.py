import bpy,bmesh,json
from pathlib import Path
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CleanCoreEnvelopeAudit_v54';out.mkdir(parents=True,exist_ok=True)
plan=json.loads((repo/'upright-furnace-envelope.json').read_text());mesh=bpy.data.meshes.new('Read-only source envelope');mesh.from_pydata(plan['vertices'],[],plan['faces']);mesh.update();bm=bmesh.new();bm.from_mesh(mesh)
report={'source':'upright-furnace-envelope.json','vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'signedVolume':bm.calc_volume(signed=True),'limits':'Tests uncut construction mesh, not final appearance or replacement fit.'}
path=out/'envelope-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));bm.free();print(json.dumps(report))
