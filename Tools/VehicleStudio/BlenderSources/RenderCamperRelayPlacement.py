"""Render actual serialized camper support/roof geometry without a Unity license.

Only Unity text Mesh assets and built-in cube/cylinder geometry are supported.
Unsupported referenced meshes are recorded, never silently substituted.
"""
import bpy, re, struct, math, json
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

PROJECT=Path('E:/_Haven/shared/REBIRTHVehicleStudio_Camper')
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RelayPlacement_20261020');P.mkdir(parents=True,exist_ok=True)
PREFAB=PROJECT/'Assets/REBIRTHCamperTurretProduction_20261020/WithLods/rebirthCamper80s.prefab'
bpy.ops.wm.read_factory_settings(use_empty=True)
raw=PREFAB.read_text(encoding='utf-8-sig')
docs={int(i):(int(t),s) for t,i,s in re.findall(r'--- !u!(\d+) &(\d+)\s*\n(.*?)(?=\n--- !u!|\Z)',raw,re.S)}
def value(s,k):
    m=re.search(r'^  '+re.escape(k)+r':\s*([^\r\n]+)',s,re.M)
    return m.group(1).strip() if m else None
def ref(s,k):return int(re.search(r'fileID:\s*(-?\d+)',value(s,k)).group(1))
def xyz(s,k):return {key:float(v) for key,v in re.findall(r'(\w+):\s*([-+\deE.]+)',value(s,k))}
names={i:value(s,'m_Name') for i,(t,s) in docs.items() if t==1}
transforms={};gotrans={}
for i,(t,s) in docs.items():
    if t!=4:continue
    go=ref(s,'m_GameObject');p=xyz(s,'m_LocalPosition');q=xyz(s,'m_LocalRotation');z=xyz(s,'m_LocalScale')
    local=Matrix.LocRotScale(Vector([p[a] for a in 'xyz']),Quaternion([q[a] for a in 'wxyz']),Vector([z[a] for a in 'xyz']))
    transforms[i]={'go':go,'parent':ref(s,'m_Father'),'local':local};gotrans[go]=i
worlds={}
def world(i):
    if not i:return Matrix.Identity(4)
    if i not in worlds:worlds[i]=world(transforms[i]['parent'])@transforms[i]['local']
    return worlds[i]
def chain(i):
    result=[]
    while i:
        result.append(names[transforms[i]['go']]);i=transforms[i]['parent']
    return result
guids={}
for f in (PROJECT/'Assets').rglob('*.asset.meta'):
    m=re.search(r'^guid: ([a-f0-9]+)',f.read_text(errors='ignore'),re.M)
    if m:guids[m.group(1)]=Path(str(f)[:-5])
cache={};skipped=[]
exports=json.loads(Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output/camper-lod-input.json').read_text())['meshes']
exported={(m['guid'],m['localId']):m for m in exports}

def mesh_asset(guid,file_id):
    key=(guid,file_id)
    if key in exported:
        if key in cache:return cache[key]
        data=exported[key];vertices=[tuple(v[a] for a in 'xyz') for v in data['vertices']];faces=[]
        for sub in data['submeshes']:
            ix=sub['triangles'];faces.extend((ix[i],ix[i+2],ix[i+1]) for i in range(0,len(ix),3))
        mesh=bpy.data.meshes.new(data['name']);mesh.from_pydata(vertices,[],faces);mesh.update();cache[key]=mesh;return mesh

    if guid in cache:return cache[guid]
    path=guids.get(guid)
    if not path:raise ValueError('non-text mesh '+guid)
    s=path.read_text();vcount=int(re.search(r'm_VertexCount: (\d+)',s).group(1))
    channels=[tuple(map(int,m)) for m in re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)',s)]
    sizes=[4,2,1,1,2,2,1,1,2,2,4,4]
    stride=max(offset+sizes[fmt]*dim for stream,offset,fmt,dim in channels if stream==0 and dim)
    stream,offset,fmt,dim=channels[0]
    if stream!=0 or fmt!=0 or dim!=3:raise ValueError('unsupported position format '+str(path))
    data=bytes.fromhex(re.search(r'_typelessdata: ([a-fA-F0-9]+)',s).group(1))
    vertices=[struct.unpack_from('<fff',data,i*stride+offset) for i in range(vcount)]
    ib=bytes.fromhex(re.search(r'm_IndexBuffer: ([a-fA-F0-9]+)',s).group(1))
    fmt='I' if int(re.search(r'm_IndexFormat: (\d+)',s).group(1)) else 'H';step=struct.calcsize(fmt)
    indices=struct.unpack('<'+fmt*(len(ib)//step),ib);faces=[]
    for first,count,base in re.findall(r'firstByte: (\d+)\s+indexCount: (\d+)\s+topology: 0\s+baseVertex: (\d+)',s):
        start=int(first)//step;end=start+int(count);base=int(base)
        faces.extend(tuple(base+indices[i+j] for j in (0,2,1)) for i in range(start,end,3))
    mesh=bpy.data.meshes.new(path.stem);mesh.from_pydata(vertices,[],faces);mesh.update();cache[guid]=mesh
    return mesh
def material(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.65
    return m
clay=material('Camper neutral clay',(.36,.39,.43));support=material('Mounts inspection clay',(.64,.47,.24))
C=Matrix.Rotation(math.pi/2,4,'X')
objects=[];mounts=[];platforms=[]
for i,(t,s) in docs.items():
    if t!=33:continue
    go=ref(s,'m_GameObject');tr=gotrans.get(go)
    if not tr:continue
    ancestry=chain(tr);name=names[go]
    in_mount=any(n.startswith('REBIRTHCamperTurretSlot') for n in ancestry)
    if any('_LOD1' in n or '_LOD2' in n for n in ancestry):continue
    cursor=tr;active=True
    while cursor:
        go_doc=docs[transforms[cursor]['go']][1]
        if value(go_doc,'m_IsActive')=='0':active=False;break
        cursor=transforms[cursor]['parent']
    if not active:continue
    if any(n.startswith('Camp furniture') for n in ancestry):continue
    if 'shadow_plane' in name:continue
    ms=value(s,'m_Mesh')
    if not ms or not re.search(r'guid: (\w+)',ms):continue
    file_id=int(re.search(r'fileID: (-?\d+)',ms).group(1));guid=re.search(r'guid: (\w+)',ms).group(1)
    try:
        if guid.startswith('00000000'):
            if file_id==10202:bpy.ops.mesh.primitive_cube_add(size=1)
            elif file_id==10206:
                bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.5,depth=2)
                bpy.context.object.data.transform(Matrix.Rotation(-math.pi/2,4,'X'))
            else:raise ValueError('unsupported builtin '+str(file_id))
            o=bpy.context.object;o.name=name
        else:
            o=bpy.data.objects.new(name,mesh_asset(guid,file_id).copy());bpy.context.scene.collection.objects.link(o)
        o.matrix_world=C@world(tr);o.data.materials.clear();o.data.materials.append(clay)
        objects.append(o)
        if in_mount:mounts.append(o)
        if name=='Circular fixed turret platform':platforms.append(o)
    except Exception as e:skipped.append({'name':name,'error':str(e)})

# Use actual authored battery target, not a guessed generic camper outline.
battery_go=next(go for go,n in names.items() if n=='REBIRTHInteraction_battery_bank')
battery_tr=gotrans[battery_go]
battery_doc=next(s for typ,s in docs.values() if typ==65 and ref(s,'m_GameObject')==battery_go)
bc=xyz(battery_doc,'m_Center');bs=xyz(battery_doc,'m_Size')
battery_unity=world(battery_tr)@Vector([bc[a] for a in 'xyz'])
# Locate the relay a small gap above the actual bank top, centered longitudinally.
# In clay, a shallow stripe guide shows the intended band centre placement.
relay_unity=Vector((abs(battery_unity.x)+.04,battery_unity.y+bs['y']*.5+.12,battery_unity.z))
relay_mat=material('Relay placement light clay',(.70,.72,.75))
def box(name,location,scale):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location);o=bpy.context.object;o.name=name;o.scale=scale;o.data.materials.append(relay_mat)
    bevel=o.modifiers.new('Soft machined edges','BEVEL');bevel.width=.008;bevel.segments=2
    o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
native=json.loads(P.joinpath('native-relay.json').read_text())
relay_objects=[]
for side in (-1,1):
    anchor=C@Vector((side*relay_unity.x,relay_unity.y,relay_unity.z))
    # Find the actual body surface rather than leaving the relay on a proxy plane.
    origin=C@Vector((side*3,relay_unity.y,relay_unity.z));direction=C@Vector((-side,0,0))
    candidates=[]
    for obj in objects:
        inv=obj.matrix_world.inverted();hit,point,normal,index=obj.ray_cast(inv@origin,(inv.to_3x3()@direction).normalized())
        if hit:
            point=obj.matrix_world@point
            if side*point.x>0:candidates.append(point)
    if candidates:anchor.x=max(side*p.x for p in candidates)*side
    rotation=Matrix.Rotation(-side*math.pi/2,4,'Z')@Matrix.Scale(1,4)
    created=[]
    for part in native['parts']:
        local=Matrix.Identity(4)
        for transform in part['chain']:local=local@Matrix.LocRotScale(Vector(transform['p']),Quaternion(transform['q']),Vector(transform['s']))
        mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(part['vertices'],[],part['faces']);mesh.update()
        obj=bpy.data.objects.new('Native relay '+part['name'],mesh);bpy.context.scene.collection.objects.link(obj)
        obj.matrix_world=C@rotation@local;obj.data.materials.append(relay_mat);created.append(obj)
    points=[o.matrix_world@Vector(v) for o in created for v in o.bound_box]
    inner=min(side*p.x for p in points);center=sum(points,Vector())/len(points)
    offset=Vector((anchor.x-side*inner+side*.002,anchor.y-center.y,anchor.z-center.z))
    for obj in created:obj.location+=offset
    relay_objects.extend(created)
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=16;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=4;sc.render.resolution_x=1500;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Clay studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.065,.075,.09,1);sc.world.node_tree.nodes['Background'].inputs[1].default_value=.7
focus=C@Vector((0,relay_unity.y-.35,relay_unity.z))
for name,pos,power,size in [('Key',(-5,-3,8),1000,5),('Fill',(5,2,7),850,5)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=pos;o.rotation_euler=(focus-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('Actual camper relay placement',bpy.data.cameras.new('Clay camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO'
for side,label in [(-1,'left')]:
    anchor=C@Vector((side*relay_unity.x,relay_unity.y,relay_unity.z))
    for view,offset,scale in [('side',Vector((side*6,0,0)),1.6),('top',Vector((side*.65,0,6)),1.6)]:
        cam.location=anchor+offset;cam.rotation_euler=(anchor-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
        sc.render.filepath=str(P/('native-size-relay-'+label+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
P.joinpath('placement.json').write_text(json.dumps({'source':str(PREFAB),'batteryUnity':list(battery_unity),'relayUnityAbsX':list(relay_unity),'loadedMeshes':len(objects),'skipped':skipped,'scope':'Exact native relay LOD0 geometry, seated on actual body ray intersection. Proposed placement render.'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'RelayPlacement.blend'))
