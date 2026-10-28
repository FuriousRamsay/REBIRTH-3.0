import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsStitches_v34';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'BellowsSeams_v33/bellows-seams-clay.blend'))
body=bpy.data.objects['planned_mesh_000'];tree=BVHTree.FromObject(body,bpy.context.evaluated_depsgraph_get());inverse=body.matrix_world.inverted();parts=[]
seams=json.loads((root/'BellowsSeams_v33/geometry-plan.json').read_text())
for seam in seams['parts']:
    points=[Vector(p) for p in seam['points']]
    for i in range(1,len(points)-1):
        p=points[i];near,n,index,d=tree.find_nearest(inverse@p);n=(body.matrix_world.to_3x3()@n).normalized();t=(points[i+1]-points[i-1]).normalized();cross=n.cross(t).normalized()
        center=p+n*.001;ends=[center-cross*.003,center+cross*.003]
        parts.append({'id':f"{seam['id']}_stitch_{i:02d}",'endpoints':[list(v) for v in ends],'radiusMeters':.0006})
assert len(parts)>100
for p in parts:assert abs((Vector(p['endpoints'][1])-Vector(p['endpoints'][0])).length-.006)<.00001
plan={'source':'BellowsSeams_v33/bellows-seams-clay.blend','status':'MEASURED_PLAN_REQUIRES_CLAY_REVIEW','parts':parts,'method':'6 mm transverse stitches centered on each surface seam sample; 0.6 mm thread radius; original body and four seam curves unchanged.'}
path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('PLANNED_STITCHES',len(parts))
