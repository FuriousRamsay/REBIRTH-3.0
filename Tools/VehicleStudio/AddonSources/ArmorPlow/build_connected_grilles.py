"""Reference grille construction fitted to source glass and metal surrounds."""
import bpy,json,math,ast,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'ConnectedGrilles';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'CamperMount/camper-mounted-plough.blend'))
for obj in list(bpy.data.objects):
    if obj.name.startswith('Guard') and obj.type in ['MESH','CURVE']:bpy.data.objects.remove(obj,do_unlink=True)
mat=bpy.data.materials['Untextured guard'];collection=bpy.data.collections.new('Reference connected window guards');bpy.context.scene.collection.children.link(collection)
for file,names in [('build_plough_clay_study.py',{'mesh','tube','bolt'}),('build_guard_clay_study.py',{'convex','fitting_corners','rounded','cut'})]:
    tree=ast.parse((Path(__file__).parent/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),file,'exec'))
dg=bpy.context.evaluated_depsgraph_get();supports={name:BVHTree.FromObject(bpy.data.objects[name],dg) for name in ['body','camper_body','parts']}
door_rows=json.loads((P/'rear-door.json').read_text())['meshes'] if (P/'rear-door.json').exists() else []
for row in door_rows:
    o=mesh(row['name'],[(v['x'],v['y'],v['z']) for v in row['vertices']],[row['triangles'][i:i+3] for i in range(0,len(row['triangles']),3)])
    # Original door geometry is context, not newly authored armor.
    collection.objects.unlink(o);bpy.context.scene.collection.objects.link(o)
    o.data.materials.clear();o.data.materials.append(bpy.data.materials['Window silhouette'] if row['name'].endswith('windows') else bpy.data.materials['Untextured source body'])
    if row['name'].endswith('camper_body'):
        bpy.context.view_layer.update();supports[row['name']]=BVHTree.FromObject(o,bpy.context.evaluated_depsgraph_get())
def support_point(p,n,max_fit=.11):
    hits=[]
    for name,bvh in supports.items():
        hit,normal,face,d=bvh.ray_cast(Vector(p)+Vector(n)*.1,-Vector(n),.40)
        if hit is not None:hits.append((d,hit,name))
    if hits:return min(hits,key=lambda h:h[0])[1:]
    # A source edge can be oblique to the glass plane: bounded closest metal
    # fitting is allowed, but never fabricate a default attachment depth.
    for name,bvh in supports.items():
        hit,normal,face,d=bvh.find_nearest(Vector(p),max_fit)
        if hit is not None:hits.append((d,hit,name))
    assert hits,'No metal surround for grille foot '+str(p)
    return min(hits,key=lambda h:h[0])[1:]
foot_report=[]
sleeve_report=[]
pipe_surfaces={}

def nearest_path(p,path):
    p=Vector(p);best=None;distance=0
    for a,b in zip(path,path[1:]):
        d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));q=a+d*t
        item=((q-p).length,distance+d.length*t,q)
        if best is None or item[0]<best[0]:best=item
        distance+=d.length
    return best

def sleeve(name,path,p,n,length=.034,radius=.029):
    # Annular sleeve follows the same polyline as the pipe. End faces are
    # rings, never disks crossing the parent pipe or chords across an elbow.
    path=[Vector(q) for q in path];n=Vector(n).normalized()
    cache_key=tuple(tuple(q) for q in path)
    if cache_key not in pipe_surfaces:
        temporary=tube('Temporary sleeve fitting surface',[tuple(q) for q in path],radius)
        bpy.context.view_layer.update();pipe_surfaces[cache_key]=BVHTree.FromObject(temporary,bpy.context.evaluated_depsgraph_get())
        bpy.data.objects.remove(temporary,do_unlink=True)
    surface=pipe_surfaces[cache_key]
    lengths=[0.]
    for a,b in zip(path,path[1:]):lengths.append(lengths[-1]+(b-a).length)
    closed=(path[0]-path[-1]).length<1e-5;s0=nearest_path(p,path)[1]
    def sample(s):
        s=s%lengths[-1] if closed else max(0,min(lengths[-1]-.000001,s))
        i=min(len(path)-2,max(0,int(np.searchsorted(lengths,s,side='right'))-1))
        direction=(path[i+1]-path[i]).normalized();f=(s-lengths[i])/(lengths[i+1]-lengths[i])
        before=(path[i]-path[i-1]).normalized() if i else ((path[-1]-path[-2]).normalized() if closed else direction)
        after=(path[i+2]-path[i+1]).normalized() if i+2<len(path) else ((path[1]-path[0]).normalized() if closed else direction)
        tangent=((before+direction).normalized()*(1-f)+(direction+after).normalized()*f).normalized()
        return path[i]+direction*(s-lengths[i]),tangent
    vs=[];steps=12;sectors=32;inner=radius+.00015;outer=radius+.0055
    for layer,r in enumerate([inner,outer]):
        for j in range(steps+1):
            q,t=sample(s0-length/2+length*j/steps);v=t.cross(n).normalized();w=v.cross(t).normalized()
            for k in range(sectors):
                angle=k*2*math.pi/sectors;radial=w*math.cos(angle)+v*math.sin(angle)
                hit,normal,_,_=surface.find_nearest(q+radius*radial)
                if normal.dot(radial)<0:normal=-normal
                vs.append(tuple(hit+normal*(r-radius)))
    count=(steps+1)*sectors;fs=[]
    for layer in range(2):
        off=layer*count
        for j in range(steps):
            for k in range(sectors):
                a=off+j*sectors+k;b=off+j*sectors+(k+1)%sectors
                face=(a,b,b+sectors,a+sectors);fs.append(face if layer else face[::-1])
    for j in [0,steps]:
        for k in range(sectors):
            a=j*sectors+k;b=j*sectors+(k+1)%sectors;fs.append((a,b,b+count,a+count))
    obj=mesh(name,vs,fs)
    for poly in obj.data.polygons:poly.use_smooth=True
    sleeve_report.append({'name':obj.name,'innerRadiusM':inner,'parentRadiusM':radius,'clearanceM':inner-radius,'annularEnds':True})
    return sample(s0)

def strap(name,points,width,thickness,width_axis):
    points=[Vector(q) for q in points];w=Vector(width_axis).normalized();vs=[]
    for i,q in enumerate(points):
        tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();h=tangent.cross(w).normalized()
        for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:vs.append(tuple(q+w*a*width/2+h*b*thickness/2))
    fs=[(3,2,1,0),tuple(range(len(vs)-4,len(vs)))]
    for i in range(len(points)-1):
        for k in range(4):fs.append((4*i+k,4*i+(k+1)%4,4*(i+1)+(k+1)%4,4*(i+1)+k))
    return mesh(name,vs,fs,.001)
def mounting_foot(p,n,right,up,name,max_fit=.11,pipe_path=None):
    p=Vector(p);n=Vector(n);right=Vector(right);up=Vector(up)
    hit,host=support_point(p,n,max_fit);outline=[]
    for x,y in [(-.035,-.028),(.035,-.028),(.035,.028),(-.035,.028)]:
        q,_=support_point(hit+right*x+up*y,n,max_fit);outline.append(tuple(q+n*.009))
    m=mesh(name+' fitted metal foot',outline,[(0,1,2,3)]);s=m.modifiers.new('Mount plate thickness','SOLIDIFY');s.thickness=.009
    # Bent flat strap ends in a saddle on the actual rounded frame path.
    # Body fastener sits on the evaluated metal foot rather than a guessed Z.
    if pipe_path:
        q,t=sleeve(name+' formed saddle',pipe_path,p,n,length=.045)
    else:q=p;t=up
    start=hit+n*.013;end=q-n*.031
    if (end-start).length>.008:
        strap(name+' bent mounting strap',[start,start+n*.012,(start+end)/2,end],.032,.006,t)
    bolt(hit+n*.009,n,scale=.60,support=m)
    foot_report.append({'assembly':name,'host':host,'contact':list(hit),'framePoint':list(p),'standOffM':(p-hit).length})

# Reconstruct measured glass polygons. Join cab quarter/main panes into the
# single outer cab-side guard shown in the reference, retaining the door rake.
rows=json.loads((P/'GuardClay/geometry-report.json').read_text());groups={}
for row in rows:
    c=np.array(row['center']);n=np.array(row['normal']);r=np.cross([0,1,0],n);r/=np.linalg.norm(r);u=np.cross(n,r)
    points=[c+r*p[0]+u*p[1] for p in row['silhouettePlane']]
    key=('CabSide'+str(int(np.sign(c[0])))) if abs(n[0])>.75 and 1.1<c[1]<1.65 and c[2]>-.29 else row['name']
    groups.setdefault(key,[]).extend(points)
for row in door_rows:
    if not row['name'].endswith('windows'):continue
    pts=np.unique(np.round([(v['x'],v['y'],v['z']) for v in row['vertices']],4),axis=0)
    groups['RearDoorGuard']=list(pts)
frames=[]
for key,pts in groups.items():
    pts=np.array(pts);c=pts.mean(0);_,_,vh=np.linalg.svd(pts-c);n=vh[-1]
    if abs(n[0])>.75:n*=np.sign(n[0]*c[0])
    else:n*=np.sign(n[2]*(c[2]+.6))
    r=np.cross([0,1,0],n);r/=np.linalg.norm(r);u=np.cross(n,r);hull=fitting_corners(convex(np.stack([(pts-c)@r,(pts-c)@u],1)))
    center=c+n*.075
    def world(p):return center+r*p[0]+u*p[1]
    if key.startswith('CabSide'):
        # Keep the original mirror and its arm. Trim the forward lower guard
        # contour above that assembly instead of running a pipe through it.
        clipped=[]
        def clearance(p):
            q=world(p);return q[1]-.85*q[2]-1.12
        for a,b in zip(hull,np.roll(hull,-1,axis=0)):
            da,db=clearance(a),clearance(b)
            if da>=0:clipped.append(a)
            if (da>=0)!=(db>=0):clipped.append(a+(b-a)*da/(da-db))
        hull=np.array(clipped)
    path=rounded(hull,.05);pipe_path=[Vector(world(p)) for p in path]+[Vector(world(path[0]))];tube('Connected '+key+' rounded frame',[tuple(p) for p in pipe_path],.029)
    for axis in [0,1]:
        lo=hull[:,axis].min();hi=hull[:,axis].max();count=max(2,round((hi-lo)/.075))
        for k in np.linspace(lo,hi,count+1)[1:-1]:
            ends=cut(hull,axis,k)
            if len(ends)==2:tube('Connected '+key+' clipped grid',[tuple(world(p)-n*.007) for p in ends],.0045)
    for i,p in enumerate(hull):
        a=hull[i-1]-p;b=hull[(i+1)%len(hull)]-p
        for d in [a,b]:
            d=d/np.linalg.norm(d);q=p+d*.080;sleeve('Connected '+key+' elbow sleeve',pipe_path,world(q),n)
        outward=p-hull.mean(0);outward/=np.linalg.norm(outward)
        q=nearest_path(world(p),pipe_path)[2]
        mounting_foot(q,n,r,u,key,pipe_path=pipe_path)
        # Straight-section collars are placed on real frame lengths, not noise.
        edge=hull[(i+1)%len(hull)]-p;length=np.linalg.norm(edge)
        for t in np.arange(.22,length-.10,.24):
            q=p+edge*t/length;sleeve('Connected '+key+' straight coupling',pipe_path,world(q),n,length=.024)
    frames.append({'id':key,'center':c.tolist(),'normal':n.tolist(),'corners':[nearest_path(world(p),pipe_path)[2][:] for p in hull],'path':[list(p) for p in pipe_path]})

rails=[]

# Reference front armor guard: one rounded outer frame, headlight grids on
# either side and a finer diamond mesh in the center. It sits behind the
# separately mod-controlled plough, leaving the mounting receivers below it.
front_hull=np.array([[-.82,.71],[.82,.71],[.82,1.085],[-.82,1.085]])
front_path=[Vector((p[0],p[1],2.105)) for p in rounded(front_hull,.055)]
front_path.append(front_path[0]);tube('Armor front guard rounded frame',[tuple(p) for p in front_path],.029)
for x in [-.38,.38]:tube('Armor front guard bay divider',[(x,.72,2.105),(x,1.075,2.105)],.022)
for lo,hi in [(-.79,-.40),(.40,.79)]:
    for x in np.arange(lo+.04,hi,.065):tube('Armor headlight guard vertical wire',[(float(x),.735,2.100),(float(x),1.06,2.100)],.004)
    for y in np.arange(.76,1.06,.065):tube('Armor headlight guard horizontal wire',[(lo,float(y),2.100),(hi,float(y),2.100)],.004)
for slope in [-1,1]:
    for intercept in np.arange(.26,1.52,.065):
        points=[]
        for x in [-.365,.365]:
            y=slope*x+intercept
            if .735<=y<=1.06:points.append((x,y,2.100))
        for y in [.735,1.06]:
            x=(y-intercept)/slope
            if -.365<=x<=.365:points.append((x,y,2.100))
        if len(points)==2:tube('Armor center diamond grille wire',points,.0035)
for x in [-.75,-.36,.36,.75]:
    for y in [.72,1.075]:
        p=Vector((x,y,2.105));sleeve('Armor front guard fitted coupling',front_path,p,(0,0,1),length=.032)
for p in [front_path[0],front_path[9],front_path[18],front_path[27]]:
    mounting_foot(p,(0,0,1),(1,0,0),(0,1,0),'ArmorFrontGuard',.20,pipe_path=front_path)
frames.append({'id':'ArmorFrontGuard','center':[0,.8975,2.105],'normal':[0,0,1],'corners':[list(p) for p in front_path[::9]],'path':[list(p) for p in front_path]})
def add_rail(name,points,radius=.029):
    smooth=[Vector(points[0])]
    for i in range(1,len(points)-1):
        p=Vector(points[i]);a=Vector(points[i-1])-p;b=Vector(points[i+1])-p
        if a.normalized().dot(b.normalized())>-.85:
            d=min(.065,a.length*.25,b.length*.25);a=p+a.normalized()*d;b=p+b.normalized()*d
            for t in np.linspace(0,1,9):smooth.append(a*(1-t)**2+p*2*t*(1-t)+b*t*t)
        else:smooth.append(p)
    smooth.append(Vector(points[-1]));tube(name,[tuple(p) for p in smooth],radius)
    for a,b in zip(smooth,smooth[1:]):rails.append((Vector(a),Vector(b),name,smooth))
front_junctions=[]
for side in [-1,1]:
    x=side*1.005;front=[]
    source_points=np.array([tuple(v.co) for v in bpy.data.objects['camper_body'].data.vertices])
    for y in np.linspace(2.96,1.74,18):
        hits=[]
        for sx in [.85,.88,.90,.92]:
            hit,_,_,_=supports['camper_body'].ray_cast(Vector((side*sx,float(y),1.50)),Vector((0,0,-1)),4)
            if hit is not None:hits.append(hit.z)
        if hits:z=max(hits)
        else:
            near=source_points[(abs(source_points[:,1]-y)<.06)&(abs(source_points[:,0])>.80)&(source_points[:,2]>0)]
            assert len(near),'No measured front edge vertices at '+str(y)
            z=float(near[:,2].max())
        front.append((x,float(y),z+.028))
    points=front+[(x,1.74,-.43),(x,1.46,-.43),(x,1.46,-3.23),(x,2.96,-3.23)]
    add_rail('Reference camper perimeter rail '+str(side),points)
    front_junctions.append(Vector(front[-1]))
    for z in [-.52,-3.16]:mounting_foot((x,1.46,z),(side,0,0),(0,0,1),(0,1,0),'Perimeter'+str(side))
    for p in [front[0],front[-1]]:mounting_foot(p,(side,0,0),(0,0,1),(0,1,0),'FrontPerimeter'+str(side),.20)
add_rail('Reference front lower transverse guard',[tuple(p) for p in front_junctions])
# Fuse the actual intersecting steel at each tee. Previously a separately
# capped cross tube ended at a guessed coordinate and left a blunt gap.
cross=bpy.data.objects['Reference front lower transverse guard']
def evaluated_mesh(obj):
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    result=mesh(obj.name+' welded mesh',[],[]);result.data=bpy.data.meshes.new_from_object(evaluated)
    bpy.data.objects.remove(obj,do_unlink=True);return result
cross=evaluated_mesh(cross)
for side in [-1,1]:
    rail=evaluated_mesh(bpy.data.objects['Reference camper perimeter rail '+str(side)])
    union=cross.modifiers.new('Continuous welded tee '+str(side),'BOOLEAN');union.operation='UNION';union.solver='EXACT';union.object=rail
    bpy.context.view_layer.objects.active=cross;bpy.ops.object.modifier_apply(modifier=union.name)
    bpy.data.objects.remove(rail,do_unlink=True)
    # Weld bead follows the branch intersection with the main curved pipe.
    junction=front_junctions[0 if side<0 else 1];ring=[]
    for angle in np.linspace(0,2*math.pi,49):
        ring.append(tuple(junction+Vector((-side*.026,.030*math.cos(angle),.030*math.sin(angle)))))
    tube('Continuous front tee weld '+str(side),ring,.0025)
for polygon in cross.data.polygons:polygon.use_smooth=True
connections=[]
# Each grille already has its own fitted vehicle mounts. The user explicitly
# rejected additional frame-to-perimeter links; do not recreate those braces.

(O/'connection-report.json').write_text(json.dumps({'frames':frames,'feet':foot_report,'sleeves':sleeve_report,'railLinks':connections,'reference':'armor-window-grilles.png','geometrySource':'original glass and metal surrounds; explicit perimeter reconstruction','status':'REVIEW_CANDIDATE_NOT_INSTALLED'},indent=2))
scene=bpy.context.scene;cam=scene.camera
for name,eye,target,scale in [('grilles-overview',(6,3.5,7),(0,1.5,-.15),6.6),('grilles-cab',(4,2.2,3),(.3,1.65,.2),2.7),('grilles-side',(5,2.5,-.8),(.4,2,-1.1),3.5)]:
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'camper-connected-grilles.blend'))

