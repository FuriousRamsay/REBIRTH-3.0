"""Fit residual exterior patches measured from the actual side camera."""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'SideMeasuredFit_v15';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'TransitionFit_v14/transition.blend'))
sc=bpy.context.scene;originalCamera=sc.camera.matrix_world.copy();originalScale=sc.camera.data.ortho_scale
cam=sc.camera;target=Vector((-.05,.14,.46));a=math.radians(116.064);e=math.radians(25);d=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
cam.location=target+d*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1;bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[];owners=[];cutverts=[];cutfaces=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or o.name=='Inspection ground':continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(verts);verts.extend(o.matrix_world@v.co for v in mesh.vertices)
    for f in mesh.polygons:faces.append([start+i for i in f.vertices]);owners.append(o.name)
    if o.name!='furnace_core':
        start=len(cutverts);cutverts.extend(o.matrix_world@v.co for v in mesh.vertices);cutfaces.extend([start+i for i in f.vertices] for f in mesh.polygons)
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(verts,faces);direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));groups={}
for y in range(420,910,12):
    for x in range(240,600,12):
        origin=cam.matrix_world@Vector(((x/1440-.5)*2.1,(.5-y/1080)*2.1*1080/1440,0))
        p,n,index,dist=tree.ray_cast(origin,direction,100)
        if p is not None and owners[index]=='furnace_core':groups.setdefault((x//60,y//72),[]).append({'pixel':[x,y],'position':list(p),'normal':list(n)})
parts=[{'id':f'stone_side_patch_{i:02d}','samples':samples} for i,samples in enumerate(groups.values()) if len(samples)>=4]
(out/'side-patch-plan.json').write_text(json.dumps({'parts':parts,'validation':{'allSamplesActualVisibleCore':True,'region':'Exterior beside bellows in side view; chamber excluded','minimumSamples':4,'depthInferred':True,'visualReviewRequired':True}},indent=2))
mesh=bpy.data.meshes.new('Side patch clearance');mesh.from_pydata(cutverts,[],cutfaces);mesh.update();cutter=bpy.data.objects.new('Side patch temporary trim',mesh);sc.collection.objects.link(cutter);cutter.hide_render=True
for part in parts:
    bm=bmesh.new()
    for s in part['samples']:
        p=Vector(s['position']);n=Vector(s['normal']).normalized();u=n.cross(Vector((0,0,1))).normalized();v=n.cross(u).normalized()
        for depth in [-.06,.030]:
            for x,y in [(-1,-1),(-1,1),(1,-1),(1,1)]:bm.verts.new(p+n*depth+(u*x+v*y)*.012)
    bmesh.ops.convex_hull(bm,input=list(bm.verts));unused=[v for v in bm.verts if not v.link_faces]
    if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));mesh=bpy.data.meshes.new(part['id']);bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(part['id'],mesh);sc.collection.objects.link(o);o.data.materials.append(bpy.data.materials['Neutral untextured clay']);mod=o.modifiers.new('Neighbor fit','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    bevel=o.modifiers.new('Stone edge radius','BEVEL');bevel.width=.002;bevel.segments=2
bpy.data.objects.remove(cutter,do_unlink=True);cam.matrix_world=originalCamera;cam.data.ortho_scale=originalScale
bpy.ops.wm.save_as_mainfile(filepath=str(out/'side-fit.blend'))
cam.location=target+d*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1;sc.render.resolution_x=1440;sc.render.resolution_y=1080;sc.cycles.samples=32;sc.render.filepath=str(out/'side-clay.png');bpy.ops.render.render(write_still=True)
print('MEASURED_SIDE_PATCHES',len(parts))
