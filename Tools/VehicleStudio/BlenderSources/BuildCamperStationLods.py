"""Reduce uncovered camper/station meshes; original Unity LOD0 remains untouched.

Run only after ProductionStage exported a current camper-lod-input.json.
Unity coordinates are kept numerically unchanged; this job does no scene render.
"""
import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import barycentric_transform
from mathutils.bvhtree import BVHTree

OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
source=OUT/'camper-lod-input.json';raw=source.read_bytes();inputs=json.loads(raw)
destination=OUT/'CamperStationLods';destination.mkdir(exist_ok=True)
input_hash=hashlib.sha256(raw).hexdigest()
manifest_path=destination/'manifest.json'
manifest=json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
if manifest.get('inputSha256')!=input_hash:manifest={'schema':1,'inputSha256':input_hash,'entries':[]}
manifest['complete']=False
done={e['key']:e for e in manifest['entries'] if e.get('smallDetail') or len(e.get('levels',[]))==2 and all((destination/l['file']).is_file() for l in e['levels'])}
def checkpoint():
    (destination/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
checkpoint()
def vec(value,fields='xyz'):return tuple(value[key] for key in fields)
def xyz(value):return {key:float(value[i]) for i,key in enumerate('xyz')}
def uv(value):return {'x':float(value[0]),'y':float(value[1])}

for data in inputs['meshes']:
    if data['key'] in done:
        old=done[data['key']]
        assert all(old[key]==data[key] for key in ('asset','name','guid','localId')),'Resume source identity changed'
        continue
    faces=[];materials=[]
    for index,sub in enumerate(data['submeshes']):
        ts=sub['triangles']
        assert len(ts)%3==0,'Malformed source triangles'
        faces.extend(tuple(ts[i:i+3]) for i in range(0,len(ts),3));materials.extend([index]*(len(ts)//3))
    entry={key:data[key] for key in ('key','asset','name','guid','localId')}
    entry.update(sourceTriangles=len(faces),levels=[],smallDetail=len(faces)<=250)
    manifest['entries'].append(entry)
    if entry['smallDetail']:
        checkpoint();continue
    mesh=bpy.data.meshes.new(data['key']);mesh.from_pydata([vec(v) for v in data['vertices']],[],faces);mesh.update()
    original=bpy.data.objects.new(data['key'],mesh);bpy.context.scene.collection.objects.link(original)
    for index in range(len(data['submeshes'])):mesh.materials.append(bpy.data.materials.new(data['key']+'_material'+str(index)))
    for polygon,index in zip(mesh.polygons,materials):polygon.material_index=index;polygon.use_smooth=True
    source_vertices=[Vector(vec(v)) for v in data['vertices']]
    source_normals=[Vector(vec(n)) for n in data.get('normals',[])]
    source_tree=BVHTree.FromPolygons(source_vertices,faces,all_triangles=True)
    def projected_normal(point):
        location,normal,index,distance=source_tree.find_nearest(point)
        if index is not None and len(source_normals)==len(source_vertices):
            a,b,c=faces[index]
            normal=barycentric_transform(location,source_vertices[a],source_vertices[b],source_vertices[c],source_normals[a],source_normals[b],source_normals[c])
        if normal is None or normal.length_squared<1e-12 or not all(math.isfinite(x) for x in normal):normal=Vector((0,1,0))
        return normal.normalized()
    for name in ('uv','uv2'):
        if len(data.get(name,[]))!=len(mesh.vertices):continue
        layer=mesh.uv_layers.new(name=name)
        for loop in mesh.loops:layer.data[loop.index].uv=vec(data[name][loop.vertex_index],'xy')
    if len(data.get('colors',[]))==len(mesh.vertices):
        colors=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
        for loop in mesh.loops:colors.data[loop.index].color=vec(data['colors'][loop.vertex_index],'rgba')
    for level,ratio in ((1,.5),(2,.2)):
        reduced=original.copy();reduced.data=mesh.copy();bpy.context.scene.collection.objects.link(reduced)
        bpy.ops.object.select_all(action='DESELECT');reduced.select_set(True);bpy.context.view_layer.objects.active=reduced
        modifier=reduced.modifiers.new('Distance silhouette reduction','DECIMATE');modifier.ratio=ratio;modifier.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        m=reduced.data;m.calc_loop_triangles();assert len(m.loop_triangles)>0,'Reduction removed entire mesh'
        result={'vertices':[],'normals':[],'uv':[],'uv2':[],'colors':[],'submeshes':[{'triangles':[]} for _ in data['submeshes']]}
        # Split by loop so UV seams and transferred split normals survive export.
        for triangle in m.loop_triangles:
            for li in triangle.loops:
                index=len(result['vertices']);loop=m.loops[li]
                point=m.vertices[loop.vertex_index].co
                result['vertices'].append(xyz(point));result['normals'].append(xyz(projected_normal(point)))
                for channel in ('uv','uv2'):
                    if channel in m.uv_layers:result[channel].append(uv(m.uv_layers[channel].data[li].uv))
                if m.color_attributes.get('Color'):
                    color=m.color_attributes['Color'].data[li].color;result['colors'].append({key:float(color[i]) for i,key in enumerate('rgba')})
                result['submeshes'][triangle.material_index]['triangles'].append(index)
        file=data['key']+'_LOD'+str(level)+'.json';(destination/file).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
        tree=BVHTree.FromPolygons([v.co for v in m.vertices],[tuple(t.vertices) for t in m.loop_triangles],all_triangles=True)
        errors=sorted(tree.find_nearest(v.co)[3] for v in list(mesh.vertices)[::max(1,len(mesh.vertices)//1000)])
        entry['levels'].append({'level':level,'file':file,'triangles':len(m.loop_triangles),'distanceP95':errors[int(.95*(len(errors)-1))],'distanceMax':max(errors)})
        bpy.data.objects.remove(reduced,do_unlink=True);bpy.data.meshes.remove(m)
    bpy.data.objects.remove(original,do_unlink=True);bpy.data.meshes.remove(mesh)
    entry['normalTransfer']='barycentric projection from original Unity input';checkpoint();print('[CamperStationLOD]',data['key'],entry['sourceTriangles'],[r['triangles'] for r in entry['levels']],flush=True)
manifest['complete']=True;checkpoint()
