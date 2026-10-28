"""Render saved fitted masonry from the original reference camera."""
import bpy,json
from pathlib import Path
from mathutils import Vector,Matrix
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/SideMeasuredFit_v15')
bpy.ops.wm.open_mainfile(filepath=str(root/'side-fit.blend'))
camera=json.loads((root.parent.parent/'PlanReview/clay-input.json').read_text())
R,U,D=[Vector(camera[k]) for k in ('R','U','D')];target=R*((724-891)/760)+U*((935-543)/760)
sc=bpy.context.scene;cam=sc.camera;cam.location=target+D*5;cam.rotation_euler=Matrix((R,U,D)).transposed().to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1448/760
sc.render.resolution_x=1448;sc.render.resolution_y=1086;sc.cycles.samples=32;sc.render.filepath=str(root/'reference-clay.png');bpy.ops.render.render(write_still=True)
print('FITTED_REFERENCE_RENDER_SAVED')
