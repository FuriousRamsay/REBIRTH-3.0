import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CoreTopologyAudit_v50';out.mkdir(parents=True,exist_ok=True);records=[]
def inspect(mesh):
    bm=bmesh.new();bm.from_mesh(mesh);boundary=[e for e in bm.edges if e.is_boundary]
    report={'vertices':len(bm.verts),'faces':len(bm.faces),'boundaryEdges':len(boundary),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'looseEdges':sum(e.is_wire for e in bm.edges),'boundaryLengthMeters':sum(e.calc_length() for e in boundary),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces)}
    bm.free();return report
for label,path in [('baseline',root/'HearthLipRecession_v47/lip-recession-clay.blend'),('cut',root/'UpperCavityRebuild_v49/upper-cavity-clay.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(path));obj=bpy.data.objects['furnace_core'];ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    records.append({'label':label,'source':str(path),'raw':inspect(obj.data),'evaluated':inspect(mesh),'modifiers':[{'name':m.name,'type':m.type,'width':getattr(m,'width',None),'segments':getattr(m,'segments',None)} for m in obj.modifiers]});ev.to_mesh_clear()
path=out/'core-topology-audit.json';assert not path.exists();path.write_text(json.dumps(records,indent=2));print(json.dumps(records))
