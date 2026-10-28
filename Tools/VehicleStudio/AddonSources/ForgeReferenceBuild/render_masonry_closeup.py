import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'MasonryShapeReview_v44';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CrownTopFaces_v43/crown-top-face-clay.blend'))
sc=bpy.context.scene;cam=sc.camera
names=['stone_arch_crown_left','stone_arch_crown_right','stone_sill_right','stone_under_sill_right'];parts=[];points=[]
for name in names:
    obj=bpy.data.objects.get(name)
    if obj is None:continue
    world=[obj.matrix_world@v.co for v in obj.data.vertices];points.extend(world)
    parts.append({'object':name,'vertices':len(obj.data.vertices),'polygons':len(obj.data.polygons),'smoothFaces':sum(p.use_smooth for p in obj.data.polygons),'modifiers':[{'type':m.type,'width':getattr(m,'width',None),'segments':getattr(m,'segments',None)} for m in obj.modifiers]})
assert points
target=sum(points,Vector())/len(points);direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));cam.location=target-direction*4;cam.data.ortho_scale=.65
sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.cycles.device='CPU';sc.cycles.samples=48;sc.render.filepath=str(out/'masonry-closeup.png');assert not Path(sc.render.filepath).exists()
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'source':'CrownTopFaces_v43/crown-top-face-clay.blend','parts':parts,'cameraTarget':list(target),'orthoScale':.65,'fidelity':'VISUAL_REVIEW_REQUIRED'},indent=2));print(json.dumps(parts))
