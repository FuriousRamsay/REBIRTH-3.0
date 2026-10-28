import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'WallStoneFacets_v68';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SidePatchVolume_v62/side-patch-volume-clay.blend'));sc=bpy.context.scene;D=sc.camera.rotation_euler.to_quaternion()@Vector((0,0,1));core=bpy.data.objects['furnace_core'];tree=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get());parts=[]
for name in ('stone_left_shoulder_02','stone_left_middle_01','stone_left_edge_behind_bellows'):
    obj=bpy.data.objects[name];n=(len(obj.data.vertices)-1)//4;world=[obj.matrix_world@v.co for v in obj.data.vertices];center=sum(world[n:2*n],Vector())/n
    near,normal,index,d=tree.find_nearest(core.matrix_world.inverted()@center);normal=(core.matrix_world.to_3x3()@normal).normalized();alignment=normal.dot(D);assert alignment>.2
    changes=[]
    for i,p in enumerate(world):
        row=i//n;q=p-D*p.dot(D)
        # Macro fracture tilt uses a bounded remnant of the measured surface variation.
        variation=max(-.008,min(.008,(p-center).dot(normal)))
        relief=[-.060,0,.025+variation,.032+variation*.5,.036][row]
        target=q+D*((normal.dot(center)+relief-normal.dot(q))/alignment)
        changes.append({'index':i,'from':list(p),'to':list(target),'ring':row,'radialReliefMeters':relief})
    bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();inverse=obj.matrix_world.inverted()
    for change in changes:bm.verts[change['index']].co=inverse@Vector(change['to'])
    volume=bm.calc_volume(signed=True);nonManifold=sum(not e.is_manifold for e in bm.edges);zero=sum(f.calc_area()<1e-12 for f in bm.faces);bm.free();assert volume>0 and nonManifold==0 and zero==0
    parts.append({'object':name,'planeAnchor':list(center),'wallNormal':list(normal),'cameraAlignment':alignment,'vertexChanges':changes,'signedVolume':volume,'nonManifoldEdges':nonManifold,'zeroAreaFaces':zero})
plan={'source':'SidePatchVolume_v62/side-patch-volume-clay.blend','cameraAxis':list(D),'method':'Retain every vertex reference-camera projection. Solve depth against physical wall normal at silhouette-ring center. Rear relief -60 mm; contour0; front facets25 mm with bounded8 mm variation; inner facets32 mm; tip36 mm. Changes explicit per vertex; hidden shape inferred.','parts':parts,'status':'GEOMETRY_REVIEW_REQUIRED_BEFORE_BUILD'}
path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([{'object':p['object'],'normal':p['wallNormal'],'alignment':p['cameraAlignment'],'maxMoveMeters':max((Vector(c['to'])-Vector(c['from'])).length for c in p['vertexChanges']),'volume':p['signedVolume']} for p in parts]))
