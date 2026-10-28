"""Measure texture allocation by original material role, without asset mutation."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
roles={p['object']:p['role'] for p in json.loads((root/'material-assignment-fitted-v12.json').read_text())['assigned']}
bpy.ops.wm.open_mainfile(filepath=str(root/'fitted-atlas-source.blend'))
groups={}
for o in bpy.context.scene.objects:
    if o.name not in roles or not o.data.polygons:continue
    role=roles[o.name];r=groups.setdefault(role,{'objects':0,'uvArea':0,'surfaceArea':0,'pixelsAt2K':0})
    r['objects']+=1;uv=o.data.uv_layers.active.data
    for p in o.data.polygons:
        r['surfaceArea']+=p.area
        coords=[uv[i].uv for i in p.loop_indices]
        for a,b in zip(coords[1:-1],coords[2:]):
            da=a-coords[0];db=b-coords[0];r['uvArea']+=abs(da.x*db.y-da.y*db.x)/2
for r in groups.values():r['pixelsAt2K']=round(r['uvArea']*2048*2048)
(root/'atlas-allocation-audit.json').write_text(json.dumps({'roles':groups,'scope':'UV area allocation only, no visual fidelity claim'},indent=2));print(json.dumps(groups,indent=2))
