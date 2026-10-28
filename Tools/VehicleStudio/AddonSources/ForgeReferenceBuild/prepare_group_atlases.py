"""Reallocate fitted UV islands to three 2K material atlases."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
bpy.ops.wm.open_mainfile(filepath=str(root/'fitted-atlas-source.blend'))
roles={p['object']:p['role'] for p in json.loads((root/'material-assignment-fitted-v12.json').read_text())['assigned']}
groupFor={'stone':'masonry','mortar':'masonry','coal':'masonry','iron':'metal','wood':'organic','leather':'organic','crucible':'organic'}
groups={k:[] for k in ['masonry','metal','organic']}
for o in bpy.context.scene.objects:
    if o.name in roles and o.type=='MESH' and o.data.polygons:groups[groupFor[roles[o.name]]].append(o)
plan={'groups':{g:[o.name for o in obs] for g,obs in groups.items()},'resolutionPerMap':[2048,2048],'marginFraction':.001,'paddingPixelsApprox':2,'mortarUvLinearWeight':.4,'coalUvLinearWeight':2,'reason':'Shared atlas measured16percent area occupancy and insufficient organic texel allocation','status':'PACKING_AND_BAKE_REVIEW_REQUIRED'}
(root/'group-atlas-plan.json').write_text(json.dumps(plan,indent=2))
for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
        role=roles[o.name]
        if role in ('mortar','coal'):
            factor=.4 if role=='mortar' else 2
            for uv in o.data.uv_layers.active.data:uv.uv*=factor
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True,margin=.001,margin_method='FRACTION')
    bpy.ops.object.mode_set(mode='OBJECT')
    print('PACKED_GROUP',name,len(objects))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'group-atlas-source.blend'))
allocation={}
for group,objects in groups.items():
    for o in objects:
        r=allocation.setdefault(roles[o.name],{'group':group,'uvArea':0,'texelsAt2K':0})
        data=o.data.uv_layers.active.data
        for f in o.data.polygons:
            pts=[data[i].uv for i in f.loop_indices]
            for a,b in zip(pts[1:-1],pts[2:]):
                da=a-pts[0];db=b-pts[0];r['uvArea']+=abs(da.x*db.y-da.y*db.x)/2
for r in allocation.values():r['texelsAt2K']=round(r['uvArea']*2048**2)
(root/'group-atlas-allocation.json').write_text(json.dumps(allocation,indent=2));print(json.dumps(allocation))
