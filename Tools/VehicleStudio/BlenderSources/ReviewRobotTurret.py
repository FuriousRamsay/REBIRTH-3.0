import bpy,math,json
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019')
bpy.ops.wm.open_mainfile(filepath=str(P/'Output/source.blend'))
texdir=next((P/'Input').glob('*.fbm'))
mat=bpy.data.materials.new('Robot source PBR');mat.use_nodes=True
n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF')
for token,socket in [('basecolor','Base Color'),('roughness','Roughness'),('metallic','Metallic'),('normal','Normal')]:
 im=bpy.data.images.load(str(next(p for p in texdir.iterdir() if token in p.name)),check_existing=True)
 im.colorspace_settings.name='sRGB' if token=='basecolor' else 'Non-Color'
 t=n.new('ShaderNodeTexImage');t.image=im
 if token=='normal':
  nm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],bs.inputs['Normal'])
 else:l.new(t.outputs['Color'],bs.inputs[socket])
for o in bpy.context.scene.objects:
 if o.type=='MESH':o.data.materials.clear();o.data.materials.append(mat)
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.10,.13,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.55
target=Vector((0,0,.42))
for name,pos,power,size in [('Key',(-2,-3,4),250,3),('Fill',(2,1,2),150,2),('Rim',(-1,3,2),200,2)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=pos;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('ReviewCamera',bpy.data.cameras.new('ReviewCamera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.1
for name,eye in [('source-side',(-3,0,.42)),('source-front',(0,-3,.42)),('source-top',(0,0,4)),('source-quarter',(-2,-3,1.8))]:
 cam.location=eye;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(P/'Output'/f'{name}.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Output/source-review.blend'))
