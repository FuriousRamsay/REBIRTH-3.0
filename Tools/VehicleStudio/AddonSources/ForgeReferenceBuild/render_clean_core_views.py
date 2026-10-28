import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CleanCoreMultiView_v56';out.mkdir(parents=True,exist_ok=True)
fit='--fit-all' in sys.argv
if fit:out=root/'CleanCoreMultiViewFitted_v57';out.mkdir(parents=True,exist_ok=True)
source='CleanMortarCore_v55/clean-core-clay.blend'
if '--local-contacts' in sys.argv:
    source='LocalCrownContacts_v59/crown-top-face-clay.blend';fit=True;out=root/'LocalCrownMultiView_v60';out.mkdir(parents=True,exist_ok=True)
stoneTails='--stone-tails' in sys.argv
if stoneTails:
    source='TransitionStoneTails_v63/stone-tails-clay.blend';fit=True;out=root/'TransitionStoneReview_v64';out.mkdir(parents=True,exist_ok=True)
if '--radial-profiles' in sys.argv:
    source='RadialStoneProfiles_v65/radial-profiles-clay.blend';fit=True;stoneTails=True;out=root/'RadialStoneReview_v66';out.mkdir(parents=True,exist_ok=True)
if '--wall-facets' in sys.argv:
    source='WallStoneFacets_v68/wall-facets-clay.blend';fit=True;stoneTails=True;out=root/'WallStoneReview_v69';out.mkdir(parents=True,exist_ok=True)
current='--current-all-angles' in sys.argv
if current:
    source='ClippedStoneVolumes_v81/closed-patches-clay.blend';fit=True;stoneTails=False;out=root/'CurrentAllAngles_v83';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/source));sc=bpy.context.scene;cam=sc.camera;target=Vector((.03,.38,.46))
for obj in list(sc.objects):
    if obj.type=='LIGHT':bpy.data.objects.remove(obj,do_unlink=True)
for angle in (45,135,225,315):
    a=math.radians(angle);data=bpy.data.lights.new(f'Neutral view light{angle}','AREA');data.energy=180;data.size=2
    obj=bpy.data.objects.new(data.name,data);sc.collection.objects.link(obj);obj.location=target+Vector((math.cos(a)*2,math.sin(a)*2,2));obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.render.resolution_percentage=100;cam.data.type='ORTHO';cam.data.ortho_scale=1.85;views=[]
points=[obj.matrix_world@Vector(p) for obj in sc.objects if obj.type in ('MESH','CURVE') and not obj.hide_render and obj.name!='Inspection ground' for p in obj.bound_box]
def frame():
    if not fit:return
    q=cam.rotation_euler.to_quaternion();right=q@Vector((1,0,0));up=q@Vector((0,1,0));xs=[(p-target).dot(right) for p in points];ys=[(p-target).dot(up) for p in points]
    cam.location+=right*((min(xs)+max(xs))/2)+up*((min(ys)+max(ys))/2)
    cam.data.ortho_scale=max(max(xs)-min(xs),(max(ys)-min(ys))*1200/900)*1.13
for angle in (range(0,360,45) if current else ((135,225) if stoneTails else (45,135,225,315))):
    a=math.radians(angle);cam.location=target+Vector((math.cos(a)*3,math.sin(a)*3,1.6));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();frame();sc.render.filepath=str(out/f'clay-angle-{angle}.png');assert not Path(sc.render.filepath).exists();bpy.ops.render.render(write_still=True);views.append({'angle':angle,'image':sc.render.filepath,'orthoScale':cam.data.ortho_scale})
if not stoneTails:
    cam.location=target+Vector((.001,-.001,3));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();frame();sc.render.filepath=str(out/'clay-top.png');assert not Path(sc.render.filepath).exists();bpy.ops.render.render(write_still=True);views.append({'angle':'top','image':sc.render.filepath,'orthoScale':cam.data.ortho_scale})
(out/'receipt.json').write_text(json.dumps({'source':source,'views':views,'resolution':[1200,900],'lighting':'Four neutral area lights,180 W each,2 m size','fidelity':'VISUAL_REVIEW_REQUIRED','limits':'Additional views inspect inferred hidden geometry, not proof of reference-accurate hidden detail.'},indent=2))
