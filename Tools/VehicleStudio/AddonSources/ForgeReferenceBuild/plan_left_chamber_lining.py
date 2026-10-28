"""Measure three explicit left-wall envelopes against actual saved chamber geometry."""
import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'LeftChamberLining_v32';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'HearthLiningFit_v31/hearth-lining-clay.blend'))
sc=bpy.context.scene;camera=sc.camera;core=bpy.data.objects['furnace_core'];deps=bpy.context.evaluated_depsgraph_get()
tree=BVHTree.FromObject(core,deps);inverse=core.matrix_world.inverted()
width,height=sc.render.resolution_x,sc.render.resolution_y;sx=camera.data.ortho_scale;sy=sx*height/width
direction=camera.matrix_world.to_quaternion()@Vector((0,0,-1));records=[]
outlines=[[[943,476],[961,464],[978,476],[983,507],[966,518],[945,505]],[[938,511],[962,513],[977,530],[971,552],[949,557],[934,542]],[[932,550],[951,554],[967,572],[952,589],[933,577],[926,564]]]
for number,outline in enumerate(outlines):
    cx=sum(p[0] for p in outline)/len(outline);cy=sum(p[1] for p in outline)/len(outline)
    origin=camera.matrix_world@Vector(((cx/width-.5)*sx,(.5-cy/height)*sy,0))
    pos,n,face,distance=tree.ray_cast(inverse@origin,(inverse.to_3x3()@direction).normalized(),100)
    assert pos is not None,'Unsupported contour'
    pos=core.matrix_world@pos;n=(core.matrix_world.to_3x3()@n).normalized()
    if n.dot(direction)>0:n=-n
    assert abs(n.dot(direction))>.08,'Grazing unsupported plane'
    vertices=[]
    for scale,relief in ((.9,-.006),(1,0),(.82,.012)):
        for x,y in outline:
            point=camera.matrix_world@Vector((((cx+(x-cx)*scale)/width-.5)*sx,(.5-(cy+(y-cy)*scale)/height)*sy,0))
            point+=direction*((pos-point).dot(n)/direction.dot(n));point+=n*relief;vertices.append(list(point))
    records.append({'id':f'hearth_left_lining_{number:02d}','pixelOutline':outline,'center':list(pos),'normal':list(n),'rotationBasis':'Camera plane projected onto measured chamber tangent','vertices':vertices,'depthReliefMeters':.012})
plan={'status':'MEASURED_CONTOURS_REQUIRE_CLAY_REVIEW','source':'HearthLiningFit_v31/hearth-lining-clay.blend','scope':'Three left inner-wall stones only; no source objects changed','parts':records,'limitations':'Contours are measured on current render; source-reference comparison remains required. Tangent projection is an approximation to curved chamber.'}
path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2))
print(json.dumps([{k:v for k,v in p.items() if k!='vertices'} for p in records]))
