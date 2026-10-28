"""Export approved native geometry and bake its reviewed materials; no redesign."""
import bpy,struct,json,sys,shutil
from pathlib import Path
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017'); O=P/('Approved40Export_20261018' if '--40' in sys.argv else 'ApprovedThickExport_20261018' if '--thick' in sys.argv else 'ApprovedExport_20261018');O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/('Thickness40Review_20261018/protective-thickness-review.blend' if '--40' in sys.argv else 'ThicknessReview_20261018/protective-thickness-review.blend' if '--thick' in sys.argv else 'ArmorAssemblyReview/armor-assembly-review.blend')))
with bpy.data.libraries.load(str(P/'ReferenceFinish/reference-finish-study.blend'),link=False) as (src,dst):
 dst.materials=[n for n in src.materials if n in ['Reference ochre remnants on dark plough steel','Reference worn spike and cutting steel','Reference oxidized fasteners']]
mats={m.name.split('.00')[0]:m for m in dst.materials if m}
for name in ['Reference plough clay','Camper mounted plough receivers']:
 col=bpy.data.collections[name];col.hide_render=False
 for obj in col.objects:
  if obj.type not in ['MESH','CURVE']:continue
  key='Reference oxidized fasteners' if any(k in obj.name for k in ['fastener','washer','bolt','pin']) else 'Reference worn spike and cutting steel' if any(k in obj.name for k in ['spike','tooth','shank']) else 'Reference ochre remnants on dark plough steel'
  obj.data.materials.clear();obj.data.materials.append(mats[key])
guards=list(bpy.data.collections['Reference connected window guards'].objects)
groups={'plates':list(bpy.data.collections['Reference traced plating samples'].objects)+list(bpy.data.collections['Armor opposite side review'].objects),'grilles':[o for o in guards if 'RearDoorGuard' not in o.name],'door':[o for o in guards if 'RearDoorGuard' in o.name],'plough':list(bpy.data.collections['Reference plough clay'].objects)+list(bpy.data.collections['Camper mounted plough receivers'].objects)}
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.render.bake.margin=4
for obj in scene.objects:obj.hide_render=True;obj.hide_set(False)
if '--thick' in sys.argv or '--40' in sys.argv:
 for label in ['grilles','door']:
  for path in (P/'ApprovedExport_20261018').glob(label+'*'):shutil.copy2(path,O/path.name)
 groups={k:v for k,v in groups.items() if k in (['plates'] if '--40' in sys.argv else ['plates','plough'])}
 if '--40' in sys.argv:
  for path in (P/'ApprovedThickExport_20261018').glob('plough*'):shutil.copy2(path,O/path.name)
  shutil.copy2(P/'ApprovedThickExport_20261018/collision-shapes.json',O/'collision-shapes.json')
report=[]
for label,objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT');copies=[];dg=bpy.context.evaluated_depsgraph_get()
 for obj in objects:
  if obj.type not in ['MESH','CURVE']:continue
  mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(dg),depsgraph=dg);clone=bpy.data.objects.new('Export '+obj.name,mesh);scene.collection.objects.link(clone);clone.matrix_world=obj.matrix_world.copy();clone.select_set(True);copies.append(clone)
 assert copies,label
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();obj=copies[0];obj.name=label;obj.hide_render=False
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.002);bpy.ops.object.mode_set(mode='OBJECT')
 materials=set(m for m in obj.data.materials if m)
 print('UV_READY',label,len(obj.data.vertices),len(obj.data.polygons),flush=True)
 for channel,size in [('albedo',2048),('normal',2048),('metallic',256),('roughness',256),('ao',256)]:
  img=bpy.data.images.new(label+'_'+channel,width=size,height=size,alpha=False);img.colorspace_settings.name='sRGB' if channel=='albedo' else 'Non-Color';restore=[];targets=[]
  for mat in materials:
   nodes=mat.node_tree.nodes;links=mat.node_tree.links;tex=nodes.new('ShaderNodeTexImage');tex.image=img;nodes.active=tex;targets.append((mat,tex))
   if channel in ['normal','ao']:continue
   p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');old=[(l.from_socket,l.to_socket) for l in out.inputs['Surface'].links];em=nodes.new('ShaderNodeEmission');sock=p.inputs[{'albedo':'Base Color','metallic':'Metallic','roughness':'Roughness'}[channel]]
   if sock.links:links.new(sock.links[0].from_socket,em.inputs['Color'])
   else:
    v=sock.default_value;em.inputs['Color'].default_value=tuple(v) if hasattr(v,'__len__') else (v,v,v,1)
   links.new(em.outputs[0],out.inputs['Surface']);restore.append((mat,em,old))
  bpy.ops.object.bake(type='NORMAL' if channel=='normal' else 'AO' if channel=='ao' else 'EMIT',normal_space='TANGENT')
  img.filepath_raw=str(O/(label+'_'+channel+'.png'));img.file_format='PNG';img.save()
  for mat,em,old in restore:
   for a,b in old:mat.node_tree.links.new(a,b)
   mat.node_tree.nodes.remove(em)
  for mat,tex in targets:mat.node_tree.nodes.remove(tex)
  print('BAKED',label,channel,flush=True)
 mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active.data
 # Explicit corner normals/UV in the native XYZ frame; preserve evaluated winding.
 with (O/(label+'.meshbin')).open('wb') as f:
  f.write(struct.pack('<ii',len(mesh.loops),len(mesh.loop_triangles)*3))
  for loop in mesh.loops:
   p=mesh.vertices[loop.vertex_index].co;n=mesh.corner_normals[loop.index].vector;t=uv[loop.index].uv;f.write(struct.pack('<8f',*p,*n,*t))
  for tri in mesh.loop_triangles:f.write(struct.pack('<3i',tri.loops[0],tri.loops[1],tri.loops[2]))
 report.append({'group':label,'vertices':len(mesh.loops),'triangles':len(mesh.loop_triangles)})
 obj.hide_render=True;obj.select_set(False)
(O/'export-report.json').write_text(json.dumps(report,indent=2));print('EXPORT_COMPLETE',flush=True)



