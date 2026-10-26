"""Rebuild low UV layout and transfer all source maps; avoids distorted inherited UV interpolation."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TripoTurret_20261018');O=P/'Output'
bpy.ops.wm.open_mainfile(filepath=str(O/'RustyCombatTurret_Optimized.blend'))
source=bpy.data.objects['Turret_Source_287191'];low=bpy.data.objects['Turret_LOD0'];source.hide_set(False);source.hide_render=False;low.hide_set(False);low.hide_render=False
for i in (1,2):bpy.data.objects.remove(bpy.data.objects['Turret_LOD'+str(i)],do_unlink=True)
bpy.ops.object.select_all(action='DESELECT');low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(75),island_margin=.006);bpy.ops.object.mode_set(mode='OBJECT')
sc=bpy.context.scene;sc.cycles.samples=8;sc.render.bake.use_selected_to_active=True;sc.render.bake.cage_extrusion=.006;sc.render.bake.max_ray_distance=.02;sc.render.bake.margin=4
srcmat=source.data.materials[0];sn=srcmat.node_tree.nodes;sl=srcmat.node_tree.links;bs=sn.get('Principled BSDF');out=sn.get('Material Output');emit=sn.new('ShaderNodeEmission')
target=bpy.data.materials.new('RebakeTarget');target.use_nodes=True;tn=target.node_tree.nodes;node=tn.new('ShaderNodeTexImage');tn.active=node;low.data.materials.clear();low.data.materials.append(target)
images={}
for label,size,socket in [('Albedo',2048,'Base Color'),('Metallic',256,'Metallic'),('Roughness',256,'Roughness'),('Normal',1024,None)]:
 im=bpy.data.images.new('Rebaked_'+label,width=size,height=size,alpha=False);im.colorspace_settings.name='sRGB' if label=='Albedo' else 'Non-Color';node.image=im;tn.active=node
 if socket:
  sl.new(bs.inputs[socket].links[0].from_socket,emit.inputs['Color']);sl.new(emit.outputs[0],out.inputs['Surface'])
 else:sl.new(bs.outputs[0],out.inputs['Surface'])
 bpy.ops.object.select_all(action='DESELECT');source.select_set(True);low.select_set(True);bpy.context.view_layer.objects.active=low;bpy.ops.object.bake(type='EMIT' if socket else 'NORMAL')
 im.filepath_raw=str(O/('Turret_'+label+'.png'));im.file_format='PNG';im.save();images[label]=im
sl.new(bs.outputs[0],out.inputs['Surface'])
m=bpy.data.materials.new('Turret_Rebaked');m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;b=n.get('Principled BSDF')
for label,socket in [('Albedo','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
 t=n.new('ShaderNodeTexImage');t.image=images[label];l.new(t.outputs['Color'],b.inputs[socket])
t=n.new('ShaderNodeTexImage');t.image=images['Normal'];nm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],b.inputs['Normal']);low.data.materials.clear();low.data.materials.append(m)
lods=[low]
for i,count in [(1,8000),(2,3000)]:
 o=low.copy();o.data=low.data.copy();sc.collection.objects.link(o);o.name='Turret_LOD'+str(i);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;dec=o.modifiers.new('Distance reduction','DECIMATE');dec.ratio=count/21999;dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name);lods.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in lods:o.select_set(True)
bpy.context.view_layer.objects.active=low;bpy.ops.export_scene.fbx(filepath=str(O/'RustyCombatTurret_Optimized.fbx'),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
for o in lods:o.hide_render=o!=low
source.hide_render=True;source.hide_set(True);sc.cycles.samples=32
center=sum([source.matrix_world@Vector(c) for c in source.bound_box],Vector())/8
for label,eye in [('optimized-front',(-1.5,-2,1.1)),('optimized-rear',(1.5,2,1.0))]:
 sc.camera.location=center+Vector(eye);sc.camera.rotation_euler=(center-sc.camera.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(O/(label+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'RustyCombatTurret_Optimized.blend'))
report=json.loads((O/'optimization-report.json').read_text());report['uv']='Smart projection LOD0; all four maps transferred from source; LOD1/2 derived from LOD0';report['normal_bake']='1024 tangent-space; 0.006m extrusion, 0.02m ray; source normals included';report['lods']=[]
for o in lods:o.data.calc_loop_triangles();report['lods'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices)})
(O/'optimization-report.json').write_text(json.dumps(report,indent=2))
