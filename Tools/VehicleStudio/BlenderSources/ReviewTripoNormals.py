import bpy
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TripoTurret_20261018');O=P/'Output'
bpy.ops.wm.open_mainfile(filepath=str(O/'RustyCombatTurret_Optimized.blend'))
orig=next(p for p in (P/'Input').rglob('*.PNG') if 'normal' in p.name)
im=bpy.data.images.load(str(orig),check_existing=False);im.colorspace_settings.name='Non-Color';im.scale(1024,1024);im.filepath_raw=str(O/'Turret_Normal_Resized.png');im.file_format='PNG';im.save()
mat=bpy.data.materials['Turret_Optimized']
for n in mat.node_tree.nodes:
 if n.type=='TEX_IMAGE' and n.image and n.image.name=='Turret_Normal':n.image=im
source=bpy.data.objects['Turret_Source_287191'];points=[source.matrix_world@Vector(c) for c in source.bound_box];center=sum(points,Vector())/8
sc=bpy.context.scene;sc.camera.location=center+Vector((-1.5,-2,1.1));sc.camera.rotation_euler=(center-sc.camera.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(O/'optimized-original-normal.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'normal-review.blend'))
