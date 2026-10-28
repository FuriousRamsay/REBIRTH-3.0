"""Assembly placement study from approved reference; no material work."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/LeftComposition_01')
if out.exists():raise RuntimeError('Preserve evidence')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/LeftAssembly_02/forge-clay.blend')
changed=[]
for o in bpy.context.scene.objects:
 if o.name.startswith('Reference left'):
  o.location.y+=.16;o.location.z+=.045;changed.append(o.name)
 if o.type=='MESH' and o.name.startswith(('Hollow crucible','Crucible')):
  bpy.context.view_layer.objects.active=o
  for m in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=m.name)
  inverse=o.matrix_world.inverted()
  for v in o.data.vertices:
   p=o.matrix_world@v.co
   p=Vector((-.34+(p.x+.19)*1.18,-.23+(p.y+.29)*1.18,.055+(p.z-.055)*1.15))
   v.co=inverse@p
  changed.append(o.name)
scene=bpy.context.scene;scene.render.filepath=str(out/'clay-composition.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge-clay.blend'))
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'UNACCEPTED_COMPOSITION_STUDY','changed':changed,'intent':'Crucible forward-left of furnace, bellows behind it; all changes geometry only'},indent=2))
