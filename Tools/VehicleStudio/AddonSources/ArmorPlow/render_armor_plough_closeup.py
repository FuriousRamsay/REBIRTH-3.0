import bpy
from pathlib import Path
from mathutils import Vector,Matrix
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
O=P/'ArmorAssemblyReview'
bpy.ops.wm.open_mainfile(filepath=str(O/'armor-assembly-review.blend'))
with bpy.data.libraries.load(str(P/'ReferenceFinish/reference-finish-study.blend'),link=False) as (src,dst):
 dst.materials=[n for n in src.materials if n in ['Reference ochre remnants on dark plough steel','Reference worn spike and cutting steel','Reference oxidized fasteners']]
mats={m.name.split('.00')[0]:m for m in dst.materials if m}
for name in ['Reference plough clay','Camper mounted plough receivers']:
 col=bpy.data.collections[name];col.hide_render=False
 for obj in col.objects:
  if obj.type not in ['MESH','CURVE']:continue
  key='Reference oxidized fasteners' if any(k in obj.name for k in ['fastener','washer','bolt','pin']) else 'Reference worn spike and cutting steel' if any(k in obj.name for k in ['spike','tooth','shank']) else 'Reference ochre remnants on dark plough steel'
  obj.data.materials.clear();obj.data.materials.append(mats[key])
scene=bpy.context.scene;scene.cycles.samples=48
for name,eye,target,scale in [('armor-plough-closeup',(4,2.65,6),(.1,1.05,1.35),3.8),('armor-plough-mount-closeup',(4,1.15,3.2),(.65,.72,2.0),1.8)]:
 cam=scene.camera;cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
