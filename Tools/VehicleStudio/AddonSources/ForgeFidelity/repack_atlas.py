"""Repack existing prepared UVs and measure area before starting another bake."""
import bpy,sys,json
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];source=Path(args[0]);out=Path(args[1]);out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source/'atlas-prepared.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:
 obj.select_set(True);obj.data.uv_layers.active=obj.data.uv_layers['BakeUV']
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.001)
bpy.ops.object.mode_set(mode='OBJECT')
area=0
for obj in meshes:
 mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers['BakeUV']
 for tri in mesh.loop_triangles:
  a,b,c=[uv.data[i].uv for i in tri.loops];area+=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))/2
bpy.ops.wm.save_as_mainfile(filepath=str(out/'atlas-prepared.blend'))
(out/'packing-receipt.json').write_text(json.dumps({'status':'UV_PACKING_REVIEW_ONLY','summedUVArea':area,'source':str(source),'marginFraction':.001},indent=2))
print('REPACKED_UV_AREA',area,flush=True)
