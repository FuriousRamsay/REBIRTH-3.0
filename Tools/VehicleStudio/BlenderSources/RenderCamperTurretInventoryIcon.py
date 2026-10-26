import bpy,math
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
source=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output/RustyRobotTurret_Optimized.blend')
bpy.ops.wm.open_mainfile(filepath=str(source));sc=bpy.context.scene
visible=[o for o in sc.objects if o.type=='MESH' and not o.hide_render and not o.hide_get()]
points=[o.matrix_world@Vector(v) for o in visible for v in o.bound_box]
center=(Vector(tuple(min(p[i] for p in points) for i in range(3)))+Vector(tuple(max(p[i] for p in points) for i in range(3))))*.5
cam=sc.camera;cam.data.type='ORTHO';cam.location=center+Vector((-2,-3,1.5));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.5
sc.render.resolution_x=sc.render.resolution_y=512;sc.render.resolution_percentage=100
bpy.context.view_layer.update()
# Frame the projected mesh, rather than using a bounding-sphere radius that wastes most of the icon.
projected=[world_to_camera_view(sc,cam,p) for p in points]
extent=max(max(v.x for v in projected)-min(v.x for v in projected),max(v.y for v in projected)-min(v.y for v in projected))
cam.data.ortho_scale*=extent/.90
mid=Vector(((max(v.x for v in projected)+min(v.x for v in projected))*.5-.5,(max(v.y for v in projected)+min(v.y for v in projected))*.5-.5,0))
cam.location+=cam.rotation_euler.to_matrix()@Vector((mid.x*1.5,mid.y*1.5,0))
sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=4
sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA'
sc.render.filepath='E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RelayPlacement_20261020/camper-turret-framed-icon.png'
bpy.ops.render.render(write_still=True)
print('Rendered actual optimized model with 90 percent projected icon framing')
