import bpy,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkApprovedTextured_20261020/approved-truck.blend')
deps=bpy.context.evaluated_depsgraph_get()
body=BVHTree.FromObject(bpy.data.objects['body'],deps)
report={}
for o in bpy.data.collections['PickupOnlyInfill'].objects:
 if any(k in o.name for k in ['lamp','flute','latch plate','license']):
  points=[o.matrix_world@v.co for v in o.data.vertices]
  report[o.name]={'bounds':[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]}
samples=[]
for side in [-1,1]:
 for x in [.767,.79,.817,.84,.867]:
  for h in [.742,.76,.802,.877,.963,1.01,1.028]:
   hit,normal,idx,dist=body.ray_cast(Vector((side*x,3.2,h)),Vector((0,-1,0)),.7)
   if hit:samples.append({'x':side*x,'height':h,'rearDepth':hit.y})
report['native_lamp_mount_samples']=samples
Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkApprovedTextured_20261020/rear-fit-inspection.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
