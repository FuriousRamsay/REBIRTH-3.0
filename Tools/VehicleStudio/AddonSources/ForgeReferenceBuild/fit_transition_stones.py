"""Plan and fit missing side masonry from actual exposed outer surface hits."""
import bpy,bmesh,json,math,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'TransitionFit_v14';out.mkdir(parents=True,exist_ok=True)
src=Path(__file__).parent;bpy.ops.wm.open_mainfile(filepath=str(root/'RearJointFit_v11/packed.blend'))
sc=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[];owners=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or not (o.name=='furnace_core' or o.name.startswith(('stone_','inferred_rear_stone_','hearth_stones_'))):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices);vertices.extend(o.matrix_world@v.co for v in mesh.vertices)
    for f in mesh.polygons:faces.append([start+i for i in f.vertices]);owners.append(o.name)
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(vertices,faces)
env=json.loads((src/'upright-furnace-envelope.json').read_text());_,_,rx,ry,wall,cap,power=env['parameters'];centre=Vector(env['center'])
courses=json.loads((src/'rear-masonry-plan.json').read_text())['courses'];parts=[]
for row,(z0,z1,count) in enumerate(courses):
    z=(z0+z1)/2;scale=1 if z<=wall else max(0,1-((z-wall)/cap)**2)**(power/2)
    for col in range(count):
        a=math.tau*(col+.5*(row%2))/count;normal=Vector((math.cos(a)/rx,math.sin(a)/ry,0)).normalized()
        expected=centre+Vector((rx*scale*math.cos(a),ry*scale*math.sin(a),z))
        tangent=normal.cross(Vector((0,0,1))).normalized();hits=[]
        for horizontal,vertical in [(0,0),(-.04,0),(.04,0),(0,-.04),(0,.04)]:
            probe=expected+tangent*horizontal+Vector((0,0,vertical))
            pos,n,index,distance=tree.ray_cast(probe+normal*.30,-normal,.38)
            if pos is not None and owners[index]=='furnace_core' and (pos-probe).length<.060:hits.append((pos,n))
        if len(hits)<3:continue
        pos=sum((h[0] for h in hits),Vector())/len(hits);n=sum((h[1] for h in hits),Vector()).normalized()
        parts.append({'id':f'stone_transition_{row}_{col}','position':list(pos),'normal':list(n),'halfWidth':(rx+ry)/2*scale*math.pi/count*1.12,'halfHeight':(z1-z0)*.58,'frontRelief':.035,'backDepth':.055,'exposedSamples':len(hits),'testedSamples':5})
assert all(p['halfWidth']>0 and p['halfHeight']>0 for p in parts)
(out/'transition-plan.json').write_text(json.dumps({'parts':parts,'validation':{'seedsAreObservedOuterCoreHits':True,'maxOuterSurfaceDistance':.060,'minimumExposedSamples':3,'clearance':'Hardware ignored only for wall discovery; actual new blocks trimmed around existing solids','fidelity':'NOT_APPROVED'}},indent=2))
cutverts=[];cutfaces=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or o.name in ('furnace_core','Inspection ground'):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(cutverts);cutverts.extend(o.matrix_world@q.co for q in mesh.vertices);cutfaces.extend([start+i for i in f.vertices] for f in mesh.polygons);ev.to_mesh_clear()
cutmesh=bpy.data.meshes.new('Transition neighbor and hardware clearance');cutmesh.from_pydata(cutverts,[],cutfaces);cutmesh.update()
cutter=bpy.data.objects.new('Temporary transition clearance',cutmesh);sc.collection.objects.link(cutter);cutter.hide_render=True
donors=[o for o in sc.objects if o.name.startswith(('stone_left_middle_','stone_left_lower_','stone_base_'))];coretree=BVHTree.FromObject(bpy.data.objects['furnace_core'],deps)
for p in parts:
    normal=Vector(p['normal']);u=normal.cross(Vector((0,0,1))).normalized();v=normal.cross(u).normalized();origin=Vector(p['position'])
    donor=donors[int(hashlib.sha256(p['id'].encode()).hexdigest()[:8],16)%len(donors)];pts=[q.co.copy() for q in donor.data.vertices];dc=sum(pts,Vector())/len(pts)
    _,dn,_,_=coretree.find_nearest(dc);dn.normalize();du=dn.cross(Vector((0,0,1))).normalized();dv=dn.cross(du).normalized()
    local=[Vector(((q-dc).dot(du),(q-dc).dot(dv),(q-dc).dot(dn))) for q in pts];low=[min(q[i] for q in local) for i in range(3)];high=[max(q[i] for q in local) for i in range(3)]
    points=[]
    for q in local:
        x=(q.x-low[0])/(high[0]-low[0])*2-1;y=(q.y-low[1])/(high[1]-low[1])*2-1;depth=(q.z-low[2])/(high[2]-low[2])
        points.append(origin+u*x*p['halfWidth']+v*y*p['halfHeight']+normal*(-p['backDepth']+depth*(p['backDepth']+p['frontRelief'])))
    mesh=bpy.data.meshes.new(p['id']);mesh.from_pydata(points,[],[tuple(f.vertices) for f in donor.data.polygons]);mesh.update();bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(p['id'],mesh);sc.collection.objects.link(o);o.data.materials.append(bpy.data.materials['Neutral untextured clay'])
    mod=o.modifiers.new('Fit around existing stone and hardware','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    bevel=o.modifiers.new('Small worn stone edges','BEVEL');bevel.width=.004;bevel.segments=3
bpy.data.objects.remove(cutter,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'transition.blend'));sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
cam=sc.camera;target=Vector((-.05,.14,.46));a=math.radians(116.064);e=math.radians(25);d=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
cam.location=target+d*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1;sc.render.resolution_x=1440;sc.render.resolution_y=1080;sc.render.filepath=str(out/'side-clay.png');bpy.ops.render.render(write_still=True)
print('TRANSITION_PIECES',len(parts))
