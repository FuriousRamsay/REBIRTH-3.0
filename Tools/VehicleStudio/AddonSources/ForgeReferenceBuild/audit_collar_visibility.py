import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/CollarDetails_v21')
source=root/'collar-details.blend'
if '--current-clay' in sys.argv:
    root=root.parent/'BellowsLeatherNormals_v35';source=root/'leather-normals-clay.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
sc=bpy.context.scene;cam=sc.camera;deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[];owners=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or o.name=='Inspection ground':continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(verts)
    verts.extend(o.matrix_world@v.co for v in mesh.vertices)
    for p in mesh.polygons:faces.append([start+i for i in p.vertices]);owners.append(o.name)
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(verts,faces);direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));results=[]
for name in ['collar_front_bolt_plate','collar_front_bolt','collar_inner_basin','collar_inner_plug']:
    o=bpy.data.objects[name];samples=[]
    for point in [o.matrix_world@Vector(p) for p in o.bound_box]+[o.location]:
        projected=world_to_camera_view(sc,cam,point)
        origin=point-direction*5;hit,n,index,d=tree.ray_cast(origin,direction,10)
        samples.append({'point':list(point),'pixel':[projected.x*sc.render.resolution_x,(1-projected.y)*sc.render.resolution_y],'firstHit':owners[index] if hit is not None else None,'distanceToTarget':(hit-point).length if hit is not None else None})
    results.append({'object':name,'samples':samples})
(root/'visibility-audit.json').write_text(json.dumps(results,indent=2))
print(json.dumps([{'object':r['object'],'center':r['samples'][-1]} for r in results],indent=2))
center=Vector((.268042415,.54801476,.93074262));candidates=[]
for degrees in range(-175,-54,5):
    axis=Vector((math.cos(math.radians(degrees)),math.sin(math.radians(degrees)),0))
    for z in [.885,.895,.905,.915]:
        point=center+axis*.158;point.z=z
        projected=world_to_camera_view(sc,cam,point);hit,n,index,d=tree.ray_cast(point-direction*5,direction,10)
        distance=(hit-point).length if hit is not None else 999
        if hit is not None and distance<.020:
            pixel=[projected.x*sc.render.resolution_x,(1-projected.y)*sc.render.resolution_y]
            candidates.append({'axis':list(axis),'point':list(point),'pixel':pixel,'firstHit':owners[index],'distance':distance,'targetPixelDistance':math.hypot(pixel[0]-839,pixel[1]-166)})
candidates.sort(key=lambda c:c['targetPixelDistance'])
(root/'visible-plate-position-candidates.json').write_text(json.dumps(candidates,indent=2))
print('VISIBLE_CANDIDATES',json.dumps(candidates[:4]))
