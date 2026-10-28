"""Actual geometry-only baseline. Preserves original saved scene and materials."""
import bpy, json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayGate_v23')
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v23/forge.blend')
scene=bpy.context.scene
clay=bpy.data.materials.new('Geometry review neutral clay')
clay.use_nodes=True
p=clay.node_tree.nodes.get('Principled BSDF')
p.inputs['Base Color'].default_value=(.36,.36,.36,1)
p.inputs['Roughness'].default_value=.72
for obj in scene.objects:
 if obj.type=='MESH':
  obj.data.materials.clear();obj.data.materials.append(clay)
 if obj.type=='LIGHT':
  obj.data.color=(1,1,1)
  if obj.data.type=='POINT':obj.hide_render=True
scene.cycles.samples=32
scene.render.resolution_x=1400;scene.render.resolution_y=1100
scene.render.resolution_percentage=100
cam=scene.camera
cam.location=(-1.05,-1.8,1.05)
cam.rotation_euler=(Vector((-.09,-.015,.29))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.lens=60
scene.render.filepath=str(out/'clay-baseline.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'clay-baseline.blend'))
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'GEOMETRY_BASELINE_NOT_ACCEPTED','source':'v23/forge.blend','textures':False,'emission':False,'resolution':[1400,1100]},indent=2))
