"""Plan and build fitted rear iron straps from the actual masonry envelope."""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'RearIronwork_v26';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'BasinHeat_v25/basin-heat.blend'))
sc=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get();points=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or not (o.name=='furnace_core' or o.name.startswith(('stone_','inferred_rear_stone_'))):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();points.extend(o.matrix_world@v.co for v in mesh.vertices);ev.to_mesh_clear()
bm=bmesh.new()
for point in points:bm.verts.new(point)
hull=bmesh.ops.convex_hull(bm,input=list(bm.verts));bm.verts.ensure_lookup_table();bm.verts.index_update()
tree=BVHTree.FromPolygons([v.co.copy() for v in bm.verts],[[v.index for v in f.verts] for f in bm.faces]);bm.free()
center=Vector((.2169066,.4890315,0));parts=[]
for side,angle in [('left',math.radians(57)),('right',math.radians(123))]:
    radial=Vector((math.cos(angle),math.sin(angle),0));tangent=Vector((-radial.y,radial.x,0));stations=[]
    for k in range(22):
        z=.055+k*(.835-.055)/21
        origin=center+Vector((0,0,z))+radial*2
        hit,normal,index,distance=tree.ray_cast(origin,-radial,3)
        assert hit is not None,('Missing masonry envelope',side,z)
        stations.append({'center':list(hit+radial*.010),'radial':list(radial),'tangent':list(tangent),'z':z})
    # Curve toward the collar at the top using measured envelope hits.
    for z in [.850,.866,.880,.892]:
        hit,normal,index,distance=tree.ray_cast(center+Vector((0,0,z))+radial*2,-radial,3)
        if hit is not None:stations.append({'center':list(hit+radial*.010),'radial':list(radial),'tangent':list(tangent),'z':z})
    parts.append({'name':'harness_rear_'+side,'angleDegrees':math.degrees(angle),'width':.060,'thickness':.007,'stations':stations,'boltStations':[2,8,15,21],'rearShape':'Inferred from front harness style; actual rear not shown in concept'})
plan={'source':'BasinHeat_v25/basin-heat.blend','construction':'Solid ribbons on actual convex masonry envelope; no front geometry changes','center':list(center),'parts':parts,'checks':{'allStationsMeasured':True,'masonryClearanceMeters':.010,'noFrontShapeChanges':True},'fidelity':'CLAY_ALL_SIDES_REVIEW_REQUIRED'}
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
assert len(parts)==2 and all(len(p['stations'])>=22 for p in parts)
neutral=bpy.data.materials.get('Neutral untextured clay')
for part in parts:
    verts=[];faces=[]
    for station in part['stations']:
        p=Vector(station['center']);r=Vector(station['radial']);t=Vector(station['tangent'])
        for depth in [0,part['thickness']]:
            for sign in [-1,1]:verts.append(p+t*sign*part['width']/2+r*depth)
    for i in range(len(part['stations'])-1):
        j=i*4;q=j+4;faces.extend([(j,q,q+1,j+1),(j+2,j+3,q+3,q+2),(j,j+2,q+2,q),(j+1,q+1,q+3,j+3)])
    faces.extend([(0,1,3,2),(len(verts)-4,len(verts)-2,len(verts)-1,len(verts)-3)])
    mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(verts,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(part['name'],mesh);sc.collection.objects.link(o);o.data.materials.append(neutral)
    bevel=o.modifiers.new('Iron edge radius','BEVEL');bevel.width=.0015;bevel.segments=3;o['surfaceRole']='iron'
    for i in part['boltStations']:
        station=part['stations'][i];r=Vector(station['radial']);p=Vector(station['center'])+r*.011
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=p)
        bolt=bpy.context.object;bolt.name=part['name']+'_rivet_'+str(i);bolt.scale=(.014,.014,.008);bolt.rotation_euler=r.to_track_quat('Z','Y').to_euler();bolt.data.materials.append(neutral);bolt['surfaceRole']='iron'
# Neutral clay review only; remove material/light advantages from geometry assessment.
for o in sc.objects:
    if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground':o.data.materials.clear();o.data.materials.append(neutral)
    if o.type=='LIGHT' and o.data.type=='POINT':o.data.energy=0
sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'rear-ironwork-clay.blend'));bpy.ops.render.render(write_still=True)
target=Vector((.03,.43,.48));cam=sc.camera
cam.location=target+Vector((.9,1.7,.9));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.0
sc.render.filepath=str(out/'rear-clay.png');bpy.ops.render.render(write_still=True)
(out/'geometry-receipt.json').write_text(json.dumps({'newStraps':2,'newRivets':8,'originalFrontUnchanged':True,'clayReview':'PENDING','rearGeometry':'Inferred'},indent=2))
print('REAR_IRONWORK_CLAY_VIEWS_SAVED')
