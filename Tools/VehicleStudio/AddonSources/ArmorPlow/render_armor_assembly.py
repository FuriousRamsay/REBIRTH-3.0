"""Armor-only whole-vehicle review: approved plates and current grilles.
No bundle build, installation, original texture changes or gameplay.
"""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'ArmorAssemblyReview';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'PlateSamples/TexturedReview/textured-armor-review.blend'))
for name in ['Reference plough clay','Camper mounted plough receivers']:bpy.data.collections[name].hide_render=True
guards=bpy.data.collections['Reference connected window guards'];guards.hide_render=False
with bpy.data.libraries.load(str(P/'ReferenceFinish/reference-finish-study.blend'),link=False) as (source,target):
    target.materials=[n for n in source.materials if n in ['Reference dark oxidized grille tubes','Reference oxidized fasteners']]
loaded={m.name:m for m in target.materials if m}
for obj in guards.objects:
    if obj.type not in ['MESH','CURVE']:continue
    key='Reference oxidized fasteners' if any(k in obj.name.lower() for k in ['washer','fastener','coupling','sleeve','saddle']) else 'Reference dark oxidized grille tubes'
    material=loaded[key];obj.data.materials.clear();obj.data.materials.append(material)

# Reuse the approved panel layout on the other side. Geometry is mirrored,
# including complete hardware, with winding repaired for outward normals.
opposite=bpy.data.collections.new('Armor opposite side review');bpy.context.scene.collection.children.link(opposite)
original=bpy.data.collections['Reference traced plating samples'];mapping={}
for obj in original.objects:
    if obj.type!='MESH':continue
    other=obj.copy();other.data=obj.data.copy();other.name='Opposite '+obj.name;opposite.objects.link(other);mapping[obj.name]=other
    for vertex in other.data.vertices:vertex.co.x=-vertex.co.x
    bm=bmesh.new();bm.from_mesh(other.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(other.data);bm.free()
for obj in opposite.objects:
    if 'seat_support' in obj:obj['seat_support']=mapping[obj['seat_support']].name
bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();body=BVHTree.FromObject(bpy.data.objects['body'],dg)
skin=bpy.data.objects['body'];bm=bmesh.new();bm.from_mesh(skin.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00005);bm.normal_update()
remaining=set(bm.faces);faces=[]
while remaining:
    first=remaining.pop();island={first};pending=[first]
    while pending:
        for edge in pending.pop().edges:
            for face in edge.link_faces:
                if face in remaining:remaining.remove(face);island.add(face);pending.append(face)
    if sum(f.calc_area() for f in island)>.04:faces.extend(f for f in island if f.normal.x<-.02)
bm.verts.ensure_lookup_table();bm.verts.index_update();negative_skin=BVHTree.FromPolygons([skin.matrix_world@v.co for v in bm.verts],[[v.index for v in f.verts] for f in faces]);bm.free()
for obj in opposite.objects:
    if 'panel_outline_zy' not in obj:continue
    for vertex in obj.data.vertices:
        hit,_,_,_=negative_skin.ray_cast(Vector((-2,vertex.co.y,vertex.co.z)),Vector((1,0,0)),1.4)
        assert hit is not None,'Opposite panel outline outside source body: '+obj.name+' '+str(tuple(vertex.co))+' raw='+str(body.ray_cast(Vector((-2,vertex.co.y,vertex.co.z)),Vector((1,0,0)),1.4))
        vertex.co.x=hit.x-.006
bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
plate_surfaces={o.name:BVHTree.FromObject(o,dg) for o in opposite.objects if 'panel_outline_zy' in o}
washers=[o for o in opposite.objects if 'seat_support' in o];head_updates=[]
for washer in washers:
    old_seat=sum((v.co for v in list(washer.data.vertices)[:24]),Vector())/24
    old_top=sum((v.co for v in list(washer.data.vertices)[24:48]),Vector())/24;old_normal=(old_top-old_seat).normalized()
    target=plate_surfaces[washer['seat_support']];seat,normal,_,_=target.ray_cast(Vector((-2,old_seat.y,old_seat.z)),Vector((1,0,0)),1.5)
    assert seat is not None
    head_updates.append((old_seat.copy(),seat,old_normal.rotation_difference(normal)))
    for i in range(24):
        v=washer.data.vertices[i];hit,_,_,_=target.ray_cast(Vector((-2,v.co.y,v.co.z)),Vector((1,0,0)),1.5)
        assert hit is not None,'Opposite washer crosses plate edge'
        v.co=hit;washer.data.vertices[i+24].co=hit+normal*.0039
for obj in opposite.objects:
    if 'fastener' not in obj.name.lower():continue
    center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
    old_seat,seat,rotation=min(head_updates,key=lambda row:(center.y-row[0].y)**2+(center.z-row[0].z)**2)
    for vertex in obj.data.vertices:vertex.co=seat+rotation@(vertex.co-old_seat)
bpy.context.view_layer.update()
support=[]
for obj in opposite.objects:
    if 'panel_outline_zy' not in obj:continue
    distances=[]
    for vertex in obj.data.vertices:
        p,n,_,distance=body.ray_cast(vertex.co+Vector((.003,0,0)),Vector((1,0,0)),.008)
        assert p is not None,'Opposite side panel lacks source support: '+obj.name
        distances.append(distance)
    assert max(distances)<.004,'Opposite side does not match body: '+obj.name
    support.append({'panel':obj.name,'maximumBodyProbeM':max(distances)})
scene=bpy.context.scene;scene.cycles.samples=48;cam=scene.camera
views=[('armor-front',(0,2.05,8),(0,1.5,-.5),4.9),('armor-side-a',(8,2.0,-.55),(0,1.5,-.55),6.6),('armor-side-b',(-8,2.0,-.55),(0,1.5,-.55),6.6),('armor-rear',(0,2.05,-9),(0,1.5,-.55),4.9)]
for name,eye,target,scale in views:
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'armor-assembly-review.blend'))
(O/'review.json').write_text(json.dumps({'mode':'armor-only; plough hidden','plateLayout':'approved visible-side layout reused on opposite side','oppositeBodySupport':support,'views':[v[0] for v in views],'baseBody':'neutral clay for geometry inspection','status':'REVIEW_NOT_INSTALLED'},indent=2))

