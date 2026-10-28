"""Inspect actual baked colour on prepared geometry; other material channels remain source."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);out=Path(args[1])
if out.exists():raise RuntimeError('Preserve colour-stage evidence')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath=str(folder/'atlas-prepared.blend'))
image=bpy.data.images.load(str(folder/'albedo.png'));image.colorspace_settings.name='sRGB'
materials=set(m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m)
for material in materials:
 nodes=material.node_tree.nodes;links=material.node_tree.links;p=nodes.get('Principled BSDF')
 tex=nodes.new('ShaderNodeTexImage');tex.image=image;uv=nodes.new('ShaderNodeUVMap');uv.uv_map='BakeUV'
 links.new(uv.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
scene=bpy.context.scene;scene.cycles.samples=32;cam=scene.camera
cam.location=(-.95,-.82,.62);cam.rotation_euler=(Vector((-.41,-.01,.30))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=70
scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.filepath=str(out/'actual-4k-colour-bellows.png');bpy.ops.render.render(write_still=True)
(out/'render-receipt.json').write_text(json.dumps({'status':'COLOUR_ONLY_INSPECTION_NOT_COMPLETE_BAKED_PROOF','source':str(folder),'colour':'Actual4096 baked albedo','otherChannels':'Original source normals/roughness/metallic/emission; not yet complete bake','geometry':'v21 reduced baseline, not latestv23 topology'},indent=2))
