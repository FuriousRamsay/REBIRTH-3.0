import bpy,bmesh,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'MasonryDefectClasses_v75';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CurveExportWelds_v74/welded-seams-clay.blend'));deps=bpy.context.evaluated_depsgraph_get();records=[]
def inspect(mesh):
    bm=bmesh.new();bm.from_mesh(mesh);r={'vertices':len(bm.verts),'faces':len(bm.faces),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'zeroAreaFaces':sum(f.calc_area()<1e-12 for f in bm.faces),'volume':bm.calc_volume(signed=True)};bm.free();return r
for obj in bpy.context.scene.objects:
    if obj.hide_render or obj.type!='MESH' or not obj.name.startswith(('stone_','inferred_rear_stone_','planned_mesh_113')):continue
    raw=inspect(obj.data);ev=obj.evaluated_get(deps);mesh=ev.to_mesh();evaluated=inspect(mesh);ev.to_mesh_clear()
    if not(raw['nonManifoldEdges'] or raw['zeroAreaFaces'] or raw['volume']<=0 or evaluated['nonManifoldEdges'] or evaluated['zeroAreaFaces'] or evaluated['volume']<=0):continue
    category='RAW_GEOMETRY' if raw['nonManifoldEdges'] or raw['zeroAreaFaces'] or raw['volume']<=0 else 'MODIFIER_OUTPUT'
    records.append({'object':obj.name,'category':category,'raw':raw,'evaluated':evaluated,'modifiers':[{'name':m.name,'type':m.type,'width':getattr(m,'width',None),'limitMethod':getattr(m,'limit_method',None),'angleLimit':getattr(m,'angle_limit',None),'clampOverlap':getattr(m,'use_clamp_overlap',None)} for m in obj.modifiers]})
report={'source':'CurveExportWelds_v74/welded-seams-clay.blend','objects':records,'status':'CLASSIFIED_REPAIRS_REQUIRED'};path=out/'classification.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps({'rawIssues':sum(r['category']=='RAW_GEOMETRY' for r in records),'modifierIssues':sum(r['category']=='MODIFIER_OUTPUT' for r in records),'modifierCases':[r for r in records if r['category']=='MODIFIER_OUTPUT']}))
