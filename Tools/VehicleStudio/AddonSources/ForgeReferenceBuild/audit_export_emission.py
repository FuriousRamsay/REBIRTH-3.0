"""Compare source and reimported FBX UV coverage of the actual emission map."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/GroupedBaked2K_v3')
bpy.ops.wm.open_mainfile(filepath=str(root/'grouped-baked-only.blend'))
plan=json.loads((root.parent/'group-atlas-plan.json').read_text())['groups']
image=bpy.data.images.load(str(root/'masonry/forge-emission-2k.png'),check_existing=False)
pixels=list(image.pixels);w,h=image.size
def audit(objects):
    result=[]
    for o in objects:
        uv=o.data.uv_layers.active
        if not uv:continue
        bright=0;maximum=0
        for p in o.data.polygons:
            coords=[uv.data[i].uv for i in p.loop_indices]
            u=sum(c.x for c in coords)/len(coords);v=sum(c.y for c in coords)/len(coords)
            x=min(w-1,max(0,int(u*w)));y=min(h-1,max(0,int(v*h)))
            value=max(pixels[4*(y*w+x):4*(y*w+x)+3]);maximum=max(maximum,value)
            bright+=value>.02
        result.append({'object':o.name,'polygons':len(o.data.polygons),'brightEmissionCentroids':bright,'maxEmission':maximum})
    return result
source=audit([bpy.data.objects[n] for n in plan['masonry']])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'UnityExport/ForgeCompact.fbx'))
export=audit([o for o in bpy.context.scene.objects if o.type=='MESH' and o.name=='Forge_masonry'])
report={'sourceBright':[r for r in source if r['brightEmissionCentroids']], 'sourceBrightTotal':sum(r['brightEmissionCentroids'] for r in source),'fbxReimport':export,'scope':'Map UV sample audit; does not validate appearance'}
(root/'UnityExport/emission-uv-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
