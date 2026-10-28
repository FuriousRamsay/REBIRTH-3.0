"""Give inferred rear masonry individual fracture faces instead of curved tiles."""
import bpy,bmesh,json,hashlib,random,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'RearBlockFit_v6';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MasonryMortar_v5/mortar.blend'))
core=bpy.data.objects['furnace_core'];tree=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get())
schedule=[]
for o in bpy.context.scene.objects:
    if not o.name.startswith('inferred_rear_stone_'):continue
    centre=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
    near,normal,index,distance=tree.find_nearest(centre)
    schedule.append({'name':o.name,'centre':list(centre),'outwardNormal':list(normal.normalized()),'edgeVariation':.004,'faceRelief':.018,'role':'Inferred rear solid stone; preserve block footprint and mortar course fit'})
(out/'rear-shape-plan.json').write_text(json.dumps({'source':'MasonryMortar_v5/mortar.blend','parts':schedule,'validation':{'allPartsHaveSurfaceNormal':all(Vector(p['outwardNormal']).length>.99 for p in schedule),'hiddenSurfaceInferred':True,'clayReviewRequired':True}},indent=2))
for item in schedule:
    o=bpy.data.objects[item['name']];normal=Vector(item['outwardNormal']);rng=random.Random(int(hashlib.sha256(o.name.encode()).hexdigest()[:12],16))
    bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
    # Retain mortar footprint while breaking ruler-straight block edge heights.
    for v in bm.verts:v.co.z+=rng.uniform(-.004,.004)
    bm.normal_update()
    for f in list(bm.faces):
        if f.normal.dot(normal)<.55:continue
        corners=list(f.verts);centre=f.calc_center_median()+normal*rng.uniform(.006,.018)
        pivot=bm.verts.new(centre)
        for a,b in zip(corners,corners[1:]+corners[:1]):bm.faces.new((a,b,pivot))
        bm.faces.remove(f)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for mod in o.modifiers:
        if mod.type=='BEVEL':mod.width=.003;mod.segments=2
bpy.ops.wm.save_as_mainfile(filepath=str(out/'rear-blocks.blend'))
sc=bpy.context.scene;cam=sc.camera;target=Vector((-.05,.14,.46));a=math.radians(206.064);e=math.radians(25)
direction=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
cam.location=target+direction*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1
sc.render.resolution_x=1440;sc.render.resolution_y=1080;sc.cycles.samples=32;sc.render.filepath=str(out/'rear-clay.png');bpy.ops.render.render(write_still=True)
