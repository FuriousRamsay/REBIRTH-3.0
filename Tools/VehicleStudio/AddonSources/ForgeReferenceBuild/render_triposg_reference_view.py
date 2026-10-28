"""Approximate reference-relative front angle and object framing for fair comparison."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
seed=int(sys.argv[sys.argv.index('--seed')+1]) if '--seed' in sys.argv else 42
root=Path(f'E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial/Outputs/forge-depth8-seed{seed}/ClayReview')
bpy.ops.wm.open_mainfile(filepath=str(root/'generated-clay-review.blend'))
sc=bpy.context.scene;cam=sc.camera
objects=[o for o in sc.objects if o.type=='MESH' and len(o.data.polygons)>1000]
assert len(objects)==1
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
angle=math.radians(236.7);elevation=math.radians(22.8895)
direction=Vector((math.cos(angle)*math.cos(elevation),math.sin(angle)*math.cos(elevation),math.sin(elevation)))
rotation=(-direction).to_track_quat('-Z','Y');right=rotation@Vector((1,0,0));up=rotation@Vector((0,1,0))
rx=[p.dot(right) for p in points];uy=[p.dot(up) for p in points];dz=[p.dot(direction) for p in points]
center=right*((min(rx)+max(rx))/2)+up*((min(uy)+max(uy))/2)+direction*((min(dz)+max(dz))/2)
width,height=sc.render.resolution_x,sc.render.resolution_y
targetBounds=[160,1340,65,1046];targetWidth=targetBounds[1]-targetBounds[0];targetHeight=targetBounds[3]-targetBounds[2]
scale=max((max(rx)-min(rx))/(targetWidth/width),(max(uy)-min(uy))/(targetHeight/height)*(width/height))
# Approximate image object centre; never distort the mesh to meet a pixel box.
cx=(targetBounds[0]+targetBounds[1])/2;cy=(targetBounds[2]+targetBounds[3])/2
target=center-right*((cx/width-.5)*scale)+up*((cy/height-.5)*scale*height/width)
cam.location=target+direction*5;cam.rotation_euler=rotation.to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=scale
sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.filepath=str(root/'clay-reference-angle.png')
receipt={'cameraAngleDegrees':236.7,'elevationDegrees':22.8895,'orthoScale':scale,'manualReferenceObjectBoundsPixels':targetBounds,'geometryChanged':False,'limits':'Approximate reference-relative view, not calibrated camera reconstruction or a silhouette-fidelity pass. Front normal inferred from generated quarter views.'}
(root/'reference-camera-receipt.json').write_text(json.dumps(receipt,indent=2))
bpy.ops.render.render(write_still=True);bpy.ops.wm.save_as_mainfile(filepath=str(root/'generated-reference-view.blend'))
print('ACTUAL_GENERATED_REFERENCE_VIEW_SAVED')
