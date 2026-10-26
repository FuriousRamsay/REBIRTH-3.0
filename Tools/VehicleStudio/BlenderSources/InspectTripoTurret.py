import bpy,json
from pathlib import Path
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TripoTurret_20261018')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(next((P/'Input').glob('*.fbx'))))
out={'objects':[],'images':[]}
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 o.data.calc_loop_triangles()
 out['objects'].append({'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'triangles':len(o.data.loop_triangles),'dimensions':list(o.dimensions),'scale':list(o.scale),'uv_layers':[u.name for u in o.data.uv_layers],'materials':[m.name for m in o.data.materials if m]})
for im in bpy.data.images:out['images'].append({'name':im.name,'size':list(im.size),'path':im.filepath})
(P/'source-survey.json').write_text(json.dumps(out,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Output'/'source-inspection.blend'))
