import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).parent;root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsSeams_v33';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'LeftChamberLining_v32/left-lining-clay.blend'))
body=bpy.data.objects['planned_mesh_000'];tree=BVHTree.FromObject(body,bpy.context.evaluated_depsgraph_get());inverse=body.matrix_world.inverted()
frame=json.loads((repo/'bellows-frame-plan.json').read_text());a,e=map(math.radians,[frame['cameraAzimuthDegrees'],frame['cameraElevationDegrees']])
R=Vector((math.cos(a),-math.sin(a),0));U=Vector((math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)));D=R.cross(U)
trace=json.loads((repo/'left-reference-records.json').read_text());paths=next(p for p in trace['records'] if p['id']=='fold_envelope')['foldRidgePaths'];parts=[]
for number,path in enumerate(paths):
    points=[];misses=0
    for start,end in zip(path,path[1:]):
        for i in range(10):
            t=i/10;x=start[0]*(1-t)+end[0]*t;y=start[1]*(1-t)+end[1]*t
            origin=R*((x-891)/760)+U*((935-y)/760)+D*5
            pos,n,face,distance=tree.ray_cast(inverse@origin,(inverse.to_3x3()@(-D)).normalized())
            if pos is None:misses+=1;continue
            world=body.matrix_world@pos;normal=(body.matrix_world.to_3x3()@n).normalized()
            if normal.dot(D)<0:normal=-normal
            points.append(list(world+normal*.002))
    parts.append({'id':f'bellows_seam_{number}','radiusMeters':.0025,'surfaceOffsetMeters':.002,'points':points,'missedSamples':misses})
plan={'status':'SURFACE_PROJECTED_PLAN_REQUIRES_INSPECTION','source':'LeftChamberLining_v32/left-lining-clay.blend','body':body.name,'parts':parts,'method':'Four reference-traced leather seams projected onto actual saved bellows surface; original source camera basis. No body shape changed.'}
assert all(len(p['points'])>=15 for p in parts)
path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2))
print(json.dumps([{'id':p['id'],'points':len(p['points']),'misses':p['missedSamples']} for p in parts]))
