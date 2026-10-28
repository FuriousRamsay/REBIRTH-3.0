"""Bake/export isolation candidate; output is not a fidelity or installation approval."""
import bpy,json,sys,math
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);out=Path(args[1])
out.mkdir(parents=True,exist_ok=True)
if (out/'export.blend').exists():raise RuntimeError('Preserve prior export candidate')
bpy.ops.wm.open_mainfile(filepath=str(folder/'forge.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
meshes=[o for o in scene.objects if o.type=='MESH']
materials=set(m for o in meshes for m in o.data.materials if m)
# Keep the source UV and bind source texture reads to it before creating the output atlas UV.
for obj in meshes:
 if not obj.data.uv_layers:obj.data.uv_layers.new(name='SourceUV')
 else:obj.data.uv_layers.active.name='SourceUV'
 obj.data.uv_layers.new(name='BakeUV');obj.data.uv_layers.active=obj.data.uv_layers['BakeUV']
for material in materials:
 nodes=material.node_tree.nodes;links=material.node_tree.links
 uv=nodes.new('ShaderNodeUVMap');uv.uv_map='SourceUV'
 for node in list(nodes):
  if node.type!='TEX_COORD':continue
  for link in list(node.outputs['UV'].links):links.new(uv.outputs['UV'],link.to_socket)
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.003)
bpy.ops.object.mode_set(mode='OBJECT')
print('ATLAS_UV_READY',flush=True)
targets={}
for label in ['albedo','normal','roughness','metallic','emission']:
 image=bpy.data.images.new('Forge_'+label,width=2048,height=2048,alpha=False)
 image.colorspace_settings.name='sRGB' if label in ['albedo','emission'] else 'Non-Color'
 active_nodes=[];restore=[]
 for material in materials:
  nodes=material.node_tree.nodes;links=material.node_tree.links
  target=nodes.new('ShaderNodeTexImage');target.image=image;nodes.active=target;active_nodes.append((material,target))
  if label=='normal':continue
  p=nodes.get('Principled BSDF');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
  old=[(l.from_socket,l.to_socket) for l in output.inputs['Surface'].links]
  shader=nodes.new('ShaderNodeEmission')
  socket=p.inputs[{'albedo':'Base Color','roughness':'Roughness','metallic':'Metallic','emission':'Emission Color'}[label]]
  if socket.links:links.new(socket.links[0].from_socket,shader.inputs['Color'])
  else:
   value=socket.default_value
   shader.inputs['Color'].default_value=tuple(value) if hasattr(value,'__len__') else (value,value,value,1)
  if label=='emission':
   strength=p.inputs['Emission Strength']
   if strength.links:links.new(strength.links[0].from_socket,shader.inputs['Strength'])
   else:shader.inputs['Strength'].default_value=strength.default_value
  links.new(shader.outputs[0],output.inputs['Surface']);restore.append((material,shader,old))
 scene.render.bake.use_clear=True;scene.render.bake.margin=12
 bpy.ops.object.bake(type='NORMAL' if label=='normal' else 'EMIT',normal_space='TANGENT')
 image.filepath_raw=str(out/(label+'.png'));image.file_format='PNG';image.save();targets[label]=image
 for material,shader,old in restore:
  for a,b in old:material.node_tree.links.new(a,b)
  material.node_tree.nodes.remove(shader)
 for material,target in active_nodes:material.node_tree.nodes.remove(target)
 print('BAKED_'+label.upper(),flush=True)
# Preserve a bake-ready high-resolution scene; optimization is a separate reviewed artifact.
bpy.ops.wm.save_as_mainfile(filepath=str(out/'baked-high.blend'))
before=0;after=0
for obj in meshes:
 bpy.context.view_layer.objects.active=obj
 for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 obj.data.calc_loop_triangles();before+=len(obj.data.loop_triangles)
 if len(obj.data.polygons)>1000:
  dec=obj.modifiers.new('Review candidate reduction','DECIMATE');dec.ratio=.08
  bpy.ops.object.modifier_apply(modifier=dec.name)
 obj.data.calc_loop_triangles();after+=len(obj.data.loop_triangles)
 obj.data.uv_layers.remove(obj.data.uv_layers['SourceUV'])
 obj.data.uv_layers.active=obj.data.uv_layers['BakeUV']
material=bpy.data.materials.new('Forge baked review material');material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links;p=nodes.get('Principled BSDF')
for label,socket in [('albedo','Base Color'),('roughness','Roughness'),('metallic','Metallic'),('emission','Emission Color')]:
 tex=nodes.new('ShaderNodeTexImage');tex.image=targets[label];links.new(tex.outputs['Color'],p.inputs[socket])
normal=nodes.new('ShaderNodeTexImage');normal.image=targets['normal'];decode=nodes.new('ShaderNodeNormalMap')
links.new(normal.outputs['Color'],decode.inputs['Color']);links.new(decode.outputs[0],p.inputs['Normal']);p.inputs['Emission Strength'].default_value=1
for obj in meshes:obj.data.materials.clear();obj.data.materials.append(material)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'export.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'forge-review.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
(out/'export-receipt.json').write_text(json.dumps({'status':'UNVERIFIED_EXPORT_CANDIDATE','source':str(folder),'trianglesBefore':before,'trianglesAfter':after,'maps':list(targets),'normalConvention':'Blender tangent normal, verify Unity import without blind green flip','resolution':2048,'requires':'Bake inspection, reduced mesh comparison and imported Unity material proof'},indent=2))
print('EXPORT_COMPLETE_NOT_ACCEPTANCE',flush=True)
