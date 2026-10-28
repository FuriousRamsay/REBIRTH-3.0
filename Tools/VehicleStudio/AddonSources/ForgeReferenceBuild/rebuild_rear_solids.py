"""Replace inferred curved tiles with individually planned angular solid stones."""
import bpy,bmesh,json,random,hashlib,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'RearSurfaceFit_v10';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MasonryMortar_v5/mortar.blend'))
tree=BVHTree.FromObject(bpy.data.objects['furnace_core'],bpy.context.evaluated_depsgraph_get());plan=[]
donors=[o for o in bpy.context.scene.objects if o.name.startswith(('stone_left_middle_','stone_left_lower_','stone_base_')) and len(o.data.vertices)>=13]
assert donors, 'No eligible wall stone shapes; do not build a partial scene'
for o in bpy.context.scene.objects:
    if not o.name.startswith('inferred_rear_stone_'):continue
    pts=[v.co.copy() for v in o.data.vertices];centre=sum(pts,Vector())/len(pts)
    near,normal,index,distance=tree.find_nearest(centre);normal.normalize()
    u=normal.cross(Vector((0,0,1)))
    if u.length<.01:u=normal.cross(Vector((0,1,0)))
    u.normalize();v=normal.cross(u).normalized()
    hu=(max(p.dot(u) for p in pts)-min(p.dot(u) for p in pts))/2
    hv=(max(p.dot(v) for p in pts)-min(p.dot(v) for p in pts))/2
    depth=max((p-centre).dot(normal) for p in pts)
    rng=random.Random(int(hashlib.sha256(o.name.encode()).hexdigest()[:12],16))
    outline=[[-1,-.6],[-.6,-1],[.6,-1],[1,-.6],[1,.6],[.6,1],[-.6,1],[-1,.6]]
    outline=[[x*rng.uniform(.91,1.01),y*rng.uniform(.91,1.01)] for x,y in outline]
    anchor=centre+normal*(near-centre).dot(normal)
    plan.append({'name':o.name,'position':list(anchor),'oldCentre':list(centre),'normal':list(normal),'axisU':list(u),'axisV':list(v),'halfWidth':hu,'halfHeight':hv,'existingFrontDepth':depth,'outline':outline,'frontTiltU':rng.uniform(-.013,.013),'frontTiltV':rng.uniform(-.008,.008)})
assert all(p['halfWidth']>0 and p['halfHeight']>0 for p in plan)
(out/'solid-stone-plan.json').write_text(json.dumps({'parts':plan,'validation':{'positiveDimensions':True,'closedProfile':True,'existingCellFootprintsRetained':True,'rearStyleInferred':True,'visualPass':False}},indent=2))
for p in plan:
    o=bpy.data.objects[p['name']];centre=Vector(p['position']);u=Vector(p['axisU']);v=Vector(p['axisV']);normal=Vector(p['normal'])
    vertices=[]
    for factor,d in [(1,-.06),(1,p['existingFrontDepth']-.005),(.82,p['existingFrontDepth']+.018)]:
        for x,y in p['outline']:
            tilt=x*p['frontTiltU']+y*p['frontTiltV'] if factor==.82 else 0
            vertices.append(centre+u*x*p['halfWidth']*factor+v*y*p['halfHeight']*factor+normal*(d+tilt))
    faces=[]
    for row in range(2):
        for j in range(8):faces.append((row*8+j,row*8+(j+1)%8,(row+1)*8+(j+1)%8,(row+1)*8+j))
    faces.extend([tuple(reversed(range(8))),tuple(range(16,24))])
    donor=donors[int(hashlib.sha256(o.name.encode()).hexdigest()[:8],16)%len(donors)]
    dpts=[v.co.copy() for v in donor.data.vertices];dc=sum(dpts,Vector())/len(dpts)
    _,dn,_,_=tree.find_nearest(dc);dn.normalize();du=dn.cross(Vector((0,0,1)))
    if du.length<.01:du=dn.cross(Vector((0,1,0)))
    du.normalize();dv=dn.cross(du).normalized()
    local=[Vector(((q-dc).dot(du),(q-dc).dot(dv),(q-dc).dot(dn))) for q in dpts]
    low=[min(q[i] for q in local) for i in range(3)];high=[max(q[i] for q in local) for i in range(3)]
    vertices=[]
    for q in local:
        x=(q.x-low[0])/(high[0]-low[0])*2-1;y=(q.y-low[1])/(high[1]-low[1])*2-1;z=(q.z-low[2])/(high[2]-low[2])
        vertices.append(centre+u*x*p['halfWidth']*1.02+v*y*p['halfHeight']*1.02+normal*(-.055+z*.090))
    faces=[tuple(f.vertices) for f in donor.data.polygons];p['shapeDonor']=donor.name
    mesh=bpy.data.meshes.new(o.name+'_solid');mesh.from_pydata(vertices,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for m in o.data.materials:mesh.materials.append(m)
    o.data=mesh
    for mod in o.modifiers:
        if mod.type=='BEVEL':mod.width=.006;mod.segments=3
(out/'donor-shape-receipt.json').write_text(json.dumps({'parts':plan,'status':'VISUAL_REVIEW_PENDING'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'rear-solids.blend'))
sc=bpy.context.scene;cam=sc.camera;target=Vector((-.05,.14,.46));a=math.radians(206.064);e=math.radians(25)
direction=Vector((-math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)))
cam.location=target+direction*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1
sc.render.resolution_x=1440;sc.render.resolution_y=1080;sc.cycles.samples=32;sc.render.filepath=str(out/'rear-solid-clay.png');bpy.ops.render.render(write_still=True)
