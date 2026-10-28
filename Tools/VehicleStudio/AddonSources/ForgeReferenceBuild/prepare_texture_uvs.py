"""Prepare a unique UV atlas on a disposable copy; preserve source geometry."""
import bpy, json, hashlib
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
bpy.ops.wm.open_mainfile(filepath=str(out/'source-materials-v9.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground']
def signature(o):
    return hashlib.sha256(repr(([(tuple(v.co)) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons])).encode()).hexdigest()
before={o.name:signature(o) for o in objects}
bpy.ops.object.select_all(action='DESELECT')
for o in objects: o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.151917, island_margin=.004, area_weight=.5, correct_aspect=True, scale_to_bounds=True)
bpy.ops.object.mode_set(mode='OBJECT')
assert all(signature(o)==before[o.name] for o in objects), 'Geometry changed during UV preparation'
report={'meshObjects':len(objects),'geometryUnchanged':True,'targetMapResolution':[2048,2048],'uvLoops':sum(len(o.data.uv_layers.active.data) for o in objects),'status':'UV_PREPARED_BAKE_PENDING','limits':'Packing/seams still require visual validation. Material appearance remains provisional.'}
bpy.ops.wm.save_as_mainfile(filepath=str(out/'uv-prepared-v9.blend'))
(out/'uv-preparation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
