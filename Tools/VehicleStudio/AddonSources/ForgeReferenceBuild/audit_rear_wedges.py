import bpy,json,math,bmesh,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'RearWedgeAudit_v61';out.mkdir(parents=True,exist_ok=True)
source='LocalCrownContacts_v59/crown-top-face-clay.blend'
if '--facet-indices' in sys.argv:
    source='RadialStoneProfiles_v65/radial-profiles-clay.blend';out=root/'RearFacetAudit_v67';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/source));sc=bpy.context.scene;cam=sc.camera;target=Vector((.03,.38,.46));a=math.radians(135)
cam.location=target+Vector((math.cos(a)*3,math.sin(a)*3,1.6));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();q=cam.rotation_euler.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));direction=q@Vector((0,0,-1))
points=[obj.matrix_world@Vector(p) for obj in sc.objects if obj.type in ('MESH','CURVE') and not obj.hide_render and obj.name!='Inspection ground' for p in obj.bound_box];xs=[(p-target).dot(right) for p in points];ys=[(p-target).dot(up) for p in points]
cam.location+=right*((min(xs)+max(xs))/2)+up*((min(ys)+max(ys))/2);scale=max(max(xs)-min(xs),(max(ys)-min(ys))*1200/900)*1.13;bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[];owners=[]
for obj in sc.objects:
    if obj.type!='MESH' or obj.hide_render or obj.name=='Inspection ground':continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices);vertices.extend(obj.matrix_world@v.co for v in mesh.vertices)
    for polygon in mesh.polygons:faces.append([start+i for i in polygon.vertices]);owners.append(obj.name)
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(vertices,faces);samples=[]
for x,y in [(670,390),(670,450),(700,475),(670,510),(710,535),(600,585)]:
    origin=cam.matrix_world@Vector(((x/1200-.5)*scale,(.5-y/900)*scale*900/1200,0));p,n,index,d=tree.ray_cast(origin,direction,100)
    record={'pixel':[x,y],'object':owners[index] if p is not None else None,'position':list(p) if p is not None else None}
    if p is not None:
        obj=bpy.data.objects[owners[index]];baseTree=BVHTree.FromPolygons([obj.matrix_world@v.co for v in obj.data.vertices],[list(face.vertices) for face in obj.data.polygons]);near,nn,baseIndex,distance=baseTree.find_nearest(p)
        if near is not None:
            ids=list(obj.data.polygons[baseIndex].vertices);n=(len(obj.data.vertices)-1)//4
            record.update(baseFace=baseIndex,baseVertices=ids,ringIndices=[i//n for i in ids] if len(obj.data.vertices)==4*n+1 and n>0 else None,baseFaceDistanceMeters=distance)
    samples.append(record)
objects=[]
for name in sorted({s['object'] for s in samples if s['object']}):
    obj=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(obj.data);objects.append({'name':name,'dimensions':list(obj.dimensions),'vertices':len(bm.verts),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'signedVolume':bm.calc_volume(signed=True)});bm.free()
report={'source':source,'view':'Fitted angle135','samples':samples,'objects':objects,'limits':'Pixel seeds locate visible meshes only; nearest base face approximates bevel provenance. Exact hidden reference geometry is not known.'};path=out/'wedge-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps(report))
