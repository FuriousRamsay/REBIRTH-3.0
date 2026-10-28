"""Sample first-hit geometry in saved camera views; separate shell from chamber."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
plan=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text())
axis=Vector(plan['outwardAxis']);front=plan['frontDepth']
results=[]
sources=[root/'approved-clay.blend',root/'MasonryJointFit_v1/joint-fit.blend']
if '--mortar' in sys.argv:sources.append(root/'MasonryMortar_v5/mortar.blend')
for source in sources:
    bpy.ops.wm.open_mainfile(filepath=str(source));sc=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get();cam=sc.camera
    width,height=sc.render.resolution_x,sc.render.resolution_y
    sx=cam.data.ortho_scale;sy=sx*height/width;direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1))
    counts={};samples=[]
    vertices=[];faces=[];owners=[]
    for o in sc.objects:
        if o.type!='MESH' or o.hide_render or o.name=='Inspection ground':continue
        ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices)
        vertices.extend(o.matrix_world@v.co for v in mesh.vertices)
        for p in mesh.polygons:faces.append([start+i for i in p.vertices]);owners.append(o.name)
        ev.to_mesh_clear()
    tree=BVHTree.FromPolygons(vertices,faces)
    for y in range(0,height,10):
        for x in range(0,width,10):
            origin=cam.matrix_world@Vector(((x/width-.5)*sx,(.5-y/height)*sy,0))
            loc,normal,index,distance=tree.ray_cast(origin,direction,100)
            if loc is None:continue
            name=owners[index]
            kind='other'
            if name.startswith(('stone_','inferred_rear_stone_','hearth_stones_')):kind='stone'
            if name=='furnace_core':
                # Back of chamber is intentionally visible; it is not an exterior gap.
                kind='chamber_core' if loc.dot(axis)<front-.025 and .30<loc.z<.73 else 'exposed_backing'
                if kind=='exposed_backing':samples.append({'pixel':[x,y],'position':list(loc),'normal':list(normal)})
            counts[kind]=counts.get(kind,0)+1
    results.append({'source':str(source),'sampleSpacingPixels':10,'firstHitCounts':counts,'exposedBackingSamples':samples})
out=root/('MasonryMortar_v5/exposure-comparison.json' if '--mortar' in sys.argv else 'MasonryJointFit_v1/exposure-comparison.json');out.write_text(json.dumps({'results':results,'limits':'Sparse ray grid measures exposed shell, not fidelity or watertight construction. Chamber classification uses current chamber axis/depth; edge cases require visual review.'},indent=2))
print(json.dumps([{k:v for k,v in r.items() if k!='exposedBackingSamples'} for r in results],indent=2))
