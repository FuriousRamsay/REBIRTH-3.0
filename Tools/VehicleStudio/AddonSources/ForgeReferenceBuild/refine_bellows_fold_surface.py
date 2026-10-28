import bpy,json,sys,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsFoldSurface_v36';out.mkdir(parents=True,exist_ok=True)
regularize='--regularize-cross-section' in sys.argv
if regularize:out=root/'BellowsCrossSection_v37';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'BellowsLeatherNormals_v35/leather-normals-clay.blend'))
body=bpy.data.objects['planned_mesh_000'];assert len(body.data.vertices)==41*256
values=[v.co.copy() for v in body.data.vertices];changes=[]
for row in range(2,39):
    for col in range(256):
        index=row*256+col;p=values[index]
        average=sum(values[r*256+c].x for r,c in [(row-1,col),(row+1,col),(row,(col-1)%256),(row,(col+1)%256)])/4
        shift=max(-.002,min(.002,(average-p.x)*.5))
        destination=p.copy()
        if regularize:
            value=math.cos(col/256*math.tau);old=math.copysign(abs(value)**.35,value)
            new=value*(value*value+.02**2)**(-.325)/(1+.02**2)**(-.325)
            vertical=(values[row*256]-values[row*256+128])*.5
            motion=vertical*(new-old)*math.sin(math.pi*row/40)**2;motion.x=0
            assert motion.length<=.012
            destination+=motion
        else:destination.x+=shift
        if (destination-p).length<.00001:continue
        changes.append({'index':index,'from':list(p),'to':list(destination)})
plan={'source':'BellowsLeatherNormals_v35/leather-normals-clay.blend','object':body.name,'method':'One bounded depth-coordinate relaxation on interior 37 rings; preserve Y/Z and four attachment rings. Neighbour average contribution 50 percent, maximum 2 mm displacement. Refit seam and stitch curves after body edit.','changes':changes,'status':'EXPERIMENT_REQUIRES_CLOSEUP_REVIEW'}
if regularize:plan['method']='Regularize signed cos^0.35 cross-section using epsilon 0.02; alter Y/Z only, taper by sin(pi*ring/40)^2, preserve four attachment rings. Validate each movement below 12 mm. Refit seam and stitch curves to edited surface.'
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('PLANNED_VERTICES',len(changes));sys.exit(0)
assert json.loads(path.read_text())==plan
for change in changes:
    assert (Vector(change['to'])-Vector(change['from'])).length<=(.012001 if regularize else .002001)
    body.data.vertices[change['index']].co=change['to']
body.data.update();bpy.context.view_layer.update();tree=BVHTree.FromObject(body,bpy.context.evaluated_depsgraph_get());inverse=body.matrix_world.inverted()
refit=0
for obj in bpy.context.scene.objects:
    if obj.type!='CURVE' or not obj.name.startswith('bellows_seam_'):continue
    stitch='_stitch_' in obj.name
    for spline in obj.data.splines:
        for point in spline.points:
            world=obj.matrix_world@Vector(point.co[:3]);near,n,index,d=tree.find_nearest(inverse@world)
            if near is None or d>.015:continue
            normal=(body.matrix_world.to_3x3()@n).normalized();position=body.matrix_world@near+normal*(.003 if stitch else .002)
            local=obj.matrix_world.inverted()@position;point.co=(*local,1);refit+=1
assert not (out/'fold-surface-clay.blend').exists()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'fold-surface-clay.blend'))
sc=bpy.context.scene;cam=sc.camera;seams=json.loads((root/'BellowsSeams_v33/geometry-plan.json').read_text());points=[Vector(p) for part in seams['parts'] for p in part['points']];target=sum(points,Vector())/len(points)
direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));cam.location=target-direction*4;cam.data.ortho_scale=.52
sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.cycles.device='CPU';sc.cycles.samples=48;sc.render.filepath=str(out/'bellows-closeup.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'modifiedVertices':len(changes),'refittedCurvePoints':refit,'maximumDisplacementMeters':max((Vector(c['to'])-Vector(c['from'])).length for c in changes),'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
