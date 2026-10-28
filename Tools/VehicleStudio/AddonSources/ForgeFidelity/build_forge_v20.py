"""Correct v19's flat cup colour and uniform vent emission without hiding geometry."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v20')
if out.exists():raise RuntimeError('Preserve prior candidate')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v19/forge.blend')
cup=bpy.data.materials['Sooted refractory crucible'];nodes=cup.node_tree.nodes;links=cup.node_tree.links;p=nodes.get('Principled BSDF')
image=nodes.get('Authored stone albedo');mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.8,.62,.30,1)
links.new(image.outputs['Color'],mix.inputs[1]);links.new(mix.outputs[0],p.inputs['Base Color'])
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Hollow crucible')]:
 for polygon in obj.data.polygons:
  if polygon.index<24 or 48<=polygon.index<72:polygon.use_smooth=True
hot=bpy.data.materials['Vent recessed hot inner iron'];nodes=hot.node_tree.nodes;links=hot.node_tree.links;p=nodes.get('Principled BSDF')
field=nodes.new('ShaderNodeTexNoise');field.inputs['Scale'].default_value=7;field.inputs['Detail'].default_value=5
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.3;ramp.color_ramp.elements[0].color=(.008,.001,0,1)
ramp.color_ramp.elements[1].position=.7;ramp.color_ramp.elements[1].color=(.8,.16,.006,1)
links.new(field.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Emission Color'])
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
for name,loc,target,lens,w,h in [('geometry-review',(-1.05,-1.8,1.05),(-.09,-.015,.29),60,1400,1100),('hearth-detail',(-.55,-1.05,.64),(0,-.10,.25),70,1100,900)]:
 cam=scene.camera;cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
 scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'MATERIAL_REGRESSION_REVIEW_NOT_ACCEPTED','source':'v19 saved scene','changes':['Mineral texture restored with refractory tint','Smooth cup wall normals, chipped lip retained','Variable vent interior emission'],'lights':'Unchanged v19 studio'},indent=2))
