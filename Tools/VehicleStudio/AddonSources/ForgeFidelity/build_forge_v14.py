"""Shared material mapping, bellows mechanics and explicit close inspection views."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v13.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v13','ForgeFidelity/v14')
source=source.replace("scale.inputs[3].default_value=10", "scale.inputs[3].default_value=3")
extra='''
# All stones share a fixed physical mapping frame rather than repeating local object coordinates.
bpy.ops.object.empty_add(type='PLAIN_AXES',location=(0,0,0));mapping_frame=bpy.context.object;mapping_frame.name='Stone material mapping frame'
for node in stone.node_tree.nodes:
 if node.type=='TEX_COORD':node.object=mapping_frame
nodes=stone.node_tree.nodes;links=stone.node_tree.links;p=nodes.get('Principled BSDF')
previous=p.inputs['Base Color'].links[0].from_socket
info=nodes.new('ShaderNodeObjectInfo');variation=nodes.new('ShaderNodeValToRGB')
variation.color_ramp.elements[0].color=(.45,.41,.36,1);variation.color_ramp.elements[1].color=(1,.95,.87,1)
links.new(info.outputs['Random'],variation.inputs[0]);mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.55
links.new(previous,mix.inputs[1]);links.new(variation.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],p.inputs['Base Color'])
# Rear hinge plates, operating pins, and a secured nozzle flange.
for y in [-.108,.108]:
 cube('Bellows iron hinge cheek',(-.566,y,.31),(.008,.034,.245),iron,.003)
 for z in [.20,.31,.42]:
  rod('Bellows hinge pin',(-.58,y,z),(-.59,y,z),.010,iron)
 cube('Bellows support leg',(-.53,y,.135),(.026,.028,.26),iron,.003)
 cube('Bellows anchoring shoe',(-.53,y,.012),(.07,.06,.02),iron,.003)
cube('Bellows throat rectangular collar',(-.30,0,.31),(.026,.085,.10),iron,.005)
for y in [-.033,.033]:
 for z in [.274,.346]:rod('Throat flange fastener',(-.314,y,z),(-.324,y,z),.0055,iron)
# Bolt holes are real openings in each foot rather than painted dots.
for foot in [o for o in bpy.context.scene.objects if o.name.startswith('Securing foot')]:
 x,y,z=foot.location;hx=x+(.027 if x>0 else -.027)
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.009,depth=.10,location=(hx,y,z))
 cutter=bpy.context.object;bpy.context.view_layer.objects.active=foot
 mod=foot.modifiers.new('Actual mounting hole','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
source+='''
# Close views render the same authored scene; never image-generated model proof.
for label,location,target in [
 ('bellows-detail',(-.95,-.82,.62),(-.41,-.01,.30)),
 ('hearth-detail',(-.55,-1.05,.64),(0,-.10,.25))]:
 cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.data.lens=70;scene.render.resolution_x=1100;scene.render.resolution_y=900
 scene.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
'''
exec(compile(source,__file__,'exec'))
