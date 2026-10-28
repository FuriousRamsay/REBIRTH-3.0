"""Freeze evaluated geometry and pack all fitted meshes into one UV atlas."""
import bpy,json,hashlib
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
bpy.ops.wm.open_mainfile(filepath=str(root/'source-materials-fitted-v12.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground']
deps=bpy.context.evaluated_depsgraph_get()
def points_signature(o):
    ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    rows=sorted(tuple(round(float(c),6) for c in v.co) for v in mesh.vertices)
    result=hashlib.sha256(repr(rows).encode()).hexdigest();ev.to_mesh_clear();return result
before={o.name:points_signature(o) for o in objects}
for o in objects:
    bpy.context.view_layer.objects.active=o
    for m in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=m.name)
assert all(points_signature(o)==before[o.name] for o in objects),'Evaluated surface changed during modifier freeze'
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.004,area_weight=.5,correct_aspect=True,scale_to_bounds=True)
bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,margin=.004)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'fitted-atlas-source.blend'))
(root/'fitted-atlas-receipt.json').write_text(json.dumps({'objects':len(objects),'evaluatedGeometryPreserved':True,'modifiersFrozenBeforeUV':True,'globalPackExplicit':True,'resolution':[2048,2048],'status':'ATLAS_PREPARED_VISUAL_VALIDATION_PENDING'},indent=2));print('FITTED_ATLAS_PREPARED')
