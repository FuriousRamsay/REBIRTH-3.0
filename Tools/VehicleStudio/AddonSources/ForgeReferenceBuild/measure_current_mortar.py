"""Measure exposed joints in the actual saved candidate before altering masonry."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MortarJointMeasurement_v84';out.mkdir(parents=True,exist_ok=True)
assert not (out/'joint-measurements.json').exists()
bpy.ops.wm.open_mainfile(filepath=str(root/'ClippedStoneVolumes_v81/closed-patches-clay.blend'))
sc=bpy.context.scene;cam=sc.camera;deps=bpy.context.evaluated_depsgraph_get()
vertices=[];faces=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or not o.name.startswith(('stone_','inferred_rear_stone_')):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices)
    vertices.extend(o.matrix_world@v.co for v in mesh.vertices)
    faces.extend([start+i for i in p.vertices] for p in mesh.polygons);ev.to_mesh_clear()
tree=BVHTree.FromPolygons(vertices,faces)
w=sc.render.resolution_x;h=sc.render.resolution_y
q=cam.matrix_world.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));direction=q@Vector((0,0,-1))
samples=[]
for y in range(6,h,12):
    for x in range(6,w,12):
        origin=cam.location+right*((x/w-.5)*cam.data.ortho_scale)+up*((.5-y/h)*cam.data.ortho_scale*h/w)
        hit,p,n,face,obj,matrix=sc.ray_cast(deps,origin,direction)
        if not hit or obj.name!='furnace_core':continue
        near,normal,index,distance=tree.find_nearest(p)
        if near is None:continue
        samples.append({'pixel':[x,y],'corePoint':list(p),'nearestStonePoint':list(near),'distanceMeters':distance,'normal':list(n)})
report={'source':'ClippedStoneVolumes_v81/closed-patches-clay.blend','sampleSpacingPixels':12,'exposedCoreSamples':samples,'limits':'Nearest-surface distance is not automatically joint depth. Hearth lining and exposed floor must be classified before any movement.','proposedJointProfile':{'recessBelowAdjacentStoneMeters':[.003,.008],'edgeVariationMeters':[.001,.003],'protect':'Stone faces, hearth aperture, collar and attachment sockets'},'status':'MEASURED_LOCATIONS_REQUIRE_CLASSIFICATION_BEFORE_GEOMETRY_EDIT'}
(out/'joint-measurements.json').write_text(json.dumps(report,indent=2))
mat=bpy.data.materials.new('Mortar diagnostic only');mat.diffuse_color=(.7,.03,.35,1);mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.7,.03,.35,1)
core=bpy.data.objects['furnace_core'];core.data.materials.clear();core.data.materials.append(mat)
sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.filepath=str(out/'exposed-mortar-diagnostic.png')
bpy.ops.render.render(write_still=True)
print(json.dumps({'exposedCoreSamples':len(samples),'receipt':str(out/'joint-measurements.json')}))
