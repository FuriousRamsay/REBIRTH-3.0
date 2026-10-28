import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'RadialStoneProfiles_v65';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SidePatchVolume_v62/side-patch-volume-clay.blend'));core=bpy.data.objects['furnace_core'];tree=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get());parts=[]
for name in ('stone_left_shoulder_02','stone_left_middle_01','stone_left_edge_behind_bellows'):
    obj=bpy.data.objects[name];n=(len(obj.data.vertices)-1)//4;assert len(obj.data.vertices)==4*n+1;world=[obj.matrix_world@v.co for v in obj.data.vertices]
    center=sum(world[n:2*n],Vector())/n;near,normal,index,d=tree.find_nearest(core.matrix_world.inverted()@center);normal=(core.matrix_world.to_3x3()@normal).normalized()
    assert near is not None and d<.25
    changes=[]
    for i in range(n):
        target=center+(world[n+i]-center)*.9-normal*.075;changes.append({'index':i,'from':list(world[i]),'to':list(target)})
    test=bmesh.new();test.from_mesh(obj.data);test.verts.ensure_lookup_table();inverse=obj.matrix_world.inverted()
    for change in changes:test.verts[change['index']].co=inverse@Vector(change['to'])
    volume=test.calc_volume(signed=True);nonManifold=sum(not e.is_manifold for e in test.edges);zeroArea=sum(f.calc_area()<1e-12 for f in test.faces);test.free();assert volume>0 and nonManifold==0 and zeroArea==0
    parts.append({'object':name,'frontRingCenter':list(center),'wallContact':list(core.matrix_world@near),'wallNormal':list(normal),'radialDepthMeters':.075,'backRingScale':.9,'vertexChanges':changes,'signedVolume':volume,'nonManifoldEdges':nonManifold,'zeroAreaFaces':zeroArea})
plan={'source':'SidePatchVolume_v62/side-patch-volume-clay.blend','method':'Hold three front rings fixed. Reconstruct only back cap along nearest physical core wall normal with75 mm depth and90 percent profile scale. Closed topology/positive volume checks precede execution. Hidden geometry inferred; full visual and intersection review required.','parts':parts,'status':'CLAY_REVIEW_REQUIRED'};path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps([{'object':p['object'],'normal':p['wallNormal'],'volume':p['signedVolume'],'maximumMoveMeters':max((Vector(c['to'])-Vector(c['from'])).length for c in p['vertexChanges'])} for p in parts]));sys.exit(0)
assert json.loads(path.read_text())==plan
for part in parts:
    obj=bpy.data.objects[part['object']];inverse=obj.matrix_world.inverted()
    for change in part['vertexChanges']:obj.data.vertices[change['index']].co=inverse@Vector(change['to'])
    obj.data.update()
assert not (out/'radial-profiles-clay.blend').exists();sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
bpy.ops.wm.save_as_mainfile(filepath=str(out/'radial-profiles-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
