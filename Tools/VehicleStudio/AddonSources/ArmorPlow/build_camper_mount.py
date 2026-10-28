"""Camper-side receiver/subframe fitted to the original front underbody.
Approved blade, ribs, teeth and braces are loaded without rebuilding them.
"""
import bpy,json,math,ast,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
O=P/'CamperMount';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'source-with-running-gear.blend'))
mat=bpy.data.materials['Untextured guard'].copy();mat.name='Mount structure inspection clay';mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.20,.20,.20,1)
collection=bpy.data.collections.new('Camper mounted plough receivers');bpy.context.scene.collection.children.link(collection)
tree=ast.parse((Path(__file__).parent/'build_plough_clay_study.py').read_text())
names={'mesh','tube','solid','bolt','cylinder','beam','side_plate'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<shared fabrication helpers>','exec'))
dg=bpy.context.evaluated_depsgraph_get();original=bpy.data.objects['parts'];base_bvh=BVHTree.FromObject(original,dg)
def source_floor(x,z):
    hit,n,face,d=base_bvh.ray_cast(Vector((x,.05,z)),Vector((0,1,0)),1.)
    assert hit is not None,'No original underbody at '+str((x,z))
    assert .30<hit.y<.65,'Underbody anchor outside expected source region'
    return hit.y
anchors=[];joints=[]
# Replace only the old vehicle-end cover/fixings hidden by the new receiver.
# Preserve the approved blade, ribs, teeth and forward part of each arm.
for obj in list(bpy.data.objects):
    if obj.type!='MESH':continue
    if obj.name.startswith('Bolted rail side reinforcing plate'):
        for v in obj.data.vertices:
            if v.co.z<2.18:v.co.z=2.18
    elif obj.name.startswith(('Separate washer','Separate hex fastener','Rail plate through-bolt')):
        c=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
        if min(abs(c.z-z) for z in [1.91,2.13])<.008 and min(abs(c.y-y) for y in [.478,.552])<.008 and .70<abs(c.x)<.96:
            bpy.data.objects.remove(obj,do_unlink=True)
# Front crossmember sits below the original bumper/undertray. Inboard rails
# keep the new structure away from the tyre envelopes.
cross=beam('Camper front mount crossmember',(-.925,.385,1.85),(.925,.385,1.85),.12,.12)
for side in [-1,1]:
    x=side*.84
    # Slip receiver has an actual through opening around the existing arm.
    verts=[]
    for z in [1.81,2.10]:
        for w,h in [(.160,.170),(.1208,.1308)]:
            for sx,sy in [(-1,-1),(1,-1),(1,1),(-1,1)]:verts.append((x+sx*w/2,.515+sy*h/2,z))
    faces=[]
    for i in range(4):
        j=(i+1)%4;faces.extend([(i,j,j+8,i+8),(i+4,i+12,j+12,j+4),(i,i+4,j+4,j),(i+8,j+8,j+12,i+12)])
    receiver=mesh('Camper arm receiver sleeve '+str(side),verts,faces,.001)
    receiver_plate=side_plate('Receiver mounting side plate '+str(side),x+side*.084,[(1.81,.435),(2.10,.435),(2.12,.46),(2.12,.57),(2.10,.595),(1.81,.595)],.008)
    # Vertical saddle overlaps sleeve bottom and crossmember top.
    riser=beam('Receiver to crossmember pedestal '+str(side),(x,.440,1.85),(x,.480,1.85),.13,.15)
    for z in [1.865,2.035]:
        p=Vector((x+side*.088,.515,z));n=Vector((side,0,0));bolt(p,n,support=receiver_plate)
        shank=cylinder('Receiver locking through-bolt',p,p-n*.192,.006)
        shank['first_component']=receiver.name
        arm=next(o for o in bpy.data.objects if o.name.startswith('Rectangular chassis mounting rail') and np.sign(sum(v.co.x for v in o.data.vertices))==side)
        shank['second_component']=arm.name
        bolt(p-n*.168,-n,support=receiver)
        joints.append(shank.name)
    # A visible diagonal gusset ties each receiver down into its crossmember.
    side_plate('Receiver crossmember gusset '+str(side),x+side*.045,[(1.79,.395),(2.05,.450),(2.05,.475),(1.79,.475)],.018)
    inner=side*.42
    floor_y=source_floor(inner,1.62)
    rail_y=min(.395,floor_y-.075)
    rail=beam('Inboard underbody mounting rail '+str(side),(inner,rail_y,1.43),(inner,rail_y,1.91),.12,.12)
    # Joined to the same crossmember, not the fender or wheel arch.
    if abs(rail_y-.395)>.001:beam('Inboard rail crossmember adapter '+str(side),(inner,rail_y,1.85),(inner,.395,1.85),.12,.12)
    for z in [1.51,1.76]:
        top=source_floor(inner,z)
        # Four independent pads per vehicle have a measured upper contact face.
        shoe=beam('Source underbody saddle '+str((side,z)),(inner,top-.045,z-.07),(inner,top-.045,z+.07),.22,.090)
        foot=beam('Saddle to mounting rail web '+str((side,z)),(inner,rail_y+.045,z),(inner,top-.045,z),.10,.075)
        # Bolt pair joins shoe to the existing source underbody. Visible lower
        # heads sit fully on the pad; upper nuts sit at measured exit surfaces.
        for dx in [-.080,.080]:
            head=Vector((inner+dx,top-.090,z));n=Vector((0,-1,0));bolt(head,n,support=shoe)
            entry,_,_,_=base_bvh.ray_cast(head+Vector((0,.001,0)),Vector((0,1,0)),.30)
            assert entry is not None,'Anchor missed source underbody'
            cylinder('Source underbody anchor shank',head,entry+Vector((0,.030,0)),.006)
            bolt(entry+Vector((0,.012,0)),(0,1,0))
            anchors.append({'head':list(head),'sourceContact':list(entry),'sourceObject':original.name,'pad':shoe.name})
    # Shape follows the reference's low, bolted bumper mounting construction.

scene=bpy.context.scene;cam=scene.camera
def render(name,eye,target,scale):
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
render('mount-side',(4,.7,2.3),(.65,.56,1.95),1.85)
# Underside fill makes the source frame and mounting bolts readable.
light=bpy.data.lights.new('Underside inspection fill','AREA');light.energy=80;light.size=3;o=bpy.data.objects.new(light.name,light);bpy.context.collection.objects.link(o);o.location=(0,-1,2);o.rotation_euler=(Vector((0,.4,1.7))-o.location).to_track_quat('-Z','Y').to_euler()
render('mount-under',(2.5,-.65,2.7),(.15,.40,1.65),2.05)
render('mount-overview',(6,3.5,7),(0,1.5,-.15),6.6)
(O/'mount-plan.json').write_text(json.dumps({'anchors':anchors,'receiverJoints':joints,'originalRunningGear':'parts mesh, unchanged','approvedPloughGeometry':'unchanged snapshot','hiddenDesign':'Authored underbody adaptation, not a claim of manufacturer attachment detail'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'camper-mounted-plough.blend'))
