"""Inspect the saved UV state; no render/bake acceptance inferred from numbers."""
import bpy,sys,json
from pathlib import Path
folder=Path(sys.argv[sys.argv.index('--')+1]);bpy.ops.wm.open_mainfile(filepath=str(folder/'atlas-prepared.blend'))
report=[];total_area=0;total_triangles=0;wrong=[]
for obj in bpy.context.scene.objects:
 if obj.type!='MESH':continue
 mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.get('BakeUV')
 if not uv:wrong.append(obj.name);continue
 if not uv.active_render:wrong.append(obj.name+' render UV')
 area=0
 for tri in mesh.loop_triangles:
  a,b,c=[uv.data[i].uv for i in tri.loops]
  area+=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))/2
 total_area+=area;total_triangles+=len(mesh.loop_triangles)
 report.append({'name':obj.name,'triangles':len(mesh.loop_triangles),'uvArea':area,'renderUV':next((u.name for u in mesh.uv_layers if u.active_render),None)})
receipt={'status':'UV_STATE_AUDIT_NOT_FIDELITY_ACCEPTANCE','triangles':total_triangles,'summedUVArea':total_area,'wrongUV':wrong,'objects':report}
(folder/'uv-state-audit.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps({k:v for k,v in receipt.items() if k!='objects'}))
