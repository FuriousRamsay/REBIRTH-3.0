"""Texture the approved clay geometry using original vehicle atlases.
No image synthesis, source asset replacement, game launch or bundle installation.
Export corner normals/UVs for the Unity review importer as well as an editable blend.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio')
OUT=ROOT/'TruckBodyworkTextured_20261020'
PROJECT=Path('E:/_Haven/shared/REBIRTHVehicleStudio_Camper')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'TruckBodyworkRearSmooth_20261020/truck-bodywork-detailed.blend'))
source=json.loads((ROOT/'TruckBodyworkDetailed_20261020/native-truck.json').read_text())
parts={p['path'].split('/')[-1]:p for p in source['parts']}
entries=json.loads((OUT/'source-materials.json').read_text())['materials']
materials={}
# Original truck is authoritative. Undo the earlier unrequested cab/mirror edits.
for o in list(bpy.data.collections['TruckCommonStructure'].objects):
 bpy.data.objects.remove(o,do_unlink=True)
for o in bpy.data.collections['NativeTruckReference'].objects:
 if o.type!='MESH':continue
 p=parts[o.name];v=p['vertices'];t=p['triangles']
 faces=[tuple(t[i:i+3]) for i in range(0,len(t),3)]
 if o.name=='licence_plate':faces=[f for f in faces if all(v[i]['z']>0 for i in f)]
 m=bpy.data.meshes.new('Original unmodified '+o.name)
 m.from_pydata([(q['x'],-q['z'],q['y']) for q in v],[],faces);m.update()
 n=[Vector((q['x'],-q['z'],q['y'])) for q in p['normals']]
 for f in m.polygons:
  f.use_smooth=True
  if f.normal.dot(sum((n[i] for i in f.vertices),Vector()))<0:f.flip()
 layer=m.uv_layers.new(name='Original Unity UV')
 for loop in m.loops:
  q=p['uv'][loop.vertex_index];layer.data[loop.index].uv=(q['x'],q['y'])
 m.normals_split_custom_set_from_vertices(n);o.data=m
def material(key):
 if key in materials:return materials[key]
 entry=next(e for e in entries if e['part'].split('/')[-1]==key)
 m=bpy.data.materials.new('Original textured '+key);m.use_nodes=True
 m['unity_asset']=entry['asset'];m['unity_material_key']=key
 ns=m.node_tree.nodes;ls=m.node_tree.links;p=ns.get('Principled BSDF')
 textures={t['property']:t['path'] for t in entry['textures']}
 def tex(prop,linear=False):
  path=textures.get(prop)
  if not path:return None
  n=ns.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(PROJECT/path),check_existing=True)
  if linear:n.image.colorspace_settings.name='Non-Color'
  return n
 # Original white finish; Unity uses the blue/neutral difference solely as its dye mask.
 base=tex('_NeutralTex' if key=='body' else '_MainTex')
 if base:ls.new(base.outputs['Color'],p.inputs['Base Color'])
 normal=tex('_BumpMap',True)
 if normal:
  n=ns.new('ShaderNodeNormalMap');ls.new(normal.outputs['Color'],n.inputs['Color']);ls.new(n.outputs['Normal'],p.inputs['Normal'])
 finish=tex('_MetallicGlossMap',True)
 if finish:
  sep=ns.new('ShaderNodeSeparateColor');ls.new(finish.outputs['Color'],sep.inputs[0]);ls.new(sep.outputs[0],p.inputs['Metallic'])
  inv=ns.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
  ls.new(finish.outputs['Alpha'],inv.inputs[1]);ls.new(inv.outputs[0],p.inputs['Roughness'])
 else:p.inputs['Roughness'].default_value=.35
 if key in ['windows','headlights_glass']:
  p.inputs['Transmission Weight'].default_value=1;p.inputs['Roughness'].default_value=.06
  p.inputs['Base Color'].default_value=(.7,.75,.8,1)
  for link in list(p.inputs['Base Color'].links):ls.remove(link)
 materials[key]=m;return m

def assign(o,key):
 o.data.materials.clear();o.data.materials.append(material(key))
 for f in o.data.polygons:f.material_index=0
 o['unity_material_key']=key

def uvproject(o,region,axes=(0,2)):
 layer=o.data.uv_layers.active or o.data.uv_layers.new(name='Original atlas patch')
 lo=[min(v.co[a] for v in o.data.vertices) for a in axes]
 hi=[max(v.co[a] for v in o.data.vertices) for a in axes]
 u,v,w,h=region
 for loop in o.data.loops:
  co=o.data.vertices[loop.vertex_index].co
  xy=[(co[a]-lo[i])/max(hi[i]-lo[i],.00001) for i,a in enumerate(axes)]
  layer.data[loop.index].uv=(u+xy[0]*w,v+xy[1]*h)

# Source rear plate is translated as one rigid mesh; original size, UVs and lettering stay.
for o in list(bpy.data.objects):
 if o.name=='Rear registration plate' or o.name.startswith('Plate screw'):bpy.data.objects.remove(o,do_unlink=True)
p=parts['licence_plate'];ids=sorted({i for i in p['triangles'] if p['vertices'][i]['z']<0})
center={a:(min(p['vertices'][i][a] for i in ids)+max(p['vertices'][i][a] for i in ids))/2 for a in ['x','y','z']}
delta={'x':-center['x'],'y':.585-center['y'],'z':-2.817-center['z']}
index={old:new for new,old in enumerate(ids)}
verts=[tuple(p['vertices'][i][a]+delta[a] for a in ['x','y','z']) for i in ids]
faces=[tuple(index[j] for j in p['triangles'][i:i+3]) for i in range(0,len(p['triangles']),3) if all(j in index for j in p['triangles'][i:i+3])]
mesh=bpy.data.meshes.new('Original rear license plate');mesh.from_pydata([(x,-z,y) for x,y,z in verts],[],faces);mesh.update()
plate=bpy.data.objects.new('Original rear license plate',mesh);bpy.data.collections['PickupOnlyInfill'].objects.link(plate)
layer=mesh.uv_layers.new(name='Original plate UV')
normals=[Vector((p['normals'][i]['x'],-p['normals'][i]['z'],p['normals'][i]['y'])) for i in ids]
for f in mesh.polygons:
 if f.normal.dot(sum((normals[i] for i in f.vertices),Vector()))<0:f.flip()
for loop in mesh.loops:
 uv=p['uv'][ids[loop.vertex_index]];layer.data[loop.index].uv=(uv['x'],uv['y'])
mesh.normals_split_custom_set_from_vertices(normals);assign(plate,'licence_plate')

# Copy donor bumper UVs using the exact source component selection from the clay builder.
p=parts['parts'];v=p['vertices'];t=p['triangles'];parent=list(range(len(v)))
def find(i):
 while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
 return i
for i in range(0,len(t),3):
 for a in t[i+1:i+3]:parent[find(a)]=find(t[i])
groups={}
for i in t:groups.setdefault(find(i),set()).add(i)
used=sorted({i for ids in groups.values() if max(v[j]['z'] for j in ids)>2.07 and min(v[j]['y'] for j in ids)>.49 and max(v[j]['y'] for j in ids)<.71 for i in ids})
bumper=bpy.data.objects['Rear bumper - straight span rounded returns'];assert len(used)==len(bumper.data.vertices)==761
layer=bumper.data.uv_layers.new(name='Original front bumper UV')
for loop in bumper.data.loops:
 uv=p['uv'][used[loop.vertex_index]];layer.data[loop.index].uv=(uv['x'],uv['y'])
assign(bumper,'parts')

for o in bpy.data.collections['NativeTruckReference'].objects:
 if o.type=='MESH':assign(o,o.name)
for collection in ['PickupOnlyInfill','TruckCommonStructure']:
 for o in bpy.data.collections[collection].objects:
  if o.type!='MESH' or o.get('unity_material_key'):continue
  old=o.data.materials[0].name
  if 'formed body' in old:
   key='body';patch=(.045,.39,.245,.13)
  elif o.name.startswith(('Rear lamp lens','Fine lens')):
   key='lights';patch=(.646,.32,.15,.09) if sum(v.co.z for v in o.data.vertices)/len(o.data.vertices)>.84 else (.65,.07,.15,.12)
  elif o.name=='Single clear rear cab pane':key='windows';patch=(.1,.1,.5,.5)
  elif o.name.startswith('Door mirror glass'):key='mirrors';patch=(.15,.15,.25,.25)
  elif 'rubber' in old:key='parts';patch=(.06,.81,.20,.04)
  else:key='parts';patch=(.06,.46,.10,.025)
  axes=(1,2) if o.name.startswith('Continuous inner bedside') else (0,1) if 'load floor' in o.name or o.name=='Tailgate threshold' else (0,2)
  uvproject(o,patch,axes);assign(o,key)

# Export per corner, preserving normals and UV seams through evaluated modifiers.
data={'parts':[]};deps=bpy.context.evaluated_depsgraph_get()
for collection in ['NativeTruckReference','TruckCommonStructure','PickupOnlyInfill']:
 for o in bpy.data.collections[collection].objects:
  if o.type!='MESH':continue
  evaluated=o.evaluated_get(deps);m=evaluated.to_mesh();m.calc_loop_triangles()
  layer=m.uv_layers.active;assert layer is not None,o.name
  vs=[];ns=[];uv=[];tri=[]
  for face in m.loop_triangles:
   # U is a proper rotation, so retain winding (verified against source triangles).
   for li in face.loops:
    co=m.vertices[m.loops[li].vertex_index].co;n=m.corner_normals[li].vector
    vs.append({'x':co.x,'y':co.z,'z':-co.y});ns.append({'x':n.x,'y':n.z,'z':-n.y})
    u=layer.data[li].uv;uv.append({'x':u.x,'y':u.y});tri.append(len(tri))
  data['parts'].append({'name':o.name,'group':collection,'material':o['unity_material_key'],'vertices':vs,'normals':ns,'uv':uv,'triangles':tri})
  evaluated.to_mesh_clear()
(OUT/'textured-truck-meshes.json').write_text(json.dumps(data,separators=(',',':')))
(OUT/'texture-authoring-report.json').write_text(json.dumps({'approved_clay':'TruckBodyworkRearSmooth_20261020','source_plate_vertices':len(ids),'plate_translation':delta,'plate_original_dimensions_m':[.3000368,.1628159,.0035791],'rear_bumper_original_uv_vertices':len(used),'tailgate_material':'body','unity_body_shader':'REBIRTH/CamperTallReferenceWrap','paint_atlas_patch':[.045,.39,.245,.13],'texture_images_modified':False,'renderers':len(data['parts'])},indent=2))
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'truck-bodywork-textured.blend'))
print('TRUCK_TEXTURE_AUTHORING_COMPLETE',len(data['parts']))
