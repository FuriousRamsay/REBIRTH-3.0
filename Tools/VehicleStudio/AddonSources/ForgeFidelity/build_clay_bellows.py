"""Geometry-only bellows proportion experiment; not accepted reference fidelity."""
import bpy, math, json
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayBellows_02')
if out.exists():raise RuntimeError('Preserve existing experiment')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayGate_v23/clay-baseline.blend')
prefixes=('Pleated fan','Fan shaped','Raised leather','Oak operating','Bellows','Operating hinge','Leather','Individual leather')
changed=[]
for obj in list(bpy.context.scene.objects):
 if obj.type!='MESH' or not obj.name.startswith(prefixes):continue
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 inverse=obj.matrix_world.inverted()
 for vertex in obj.data.vertices:
  p=obj.matrix_world@vertex.co
  # Throat stays fixed; compact rear frame moves inward with the envelope.
  if p.x<-.28:p.x=-.28+(p.x+.28)*.78
  if obj.name.startswith(('Pleated fan','Fan shaped','Raised leather','Leather','Individual leather')):
   p.z=.31+(p.z-.31)*1.45
  vertex.co=inverse@p
 changed.append(obj.name)
scene=bpy.context.scene
scene.render.filepath=str(out/'clay-bellows.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge-clay.blend'))
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'PROPORTION_EXPERIMENT_NOT_ACCEPTED','changed':changed,'textures':False,'UnityWork':False},indent=2))

