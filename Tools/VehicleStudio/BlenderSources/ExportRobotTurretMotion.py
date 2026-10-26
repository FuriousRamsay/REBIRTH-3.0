"""Export an actual 3D motion preview and an FBX with the same review motion."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
bpy.ops.wm.open_mainfile(filepath=str(P/'RustyRobotTurret_AnimatedReview.blend'))
sc=bpy.context.scene;yaw=bpy.data.objects['TurretYaw'];pitch=bpy.data.objects['TurretPitch']
limits=json.loads((P/'clearance-report.json').read_text())
down=limits['directions']['down']['provisional_limit_degrees'];up=limits['directions']['up']['provisional_limit_degrees']
yaw.animation_data_clear();pitch.animation_data_clear()
for frame,heading,elevation in [(1,0,0),(11,-125,0),(31,125,0),(41,0,0),(51,0,-up),(61,0,down),(71,0,0)]:
    yaw.rotation_euler.z=math.radians(heading);yaw.keyframe_insert(data_path='rotation_euler',frame=frame)
    pitch.rotation_euler.x=math.radians(elevation);pitch.keyframe_insert(data_path='rotation_euler',frame=frame)
yaw.animation_data.action.name='TurretYawReview';pitch.animation_data.action.name='WholeBodyPitchReview'
sc.frame_start=1;sc.frame_end=71;sc.render.fps=15;sc.frame_set(1)
root=bpy.data.objects['RustyRobotTurret'];bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
for o in root.children_recursive:
    if o.type=='EMPTY' or o.type=='MESH' and '_LOD' in o.name:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(P/'RustyRobotTurret_Animated.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP')
cam=sc.camera;target=Vector((0,.07,.53));cam.location=(-2,-3,1.9);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.45
sc.render.resolution_x=640;sc.render.resolution_y=640;sc.render.resolution_percentage=100
sc.cycles.samples=12;sc.cycles.use_denoising=True;sc.render.image_settings.file_format='FFMPEG';sc.render.ffmpeg.format='MPEG4';sc.render.ffmpeg.codec='H264';sc.render.ffmpeg.constant_rate_factor='HIGH'
sc.render.filepath=str(P/'RobotTurret_40k_RotationPreview.mp4')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'RustyRobotTurret_AnimatedReview.blend'))
bpy.ops.render.render(animation=True)
