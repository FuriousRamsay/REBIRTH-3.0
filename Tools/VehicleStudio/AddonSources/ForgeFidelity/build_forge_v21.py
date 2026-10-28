"""Reference bellows timber frame proportions; remove long unrelated metal stilts."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v21')
if out.exists():raise RuntimeError('Preserve previous candidate')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v20/forge.blend')
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Oak operating upright'):
  obj.location.z=.285;obj.dimensions=(.032,.040,.380)
 elif obj.name.startswith('Bellows iron hinge cheek'):
  obj.location.z=.285;obj.dimensions=(.008,.034,.355)
 elif obj.name.startswith('Bellows support leg'):
  obj.location.z=.059;obj.dimensions=(.040,.040,.095)
 elif obj.name.startswith('Bellows rear cross shaft'):
  obj.location.z=.12 if obj.location.z<.31 else .445
 elif obj.name.startswith(('Bellows hinge pin','Operating hinge bolt')):
  obj.location.z=.12 if obj.location.z<.25 else (.445 if obj.location.z>.36 else .285)
 # Apply changed scales before evaluating authored bevel widths.
 if obj.name.startswith(('Oak operating upright','Bellows iron hinge cheek','Bellows support leg')):
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
for label,loc,target,lens,w,h in [('geometry-review',(-1.05,-1.8,1.05),(-.09,-.015,.29),60,1400,1100),('bellows-detail',(-.95,-.82,.62),(-.41,-.01,.30),70,1100,900)]:
 cam=scene.camera;cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
 scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(out/(label+'.png'));bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'BELLOWS_FRAME_REVIEW_NOT_ACCEPTED','source':'v20 saved scene','authoredEstimatesM':{'timberUprightHeight':.380,'bottomShaftHeight':.12,'topShaftHeight':.445,'metalSupportHeight':.095},'lights':'Unchanged v20 studio'},indent=2))
