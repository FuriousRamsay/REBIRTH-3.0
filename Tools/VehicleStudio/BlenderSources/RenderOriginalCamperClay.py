"""Two read-only clay views of the original asset, with no authored add-ons."""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector

OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/OriginalCamperClay_20261020')
data=json.loads((OUT/'original-camper.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
def U(p): return Vector((p['x'],-p['z'],p['y']))
mat=bpy.data.materials.new('Uniform neutral clay');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.42,.42,.42,1);bs.inputs['Roughness'].default_value=.7
objects=[]
for part in data['parts']:
    verts=[U(p) for p in part['vertices']];norms=[U(n) for n in part['normals']];ix=part['triangles']
    mesh=bpy.data.meshes.new(part['path']);mesh.from_pydata(verts,[],[ix[i:i+3] for i in range(0,len(ix),3)]);mesh.update()
    for face in mesh.polygons:
        if norms and face.normal.dot(sum((norms[i] for i in face.vertices),Vector()))<0:face.flip()
        face.use_smooth=True
    mesh.update()
    if len(norms)==len(verts):mesh.normals_split_custom_set_from_vertices(norms)
    obj=bpy.data.objects.new(part['path'],mesh);bpy.context.scene.collection.objects.link(obj);mesh.materials.append(mat);objects.append(obj)

points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));center=(lo+hi)/2
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True
sc.render.resolution_x=1600;sc.render.resolution_y=1200;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG';sc.view_settings.view_transform='AgX'
sc.world=bpy.data.worlds.new('Neutral studio');sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.085,.095,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.6
cam=bpy.data.objects.new('Inspection camera',bpy.data.cameras.new('Inspection camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO'
lights=[]
for name,offset,energy,size in [('Key',(-4,-4,6),1200,5),('Fill',(4,4,4),850,5),('Rim',(-2,5,2),650,4)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=energy;ld.size=size;obj=bpy.data.objects.new(name,ld);sc.collection.objects.link(obj);lights.append(obj)
    obj.location=center+Vector(offset);obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
def render(name,offset,roll=0):
    cam.location=center+Vector(offset);q=(center-cam.location).to_track_quat('-Z','Y');cam.rotation_euler=q.to_euler();cam.rotation_euler.rotate_axis('Z',roll)
    # Fit actual mesh bounds in camera coordinates, without altering geometry.
    inv=cam.rotation_euler.to_matrix().transposed();pp=[inv@(p-center) for p in points]
    width=max(v.x for v in pp)-min(v.x for v in pp);height=max(v.y for v in pp)-min(v.y for v in pp)
    cam.data.ortho_scale=max(width,height*sc.render.resolution_x/sc.render.resolution_y)*1.14
    sc.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('original-camper-exterior',(6,7,2.2))
if '--exterior-only' not in sys.argv:
    for o in lights:o.location.z=center.z-abs(o.location.z-center.z)-2;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
    render('original-camper-bottom',(0,0,-10),math.pi/2)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'original-camper-clay.blend'))
(OUT/'render-manifest.json').write_text(json.dumps({'source':data['source'],'meshParts':len(objects),'bounds':[list(lo),list(hi)],'addedVehicleGeometry':False,'materials':'uniform neutral clay; no texture/normal maps','excluded':'source shadow plane and lower LOD duplicates only','views':['original-camper-exterior.png','original-camper-bottom.png']},indent=2))
