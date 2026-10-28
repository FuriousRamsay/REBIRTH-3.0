"""Inspect actual saved map-only scene and its used material bindings."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/GroupedBaked2K_v3')
bpy.ops.wm.open_mainfile(filepath=str(root/'grouped-baked-only.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground' and o.data.polygons]
used={m for o in objects for m in o.data.materials if m};records=[]
for m in used:
    images=[n for n in m.node_tree.nodes if n.type=='TEX_IMAGE'];assert len(images)==5
    assert all(n.image and tuple(n.image.size)==(2048,2048) for n in images)
    assert not any(n.type in ('TEX_NOISE','TEX_VORONOI') for n in m.node_tree.nodes)
    records.append({'material':m.name,'imageFiles':[n.image.filepath for n in images],'emissionStrength':m.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value})
report={'sceneReopened':True,'renderMeshObjects':len(objects),'usedMaterials':records,'vertices':sum(len(o.data.vertices) for o in objects),'polygons':sum(len(o.data.polygons) for o in objects),'visibleTextObjects':[o.name for o in bpy.context.scene.objects if o.type=='FONT' and not o.hide_render],'scope':'Verifies saved mesh and map bindings, not reference appearance or game performance','fidelity':'VISUAL_REVIEW_PENDING','UnityVerified':False}
(root/'saved-scene-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='usedMaterials'}))
