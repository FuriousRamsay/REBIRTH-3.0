"""Measure candidate mesh intersections over pitch and yaw; render limiting poses.

Triangle intersection is an offline geometric test, not a Unity/gameplay test.
Side-bearing contact surfaces are excluded, since they intentionally share an axle.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
bpy.ops.wm.open_mainfile(filepath=str(P/'RustyRobotTurret_Optimized.blend'))
pitch=bpy.data.objects['TurretPitch'];yaw=bpy.data.objects['TurretYaw']
body=bpy.data.objects['PitchBody_LOD0'];cradle=bpy.data.objects['YawCradle_LOD0'];fixed=bpy.data.objects['FixedSupport_LOD0']
axis=pitch.matrix_world.translation.copy()

def triangles(o,exclude_bearing=False):
    o.data.calc_loop_triangles();verts=[o.matrix_world@v.co for v in o.data.vertices];faces=[]
    for tri in o.data.loop_triangles:
        center=sum((verts[i] for i in tri.vertices),Vector())/3
        if exclude_bearing and abs(center.x-axis.x)>.270 and (center.y-axis.y)**2+(center.z-axis.z)**2<.095**2:continue
        faces.append(tuple(tri.vertices))
    return verts,faces

body_verts,body_faces=triangles(body,True)
fixed_verts,fixed_faces=triangles(fixed)
cradle_verts,cradle_faces=triangles(cradle,True)
static={'inner_ring_and_pedestal':BVHTree.FromPolygons(fixed_verts,fixed_faces,all_triangles=True),
        'side_brackets_and_outer_ring':BVHTree.FromPolygons(cradle_verts,cradle_faces,all_triangles=True)}

def overlap(deg):
    matrix=Matrix.Translation(axis)@Matrix.Rotation(math.radians(deg),4,'X')@Matrix.Translation(-axis)
    tree=BVHTree.FromPolygons([matrix@v for v in body_verts],body_faces,all_triangles=True)
    return {name:len(tree.overlap(target)) for name,target in static.items()}

report={'method':'LOD0 triangle intersections; 0.5 degree pitch samples; bearing contact within 95mm of axle excluded',
        'baseline':overlap(0),'directions':{},'status':'offline candidate; camper and gameplay checks pending'}
for name,sign in [('down',1),('up',-1)]:
    rows=[];first=None
    for i in range(1,151):
        deg=i*.5*sign;hit=overlap(deg);rows.append({'degrees':deg,**hit})
        if any(hit.values()):first=abs(deg);break
    safe=75 if first is None else max(0,first-2)
    report['directions'][name]={'first_contact_degrees':first,'provisional_limit_degrees':safe,'samples':rows}
report['yaw_pitch_spot_checks']=[]
center=yaw.matrix_world.translation.copy()
worst_contact=report['directions']['down']['first_contact_degrees'] or 75
report['down_contacts_by_yaw']=[]
for heading in range(-125,126,5):
    yawmatrix=Matrix.Translation(center)@Matrix.Rotation(math.radians(heading),4,'Z')@Matrix.Translation(-center)
    for step in range(60,77):
        elevation=step*.5
        matrix=yawmatrix@Matrix.Translation(axis)@Matrix.Rotation(math.radians(elevation),4,'X')@Matrix.Translation(-axis)
        tree=BVHTree.FromPolygons([matrix@v for v in body_verts],body_faces,all_triangles=True)
        if tree.overlap(static['inner_ring_and_pedestal']):
            report['down_contacts_by_yaw'].append({'yaw':heading,'first_contact_degrees':elevation})
            worst_contact=min(worst_contact,elevation);break
report['directions']['down']['provisional_limit_degrees']=worst_contact-2
report['all_yaw_down_first_contact']=worst_contact
angles=list(range(-75,31,3))+[report['directions']['down']['provisional_limit_degrees']]
for heading in range(-125,126,5):
    yawmatrix=Matrix.Translation(center)@Matrix.Rotation(math.radians(heading),4,'Z')@Matrix.Translation(-center)
    for elevation in angles:
        matrix=yawmatrix@Matrix.Translation(axis)@Matrix.Rotation(math.radians(elevation),4,'X')@Matrix.Translation(-axis)
        tree=BVHTree.FromPolygons([matrix@v for v in body_verts],body_faces,all_triangles=True)
        hits=len(tree.overlap(static['inner_ring_and_pedestal']))
        report['yaw_pitch_spot_checks'].append({'yaw':heading,'pitch':elevation,'intersections':hits})
report['yaw_pitch_intersection_cases']=sum(bool(r['intersections']) for r in report['yaw_pitch_spot_checks'])
P.joinpath('clearance-report.json').write_text(json.dumps(report,indent=2))

sc=bpy.context.scene;cam=sc.camera;cam.data.ortho_scale=1.45
target=Vector((0,.06,.5));cam.location=(-3,.06,.5);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
sc.cycles.samples=32
for name,deg in [('level',0),('up',-report['directions']['up']['provisional_limit_degrees']),('down',report['directions']['down']['provisional_limit_degrees'])]:
    pitch.rotation_euler.x=math.radians(deg);bpy.context.view_layer.update()
    sc.render.filepath=str(P/('clearance-'+name+'.png'));bpy.ops.render.render(write_still=True)
pitch.rotation_euler.x=0;bpy.context.view_layer.update()
# Named object actions provide reviewable motion in Blender. Gameplay drives
# the same yaw/pitch hierarchy directly after Unity import.
for frame,angle in [(1,0),(31,-125),(61,0),(91,125),(121,0)]:
    yaw.rotation_euler.z=math.radians(angle);yaw.keyframe_insert(data_path='rotation_euler',frame=frame)
yaw.animation_data.action.name='Yaw_Sweep_250_Degrees'
up=report['directions']['up']['provisional_limit_degrees'];down=report['directions']['down']['provisional_limit_degrees']
for frame,angle in [(1,0),(31,-up),(61,0),(91,down),(121,0)]:
    pitch.rotation_euler.x=math.radians(angle);pitch.keyframe_insert(data_path='rotation_euler',frame=frame)
pitch.animation_data.action.name='Whole_Body_Pitch_Measured_Limits'
sc.frame_start=1;sc.frame_end=121;sc.render.fps=30;sc.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'RustyRobotTurret_AnimatedReview.blend'))
