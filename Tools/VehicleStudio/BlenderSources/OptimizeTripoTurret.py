"""Non-destructive Tripo reduction, source-to-LOD0 normal bake, FBX and matched review renders."""
import bpy,math,json,time
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TripoTurret_20261018')
OUT=P/'Output';OUT.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(next((P/'Input').glob('*.fbx'))))
source=next(o for o in bpy.context.scene.objects if o.type=='MESH');source.name='Turret_Source_287191'
bpy.context.view_layer.objects.active=source;source.select_set(True)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
source.data.calc_loop_triangles();source_count=len(source.data.loop_triangles)
texdir=next((P/'Input').glob('*.fbm'))
def image(token,color='Non-Color'):
 path=next(p for p in texdir.iterdir() if token.lower() in p.name.lower())
 im=bpy.data.images.load(str(path),check_existing=True);im.colorspace_settings.name=color;return im
base=image('basecolor','sRGB');normal=image('normal');rough=image('roughness');metal=image('metallic')
def material(name,albedo,norm,roughness,metallic):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;bs=n.get('Principled BSDF')
 for im,socket in [(albedo,'Base Color'),(roughness,'Roughness'),(metallic,'Metallic')]:
  t=n.new('ShaderNodeTexImage');t.image=im;l.new(t.outputs['Color'],bs.inputs[socket])
 t=n.new('ShaderNodeTexImage');t.image=norm;nm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],bs.inputs['Normal']);return m
source.data.materials.clear();source.data.materials.append(material('Source PBR',base,normal,rough,metal))
def resized(im,name,size):
 cp=im.copy();cp.name=name;cp.scale(size,size);cp.filepath_raw=str(OUT/(name+'.png'));cp.file_format='PNG';cp.save();return cp
albedo=resized(base,'Turret_Albedo',2048);roughsmall=resized(rough,'Turret_Roughness',256);metalsmall=resized(metal,'Turret_Metallic',256);resized(image('_rm.'),'Turret_SourceRM',256)
report={'source_triangles':source_count,'source_dimensions_m':list(source.dimensions),'lods':[],'normal_bake':'pending'}
lods=[]
for i,target in enumerate((22000,8000,3000)):
 obj=source.copy();obj.data=source.data.copy();bpy.context.collection.objects.link(obj);obj.name='Turret_LOD'+str(i)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 dec=obj.modifiers.new('Preserve silhouette reduction','DECIMATE');dec.decimate_type='COLLAPSE';dec.ratio=target/source_count;dec.use_collapse_triangulate=True
 bpy.ops.object.modifier_apply(modifier=dec.name)
 obj.data.calc_loop_triangles();lods.append(obj)
 for face in obj.data.polygons:face.use_smooth=True
 report['lods'].append({'name':obj.name,'target':target,'triangles':len(obj.data.loop_triangles),'vertices':len(obj.data.vertices),'uv_layers':len(obj.data.uv_layers)})
 obj.hide_render=True;obj.hide_set(True)
 # Measure sampled original vertices against reduced surface, in the same local coordinates.
 tree=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[tuple(t.vertices) for t in obj.data.loop_triangles],all_triangles=True)
 errors=[tree.find_nearest(v.co)[3] for v in list(source.data.vertices)[::17]]
 errors.sort();report['lods'][-1].update({'sample_count':len(errors),'distance_mean_m':sum(errors)/len(errors),'distance_p95_m':errors[int(len(errors)*.95)],'distance_max_m':max(errors)})
(OUT/'optimization-report.json').write_text(json.dumps(report,indent=2))
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=16;sc.cycles.device='CPU';sc.render.threads_mode='FIXED';sc.render.threads=8
low=lods[0];low.hide_set(False);low.hide_render=False;source.hide_render=False
# Bake high-resolution shape and supplied micro normals into the reduced mesh's preserved UVs.
im=bpy.data.images.new('Turret_Normal',width=1024,height=1024,alpha=False);im.colorspace_settings.name='Non-Color'
lm=bpy.data.materials.new('Reduced bake target');lm.use_nodes=True;node=lm.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;lm.node_tree.nodes.active=node;low.data.materials.clear();low.data.materials.append(lm)
bpy.ops.object.select_all(action='DESELECT');source.select_set(True);low.select_set(True);bpy.context.view_layer.objects.active=low
sc.render.bake.use_selected_to_active=True;sc.render.bake.cage_extrusion=.012;sc.render.bake.max_ray_distance=.04;sc.render.bake.margin=8;sc.render.bake.normal_space='TANGENT'
bpy.ops.object.bake(type='NORMAL');im.filepath_raw=str(OUT/'Turret_Normal.png');im.file_format='PNG';im.save();report['normal_bake']='High source with original normal map -> LOD0, tangent space, 1024, cage 0.012m, max ray 0.04m'
mat=material('Turret_Optimized',albedo,im,roughsmall,metalsmall)
for obj in lods:obj.data.materials.clear();obj.data.materials.append(mat)
source.hide_render=True;source.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
for obj in lods:obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=low
bpy.ops.export_scene.fbx(filepath=str(OUT/'RustyCombatTurret_Optimized.fbx'),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
(OUT/'optimization-report.json').write_text(json.dumps(report,indent=2))
# Identical camera/light for original and optimized assets; no image generation.
sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.render.resolution_x=1400;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.13,.15,.18,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.5
points=[source.matrix_world@Vector(c) for c in source.bound_box];center=sum(points,Vector())/8
def area(name,pos,power,size):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=center+Vector(pos);o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-2,-3,4),350,3);area('Fill',(2,1,2),180,2);area('Rim',(-1,3,2),250,2)
cam=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.30
def render(obj,label,eye):
 for o in [source]+lods:o.hide_render=o!=obj
 cam.location=center+Vector(eye);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
render(source,'original-front',(-1.5,-2,1.1));render(low,'optimized-front',(-1.5,-2,1.1));render(low,'optimized-rear',(1.5,2,1.0))
for obj in lods:obj.hide_render=obj!=low
source.hide_render=True;source.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RustyCombatTurret_Optimized.blend'))
report['status']='exported and review rendered';(OUT/'optimization-report.json').write_text(json.dumps(report,indent=2))

