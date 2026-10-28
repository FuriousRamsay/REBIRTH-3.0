import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BevelTriangleAudit_v77';out.mkdir(parents=True,exist_ok=True);classes=json.loads((root/'MasonryDefectClasses_v75/classification.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/classes['source']));deps=bpy.context.evaluated_depsgraph_get();records=[]
for record in classes['objects']:
    if record['category']!='MODIFIER_OUTPUT':continue
    obj=bpy.data.objects[record['object']];ev=obj.evaluated_get(deps);mesh=ev.to_mesh();mesh.calc_loop_triangles();areas=[];degenerate=[]
    for tri in mesh.loop_triangles:
        a,b,c=[obj.matrix_world@mesh.vertices[i].co for i in tri.vertices];area=(b-a).cross(c-a).length*.5;areas.append(area)
        if area<1e-12:degenerate.append({'polygon':tri.polygon_index,'vertices':list(tri.vertices),'areaSquareMeters':area})
    records.append({'object':obj.name,'triangles':len(areas),'exactOrNearZeroTriangles':sum(a<1e-18 for a in areas),'tinyTrianglesBelow1e12':len(degenerate),'totalSurfaceAreaSquareMeters':sum(areas),'tinyAreaFraction':sum(d['areaSquareMeters'] for d in degenerate)/sum(areas),'flaggedTriangles':degenerate});ev.to_mesh_clear()
report={'source':classes['source'],'objects':records,'scope':'Actual evaluated triangulation. Tiny-area fraction quantifies rendering coverage, not a fidelity or collision pass.'};path=out/'triangle-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps([{k:v for k,v in r.items() if k!='flaggedTriangles'} for r in records]))
