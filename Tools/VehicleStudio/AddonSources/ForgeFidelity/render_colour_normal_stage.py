"""Actual partial bake comparison: colour and normal only, explicitly not full material proof."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);out=Path(args[1])
if out.exists():raise RuntimeError('Preserve prior comparison')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath=str(folder/'atlas-prepared.blend'))
colour=bpy.data.images.load(str(folder/'albedo.png'));colour.colorspace_settings.name='sRGB'
normal=bpy.data.images.load(str(folder/'normal.png'));normal.colorspace_settings.name='Non-Color'
materials=set(m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m)
for material in materials:
 nodes=material.node_tree.nodes;links=material.node_tree.links;p=nodes.get('Principled BSDF')
 uv=nodes.new('ShaderNodeUVMap');uv.uv_map='BakeUV'
 for image,socket in [(colour,'Base Color'),(normal,'Normal')]:
  tex=nodes.new('ShaderNodeTexImage');tex.image=image;links.new(uv.outputs[0],tex.inputs['Vector'])
  if socket=='Normal':
   decode=nodes.new('ShaderNodeNormalMap');decode.space='TANGENT';decode.uv_map='BakeUV';links.new(tex.outputs['Color'],decode.inputs['Color']);links.new(decode.outputs[0],p.inputs['Normal'])
  else:links.new(tex.outputs['Color'],p.inputs[socket])
scene=bpy.context.scene;scene.cycles.samples=32;cam=scene.camera
cam.location=(-.95,-.82,.62);cam.rotation_euler=(Vector((-.41,-.01,.30))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=70
scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.filepath=str(out/'actual-4k-colour-normal-bellows.png');bpy.ops.render.render(write_still=True)
(out/'render-receipt.json').write_text(json.dumps({'status':'COLOUR_NORMAL_ONLY_NOT_COMPLETE_BAKED_PROOF','maps':'Actual4096 colour and tangent normal','otherChannels':'Original roughness/metallic/emission','geometry':'v21 baseline, not v23 current topology','lights':'Saved source studio,32samples'},indent=2))
