"""Regenerate only armor LODs, decimating each original damage owner separately.
The existing event-driven CamperProtection runtime can then use the same markers
on every LOD. No triangle collapse can cross a disappearing-piece boundary.
"""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import barycentric_transform
from mathutils.bvhtree import BVHTree
OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
DEST=OUT/'CamperStationLods';ownership_raw=(OUT/'camper-armor-lod-ranges.json').read_bytes();ownership=json.loads(ownership_raw)
raw=(OUT/'camper-lod-input.json').read_bytes();inputs={m['key']:m for m in json.loads(raw)['meshes']}
manifest_path=DEST/'manifest.json';manifest=json.loads(manifest_path.read_text());entries={e['key']:e for e in manifest['entries']}
assert ownership['inputSha256']==manifest['inputSha256']==hashlib.sha256(raw).hexdigest(),'Current geometry/ownership input required'
assert set(entries)==set(inputs),'Complete general LOD mesh set required'
armor_keys={e['key'] for e in ownership['entries']}
for key,entry in entries.items():
    if key not in armor_keys and not entry['smallDetail']:
        assert len(entry['levels'])==2 and all((DEST/l['file']).is_file() for l in entry['levels']),'Unfinished non-armor LOD entry'
manifest['complete']=False
def save():manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
save()
def vec(v,keys='xyz'):return tuple(v[k] for k in keys)
def xyz(v):return {k:float(v[i]) for i,k in enumerate('xyz')}
for owner in ownership['entries']:
    entry=entries[owner['key']];data=inputs[owner['key']]
    entry['armorSurfaceName']=owner['name'];entry['armorAware']=True
    if entry['smallDetail']:continue
    assert len(data['submeshes'])==1,'Native armor damage ranges require one submesh'
    original_indices=data['submeshes'][0]['triangles'];entry['levels']=[]
    for level,ratio in ((1,.5),(2,.2)):
        result={'vertices':[],'normals':[],'uv':[],'uv2':[],'colors':[],'submeshes':[{'triangles':[]}],'armorRanges':[]};errors=[]
        for owned in owner['ranges']:
            indices=original_indices[owned['start']:owned['start']+owned['count']]
            used=sorted(set(indices));remap={old:new for new,old in enumerate(used)}
            points=[Vector(vec(data['vertices'][old])) for old in used]
            normals=[Vector(vec(data['normals'][old])) for old in used]
            faces=[tuple(remap[x] for x in indices[i:i+3]) for i in range(0,len(indices),3)]
            valid_faces=[f for f in faces if (points[f[1]]-points[f[0]]).cross(points[f[2]]-points[f[0]]).length_squared>1e-20]
            assert valid_faces,'Original damage owner has no non-degenerate surface'
            tree=BVHTree.FromPolygons(points,valid_faces,all_triangles=True)
            mesh=bpy.data.meshes.new('ArmorOwner');mesh.from_pydata(points,[],faces);mesh.update()
            obj=bpy.data.objects.new('ArmorOwner',mesh);bpy.context.scene.collection.objects.link(obj)
            for p in mesh.polygons:p.use_smooth=True
            for channel in ('uv','uv2'):
                if len(data[channel])!=len(data['vertices']):continue
                uv=mesh.uv_layers.new(name=channel)
                for loop in mesh.loops:uv.data[loop.index].uv=vec(data[channel][used[loop.vertex_index]],'xy')
            if len(data['colors'])==len(data['vertices']):
                color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
                for loop in mesh.loops:color.data[loop.index].color=vec(data['colors'][used[loop.vertex_index]],'rgba')
            bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
            if len(faces)>12:
                dec=obj.modifiers.new('Independent damage piece reduction','DECIMATE');dec.ratio=ratio;dec.use_collapse_triangulate=True
                bpy.ops.object.modifier_apply(modifier=dec.name)
            m=obj.data;m.calc_loop_triangles();assert len(m.loop_triangles)>0,'Damage piece vanished during reduction'
            start=len(result['submeshes'][0]['triangles'])
            for tri in m.loop_triangles:
                centre=sum((m.vertices[v].co for v in tri.vertices),Vector())/3
                for li in tri.loops:
                    loop=m.loops[li];point=m.vertices[loop.vertex_index].co
                    location,normal,face_index,_=tree.find_nearest(point.lerp(centre,1e-5))
                    if face_index is not None:
                        a,b,c=valid_faces[face_index];projected=barycentric_transform(location,points[a],points[b],points[c],normals[a],normals[b],normals[c])
                        if projected.length_squared>1e-12 and all(math.isfinite(x) for x in projected):normal=projected
                        else:
                            valid=[normals[v] for v in (a,b,c) if normals[v].length_squared>1e-12 and all(math.isfinite(x) for x in normals[v])]
                            normal=valid[0] if valid else normal
                            entry['normalProjectionFallbacks']=entry.get('normalProjectionFallbacks',0)+1
                    assert normal is not None and normal.length_squared>1e-12 and all(math.isfinite(x) for x in normal),'Original armor normal projection failed: '+data['key']+' owner='+str(owned['order'])
                    index=len(result['vertices']);result['vertices'].append(xyz(point));result['normals'].append(xyz(normal.normalized()))
                    for channel in ('uv','uv2'):
                        if channel in m.uv_layers:
                            value=m.uv_layers[channel].data[li].uv;result[channel].append({'x':float(value.x),'y':float(value.y)})
                    if m.color_attributes.get('Color'):
                        value=m.color_attributes['Color'].data[li].color;result['colors'].append({k:float(value[i]) for i,k in enumerate('rgba')})
                    result['submeshes'][0]['triangles'].append(index)
            count=len(result['submeshes'][0]['triangles'])-start
            result['armorRanges'].append({'order':owned['order'],'threshold':owned['threshold'],'start':start,'count':count})
            reduced_tree=BVHTree.FromPolygons([v.co for v in m.vertices],[tuple(t.vertices) for t in m.loop_triangles],all_triangles=True)
            errors.extend(reduced_tree.find_nearest(p)[3] for p in points[::max(1,len(points)//1000)])
            bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(m)
        file=data['key']+'_LOD'+str(level)+'.json';(DEST/file).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
        errors.sort();entry['levels'].append({'level':level,'file':file,'triangles':len(result['submeshes'][0]['triangles'])//3,'distanceP95':errors[int(.95*(len(errors)-1))],'distanceMax':max(errors)})
        print('[ArmorLOD]',data['key'],level,'ranges',len(result['armorRanges']),'triangles',entry['levels'][-1]['triangles'],flush=True)
    entry['normalTransfer']='Original normals projected within each original damage owner';save()
manifest['armorInputSha256']=hashlib.sha256(ownership_raw).hexdigest();manifest['armorPrefabSha256']=ownership['prefabSha256'];manifest['complete']=True;save()
