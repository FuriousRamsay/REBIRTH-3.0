"""Export verified map-only mesh geometry as three material groups for Unity."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/GroupedBaked2K_v3');out=root/'UnityExport';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'grouped-baked-only.blend'))
plan=json.loads((root.parent/'group-atlas-plan.json').read_text())['groups'];merged=[]
for group,names in plan.items():
    bpy.ops.object.select_all(action='DESELECT');objects=[bpy.data.objects[name] for name in names]
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name='Forge_'+group
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);merged.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in merged:o.select_set(True)
bpy.context.view_layer.objects.active=merged[0]
bpy.ops.export_scene.fbx(filepath=str(out/'ForgeCompact.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=False,mesh_smooth_type='FACE',use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
report={'source':str(root/'grouped-baked-only.blend'),'groups':[{'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'uvLayers':[u.name for u in o.data.uv_layers]} for o in merged],'status':'FBX_EXPORTED_UNITY_IMPORT_REVIEW_PENDING','scope':'Static geometry/material groups only; no bundle or installation'}
(out/'export-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
