"""Read-only close-up renders of the owner-selected model; no material/mesh edits."""
import bpy, json
from pathlib import Path
from mathutils import Vector, Matrix

OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkApprovedTextured_20261020')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'approved-truck.blend'))
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.resolution_x=1800;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.view_settings.exposure=-1.5
def U(p):return Vector((p[0],-p[2],p[1]))
for o in bpy.data.collections['Studio'].objects:o.hide_set(False)
cam=sc.camera
lights=[bpy.data.objects[n] for n in ['Key','Fill','Rim']]
def render(name,target,direction,scale):
 at=U(target);forward=-U(direction).normalized();up=Vector((0,0,1));right=forward.cross(up).normalized();up=right.cross(forward)
 cam.location=at+U(direction);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=scale
 for obj,loc in zip(lights,[at-forward*4-right*3+up*4,at-forward*2+right*4+up,at+forward*3-right+up*3]):
  obj.location=loc;obj.rotation_euler=(at-loc).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)

rear=bpy.data.objects['Rear bumper - straight span rounded returns']
front=bpy.data.objects['parts']
report={'rear_material':rear.data.materials[0].name,'original_parts_material':front.data.materials[0].name,'same_material_object':rear.data.materials[0]==front.data.materials[0],
        'rear_uv_layer':rear.data.uv_layers.active.name,'model_modified':False,'camera_relative_lighting':'identical for both views'}
(OUT/'bumper-material-comparison.json').write_text(json.dumps(report,indent=2))
render('rear-bumper-closeup',(0,.64,-2.68),(2,.85,-6),2.35)
render('front-bumper-comparison',(0,.64,2.03),(-2,.85,6),2.35)
print('APPROVED_BUMPER_RENDERS_COMPLETE')
