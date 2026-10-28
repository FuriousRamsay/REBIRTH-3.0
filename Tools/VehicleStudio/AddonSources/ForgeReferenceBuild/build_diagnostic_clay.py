"""Actual Blender inspection scene. No textures, no fidelity acceptance implied."""
import bpy,bmesh,json,sys,math
from pathlib import Path
from mathutils import Vector,Matrix
convex='--convex-harness' in sys.argv
rounded='--round-stones' in sys.argv
hearth_detail='--hearth-detail' in sys.argv
floor_coals='--floor-coals' in sys.argv
rear_stones='--rear-stones' in sys.argv
bulk='--bulk-stones' in sys.argv
contact='--fit-harness' in sys.argv or convex
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild')/('HarnessEnvelopeInspection' if convex else ('HarnessContactInspection' if contact else 'ClayInspection'))
if rounded:out=out.parent/'RoundedStoneInspection'
if hearth_detail:out=out.parent/'HearthDetailInspection'
if floor_coals:out=out.parent/'FloorCoalInspection'
if rear_stones:out=out.parent/'RearMasonryInspection'
if bulk:out=out.parent/'BulkStoneInspection'
out.mkdir(parents=True,exist_ok=True)
data=json.loads((out.parent/'PlanReview/clay-input.json').read_text())
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('Neutral untextured clay');mat.diffuse_color=(.48,.48,.48,1)
mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.48,.48,.48,1);bs.inputs['Roughness'].default_value=.8
straps=[];target_vertices=[];target_faces=[]
strap_names={'harness_left_front_strap','harness_right_front_strap','harness_crown_diagonal_left','harness_crown_cross_band'}
for i,part in enumerate(data['meshes']):
 name=part.get('label') or f'planned_mesh_{i:03d}'
 vertices,faces=part['vertices'],part['faces']
 if name=='furnace_core' or name.startswith('stone_'):
  start=len(target_vertices);target_vertices.extend(vertices);target_faces.extend([[j+start for j in f] for f in faces])
 if contact and name in strap_names:
  # Rebuild the solid ribbon as a surface before wrapping; keep thickness separate.
  count=len(vertices)//8
  vertices=[vertices[k*8+j] for k in range(count) for j in (0,5)]
  faces=[[2*k,2*k+1,2*k+3,2*k+2] for k in range(count-1)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
 if rounded and name.startswith('stone_'):
  bevel=obj.modifiers.new('Worn stone edge geometry','BEVEL');bevel.width=.009;bevel.segments=4;bevel.limit_method='ANGLE';bevel.angle_limit=.20;bevel.use_clamp_overlap=True
 if contact and name in strap_names:straps.append(obj)
 # Leave topology and faceting visible: no subdivision or displacement to conceal it.
if contact:
 mesh=bpy.data.meshes.new('Contact target');mesh.from_pydata(target_vertices,[],target_faces);mesh.update()
 if convex:
  bm=bmesh.new()
  for v in target_vertices:bm.verts.new(v)
  hull=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
  bmesh.ops.delete(bm,geom=hull['geom_interior'],context='VERTS')
  bm.to_mesh(mesh);bm.free();mesh.update()
 target_obj=bpy.data.objects.new('Hidden furnace contact surface',mesh);bpy.context.collection.objects.link(target_obj);target_obj.hide_render=True
 for obj in straps:
  wrap=obj.modifiers.new('Fit to stone surface','SHRINKWRAP');wrap.target=target_obj;wrap.wrap_method='NEAREST_SURFACEPOINT';wrap.offset=.003
  solid=obj.modifiers.new('Iron thickness','SOLIDIFY');solid.thickness=.009;solid.offset=1
  bevel=obj.modifiers.new('Small iron edge radius','BEVEL');bevel.width=.002;bevel.segments=3
 if convex:
  from mathutils.bvhtree import BVHTree
  bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
  trees=[BVHTree.FromObject(o,deps) for o in straps]
  for obj in list(bpy.context.scene.objects):
   if not obj.name.startswith('harness_') or obj in straps:continue
   center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
   candidates=[tree.find_nearest(center) for tree in trees]
   candidates=[v for v in candidates if v[0] is not None]
   if not candidates:continue
   point,normal,_,distance=min(candidates,key=lambda v:v[3])
   obj.location+=point+normal*.018-center
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1448;scene.render.resolution_y=1086;scene.render.resolution_percentage=100
scene.world.color=(.15,.15,.15)
R,U,D=[Vector(data[k]) for k in ('R','U','D')]
if rear_stones:
 spec=json.loads((Path(__file__).parent/'rear-masonry-plan.json').read_text())
 env=json.loads((Path(__file__).parent/'upright-furnace-envelope.json').read_text())
 px,py,rx,ry,wall,cap,power=env['parameters'];e=math.radians(22.88949901583545)
 H=Vector((math.sin(math.radians(26.063965073925417)),math.cos(math.radians(26.063965073925417)),0))
 center=R*((px-891)/760)+H*((935-py)/(760*math.sin(e)))
 for row,(z0,z1,count) in enumerate(spec['courses']):
  for col in range(count):
   angle=math.tau*(col+.5*(row%2))/count
   normal=Vector((math.cos(angle)/rx,math.sin(angle)/ry,0)).normalized()
   if normal.dot(D)>-.05:continue
   half=math.pi/count-spec['angularJointRadians'];verts=[]
   for inset in (-spec['radialDepth'],spec['exteriorRelief']):
    for z in (z0,z1):
     scale=1 if z<=wall else max(0,1-((z-wall)/cap)**2)**(power/2)
     for k in range(5):
      a=angle-half+2*half*k/4
      verts.append(center+Vector(((rx*scale+inset)*math.cos(a),(ry*scale+inset)*math.sin(a),z)))
   faces=[]
   for k in range(4):
    faces.extend([[k,k+1,6+k,5+k],[10+k,15+k,16+k,11+k],[k,10+k,11+k,k+1],[5+k,6+k,16+k,15+k]])
   faces.extend([[0,5,15,10],[4,14,19,9]])
   mesh=bpy.data.meshes.new(f'inferred_rear_stone_{row}_{col}');mesh.from_pydata(verts,[],faces);mesh.update()
   bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
   obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
   bevel=obj.modifiers.new('Stone edge rounding','BEVEL');bevel.width=.008;bevel.segments=3
if hearth_detail:
 from mathutils.bvhtree import BVHTree
 spec=json.loads((Path(__file__).parent/'hearth-detail-plan.json').read_text())
 core=bpy.data.objects['furnace_core'];bpy.context.view_layer.update()
 tree=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get());checks=[]
 for kind in ('stones','coals'):
  if floor_coals and kind=='coals':continue
  relief=spec['stoneRelief' if kind=='stones' else 'coalRelief']
  for i,outline in enumerate(spec[kind]):
   cx=sum(p[0] for p in outline)/len(outline);cy=sum(p[1] for p in outline)/len(outline)
   ray=R*((cx-891)/760)+U*((935-cy)/760)
   pos,normal,face,distance=tree.ray_cast(ray+D*5,-D)
   if pos is None or abs(normal.dot(D))<.08:
    checks.append({'kind':kind,'index':i,'status':'UNSUPPORTED'});continue
   if normal.dot(D)<0:normal=-normal
   verts=[];n=len(outline)
   for scale,z in ((.90,-.015),(1,0),(.78,relief)):
    for x,y in outline:
     p=R*((cx+(x-cx)*scale-891)/760)+U*((935-cy-(y-cy)*scale)/760)
     p+=D*(normal.dot(pos-p)/normal.dot(D))+normal*z;verts.append(p)
   verts.append(pos+normal*(relief*1.15));faces=[]
   for row in range(2):
    for j in range(n):faces.append([row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j])
   for j in range(n):faces.append([2*n+j,2*n+(j+1)%n,3*n])
   faces.append(list(range(n-1,-1,-1)))
   mesh=bpy.data.meshes.new(f'hearth_{kind}_{i:02d}');mesh.from_pydata(verts,[],faces);mesh.update()
   obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
   bevel=obj.modifiers.new('Rounded fractured edges','BEVEL');bevel.width=.003;bevel.segments=3
   checks.append({'kind':kind,'index':i,'status':'PROJECTED_NOT_APPROVED','position':list(pos),'normal':list(normal)})
 if floor_coals:
  chamber=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text())
  axis=Vector(chamber['outwardAxis']);side=Vector((0,0,1)).cross(axis)
  profile=chamber['openingProfileSideZ'][:4]
  # Deliberate floor layout, normalized chamber coordinates, no camera-ray depth.
  placements=[(.11,-.20,.030),(.20,-.21,.033),(.30,-.20,.028),(.40,-.21,.030),(.15,-.13,.032),(.25,-.12,.030),(.36,-.13,.034),(.19,-.055,.029),(.30,-.05,.033),(.42,-.055,.027)]
  for i,(s,d,size) in enumerate(placements):
   segment=next(((a,b) for a,b in zip(profile,profile[1:]) if a[0]<=s<=b[0]),None)
   if segment is None:raise ValueError('Coal outside floor profile')
   a,b=segment;t=(s-a[0])/(b[0]-a[0]);z=a[1]+t*(b[1]-a[1])
   bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1)
   obj=bpy.context.object;obj.name=f'floor_coal_{i:02d}';obj.location=side*s+axis*d+Vector((0,0,z))
   # Flatten lower footprint onto the sloping floor; keep a broken upper profile.
   slope=(b[1]-a[1])/(b[0]-a[0])
   for v in obj.data.vertices:
    local=v.co.copy();ds=local.x*size;dd=local.y*size*.72
    height=(local.z+1)*size*.58
    v.co=side*ds+axis*dd+Vector((0,0,max(0,height-.004)+slope*ds))
   obj.data.materials.append(mat)
   checks.append({'kind':'floor_coal','index':i,'position':list(obj.location),'side':s,'depth':d,'floorHeight':z,'status':'FLOOR_CONSTRAINED_REQUIRES_VISUAL_REVIEW'})
 (out/'hearth-placement-audit.json').write_text(json.dumps(checks,indent=2))
target=R*((724-891)/760)+U*((935-543)/760)
bpy.ops.object.camera_add(location=target+D*5);cam=bpy.context.object
cam.rotation_euler=Matrix((R,U,D)).transposed().to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1448/760;scene.camera=cam
def light(name,pos,energy,size):
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
light('Large key',target+D*3+U*3-R*2,450,3)
light('Soft fill',target+D*2+R*3,150,3)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025));bpy.context.object.name='Inspection ground';bpy.context.object.data.materials.append(mat)
scene.view_settings.view_transform='AgX'
scene['fidelity_status']='FAILED - diagnostic geometry inspection only'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge-diagnostic-clay.blend'))
scene.render.filepath=str(out/'reference-view.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'RENDERED_NOT_FIDELITY_APPROVED','meshCount':len(data['meshes']),'textures':False,'resolution':[1448,1086],'scene':'forge-diagnostic-clay.blend'},indent=2))
