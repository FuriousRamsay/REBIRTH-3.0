"""Localized corrections to the approved pickup, retaining its immutable baseline.
Blender plate display mapping is separate from the original Unity-export UVs.
"""
import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio')
BASE=ROOT/'TruckBodyworkApprovedTextured_20261020/approved-truck.blend'
OUT=ROOT/'TruckBodyworkRearDetails_20261020'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(BASE))
def signature(o):
 m=o.data
 return hashlib.sha256(repr(([tuple(v.co) for v in m.vertices],[tuple(p.vertices) for p in m.polygons],[[tuple(t.uv) for t in l.data] for l in m.uv_layers])).encode()).hexdigest()
native={o.name:signature(o) for o in bpy.data.collections['NativeTruckReference'].objects if o.type=='MESH'}
deps=bpy.context.evaluated_depsgraph_get()
body=BVHTree.FromObject(bpy.data.objects['body'],deps)
changed=[]
for o in list(bpy.data.collections['PickupOnlyInfill'].objects):
 if o.name.startswith(('Rear lamp bezel','Rear lamp lens segment','Fine lens flute')):
  # The measured original mounting lip reaches 2.77425 m. Place the
  # housing's outer face at 2.780 m and retain every lens's relative depth.
  for v in o.data.vertices:
   side=1 if v.co.x>0 else -1
   v.co.x=side*(.807+(abs(v.co.x)-.817)*.86)
   v.co.y+=.029
  o.data.update();changed.append(o.name)
 if o.name.startswith('Tailgate side latch plate'):
  # Recess the latch hardware behind the curved skin, including its upper end.
  for v in o.data.vertices:v.co.y-=.045
  o.data.update();changed.append(o.name)

# Extend each black housing back to the curved factory corner. This closes
# the gap behind the newly cleared lamps without cutting the original body.
for bezel in [o for o in bpy.data.objects if o.name.startswith('Rear lamp bezel')]:
 outline=[v.co.copy() for v in list(bezel.data.vertices)[:28]]
 assert len(outline)==28
 back=[]
 for p in outline:
  hit,_,_,_=body.ray_cast(Vector((p.x,3.2,p.z)),Vector((0,-1,0)),.7)
  depth=hit.y if hit else 2.70
  back.append(Vector((p.x,min(p.y-.002,depth-.001),p.z)))
 n=len(outline);m=bpy.data.meshes.new('Contoured rear lamp mounting skirt')
 m.from_pydata(outline+back,[],[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])
 m.update();o=bpy.data.objects.new('Contoured rear lamp mounting skirt',m)
 bpy.data.collections['PickupOnlyInfill'].objects.link(o)
 m.materials.append(bezel.data.materials[0]);o['unity_material_key']='parts'
 uv=m.uv_layers.new(name='Original black trim atlas')
 for p in m.polygons:
  p.use_smooth=True
  for i,li in enumerate(p.loop_indices):uv.data[li].uv=((.06,.81),(.26,.81),(.26,.85),(.06,.85))[i]
 changed.append(o.name)

plate=bpy.data.objects['Original rear license plate']
uv_before=[tuple(t.uv) for t in plate.data.uv_layers.active.data]
mat=plate.data.materials[0].copy();mat.name='Original rear plate - Blender handedness correction'
plate.data.materials[0]=mat
nodes=mat.node_tree.nodes;links=mat.node_tree.links
coords=nodes.new('ShaderNodeTexCoord')
scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(-1,1,1)
offset=nodes.new('ShaderNodeVectorMath');offset.operation='ADD'
offset.inputs[1].default_value=(min(u[0] for u in uv_before)+max(u[0] for u in uv_before),0,0)
links.new(coords.outputs['UV'],scale.inputs[0]);links.new(scale.outputs['Vector'],offset.inputs[0])
for node in list(nodes):
 if node.type=='TEX_IMAGE':links.new(offset.outputs['Vector'],node.inputs['Vector'])
mat['display_only']='Blender handedness correction; Unity uses original plate UVs and material.'
changed.append(plate.name+' (Blender material mapping only)')

# Export corrected addition meshes for the existing staged Unity importer.
# This deliberately exports original plate UVs, never the Blender-only mapping.
data={'parts':[]};deps=bpy.context.evaluated_depsgraph_get()
for collection in ['NativeTruckReference','TruckCommonStructure','PickupOnlyInfill']:
 for o in bpy.data.collections[collection].objects:
  if o.type!='MESH':continue
  evaluated=o.evaluated_get(deps);m=evaluated.to_mesh();m.calc_loop_triangles()
  vs=[];ns=[];uv=[];tri=[];layer=m.uv_layers.active
  for f in m.loop_triangles:
   for li in f.loops:
    co=m.vertices[m.loops[li].vertex_index].co;n=m.corner_normals[li].vector
    vs.append({'x':co.x,'y':co.z,'z':-co.y});ns.append({'x':n.x,'y':n.z,'z':-n.y})
    u=layer.data[li].uv;uv.append({'x':u.x,'y':u.y});tri.append(len(tri))
  data['parts'].append({'name':o.name,'group':collection,'material':o['unity_material_key'],'vertices':vs,'normals':ns,'uv':uv,'triangles':tri})
  evaluated.to_mesh_clear()
(OUT/'textured-truck-meshes.json').write_text(json.dumps(data,separators=(',',':')))
report={'changed':changed,'native_meshes_unchanged':all(signature(bpy.data.objects[n])==h for n,h in native.items()),
 'native_mesh_count':len(native),'plate_export_uv_unchanged':uv_before==[tuple(t.uv) for t in plate.data.uv_layers.active.data],
 'lamp_rearward_shift_m':.029,'lamp_width_scale':.86,'lamp_center_inward_m':.010,'latch_inward_shift_m':.045,'status':'REVISED MODEL REVIEW; NOT INSTALLED'}
assert report['native_meshes_unchanged'] and report['plate_export_uv_unchanged']
(OUT/'rear-details-audit.json').write_text(json.dumps(report,indent=2))
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.resolution_x=1800;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
for o in bpy.data.collections['Studio'].objects:o.hide_set(False)
def U(p):return Vector((p[0],-p[2],p[1]))
def render(name,direction):
 at=U((0,.64,-2.68));forward=-U(direction).normalized();up=Vector((0,0,1));right=forward.cross(up).normalized();up=right.cross(forward)
 cam=sc.camera;cam.location=at+U(direction);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=2.35
 for obj,loc in zip([bpy.data.objects[n] for n in ['Key','Fill','Rim']],[at-forward*4-right*3+up*4,at-forward*2+right*4+up,at+forward*3-right+up*3]):
  obj.location=loc;obj.rotation_euler=(at-loc).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('rear-corrected-closeup',(2,.85,-6))
render('rear-corrected-straight',(0,.35,-6))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'truck-rear-details.blend'))
print('REAR_DETAILS_COMPLETE',json.dumps(report))
