"""Render actual serialized camper support/roof geometry without a Unity license.

Only Unity text Mesh assets and built-in cube/cylinder geometry are supported.
Unsupported referenced meshes are recorded, never silently substituted.
"""
import bpy, re, struct, math, json
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

PROJECT=Path('E:/_Haven/shared/REBIRTHVehicleStudio_Camper')
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
PREFAB=PROJECT/'Assets/REBIRTHCamperTurrets_20261019/rebirthCamper80s.prefab'
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
def mesh_asset(guid):
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
    if any(n.startswith('Optimized Tripo turret') for n in ancestry):continue
    if not in_mount and not any(n in ('Camp roof','Fitted rooftop guardrails','Extended rear side guardrails','REBIRTHRearLadders') for n in ancestry):continue
    ms=value(s,'m_Mesh');file_id=int(re.search(r'fileID: (\d+)',ms).group(1));guid=re.search(r'guid: (\w+)',ms).group(1)
    try:
        if guid.startswith('00000000'):
            if file_id==10202:bpy.ops.mesh.primitive_cube_add(size=1)
            elif file_id==10206:
                bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.5,depth=2)
                bpy.context.object.data.transform(Matrix.Rotation(-math.pi/2,4,'X'))
            else:raise ValueError('unsupported builtin '+str(file_id))
            o=bpy.context.object;o.name=name
        else:
            o=bpy.data.objects.new(name,mesh_asset(guid).copy());bpy.context.scene.collection.objects.link(o)
        o.matrix_world=C@world(tr);o.data.materials.clear();o.data.materials.append(support if in_mount else clay)
        objects.append(o)
        if in_mount:mounts.append(o)
        if name=='Circular fixed turret platform':platforms.append(o)
    except Exception as e:skipped.append({'name':name,'error':str(e)})
report={'prefab':str(PREFAB),'loaded_meshes':len(objects),'mount_meshes':len(mounts),'skipped':skipped,
        'description':'Current serialized supports, roof, rails and ladders. Turrets hidden to expose platform footprints. No geometry changes.'}
P.joinpath('support-overhead-report.json').write_text(json.dumps(report,indent=2))
if len(platforms)!=4:raise RuntimeError('Expected four mount platforms')
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=32;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=8;sc.render.resolution_x=1600;sc.render.resolution_y=1600;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.065,.08,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.6
target=Vector((0,1,2.95))
for name,pos,power,size in [('Key',(-3,-2,8),700,5),('Fill',(3,5,6),500,5)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=pos;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('Support overhead',bpy.data.cameras.new('Support overhead'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=5.7
cam.location=(0,1,10);cam.rotation_euler=(0,0,0)
sc.render.filepath=str(P/'supports-overhead.png');bpy.ops.render.render(write_still=True)
cam.data.ortho_scale=1.35;cam.location=(-1.05,2.98,10)
sc.render.filepath=str(P/'support-rear-left-overhead.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'CamperSupport_Overhead.blend'))
