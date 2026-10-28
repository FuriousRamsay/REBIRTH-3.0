"""Reference-derived visible plough study. Unaccepted; no bundle deployment.
Pixel landmarks are kept here for audit; hidden brackets are authored estimates.
"""
import bpy,json,math,numpy as np
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from pathlib import Path
ROOT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
OUT=ROOT/'PloughClay';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'GuardClay/guard-clay-study.blend'))
mat=bpy.data.materials.get('Untextured guard')
collection=bpy.data.collections.new('Reference plough clay');bpy.context.scene.collection.children.link(collection)
def mesh(name,verts,faces,bevel=0):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new(name,m);collection.objects.link(o);m.materials.append(mat)
    if bevel:
        b=o.modifiers.new('Fabricated edge radius','BEVEL');b.width=bevel;b.segments=2
        o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
    return o
def tube(name,points,radius):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=radius;c.bevel_resolution=3;c.use_fill_caps=True
    s=c.splines.new('POLY');s.points.add(len(points)-1)
    for q,p in zip(s.points,points):q.co=(*p,1)
    o=bpy.data.objects.new(name,c);collection.objects.link(o);c.materials.append(mat);return o
def solid(name,outline,thickness=.022):
    if (Vector(outline[1])-Vector(outline[0])).cross(Vector(outline[2])-Vector(outline[0])).z<0:outline=outline[::-1]
    o=mesh(name,outline,[tuple(range(len(outline)))],.003);s=o.modifiers.new('Steel sheet thickness','SOLIDIFY');s.thickness=thickness;return o
def bolt(p,normal=None,contact=None,scale=.80,support=None):
    normal=Vector(normal or (0,0,1)).normalized();right=Vector((1,0,0) if abs(normal.x)<.9 else (0,0,1));up=normal.cross(right).normalized();right=up.cross(normal).normalized();p=Vector(p)
    # Orient both pieces to the local plate normal. The washer backing follows
    # the curved contact surface rather than bridging it with a flat disk.
    for r,d,z in [(.020,.006,0),(.012,.014,.006)]:
        r*=scale;d*=scale;z*=scale
        n=24 if z==0 else 6;vs=[]
        for k in (0,1):
            for i in range(n):
                a=i*2*math.pi/n;q=p+right*(r*math.cos(a))+up*(r*math.sin(a))+normal*(z+k*d)
                if contact is not None and n==24:
                    q.z=contact(q.x,q.y)+normal.z*k*d
                vs.append(tuple(q))
        faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        hardware=mesh('Separate hex fastener' if n==6 else 'Separate washer',vs,faces,.0006)
        if n==24 and support is not None:hardware['seat_support']=support.name;hardware['seat_vertex_count']=n

# Homography is used only to recover relative blade-face placement from the
# photographed quadrilateral. It does not claim a calibrated 3D camera.
pixels=np.array([[103,474],[415,474],[464,691],[81,633]],float)
uv=np.array([[0,1],[1,1],[1,0],[0,0]],float);A=[];B=[]
for (x,y),(u,v) in zip(pixels,uv):
    A.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]]);B.extend([u,v])
H=np.append(np.linalg.solve(A,B),1).reshape(3,3)
def image_uv(p):
    q=H@np.array([*p,1.]);return q[:2]/q[2]
def blade(u,t,offset=0):return ((u-.5)*2.30,.35+t*.87,2.39+.30*(1-t)**2+.06*t**8+offset)
def front_z(y):
    # Match the actual segmented blade mesh, not merely its smooth formula.
    t=(y-.35)/.87;i=max(0,min(31,int(math.floor(t*32))));f=t*32-i
    return blade(0,i/32)[2]*(1-f)+blade(0,(i+1)/32)[2]*f
def surface_normal(y):
    t=(y-.35)/.87;i=max(0,min(31,int(math.floor(t*32))))
    slope=(blade(0,(i+1)/32)[2]-blade(0,i/32)[2])/(.87/32)
    return Vector((0,-slope,1)).normalized()
contact_back_vertices=[]
def fitted_skin(name,x0,x1,y0,y1,thickness,nx=4,ny=12):
    # Closed curved plate: back embeds 0.4 mm into the moldboard to avoid cracks
    # from tessellation/float roundoff; every front and back vertex is fitted.
    verts=[];faces=[];count=(nx+1)*(ny+1)
    for layer in [0,1]:
        for i in range(nx+1):
            for j in range(ny+1):
                x=x0+(x1-x0)*i/nx;y=y0+(y1-y0)*j/ny;z=front_z(y)+(thickness if layer else -.0004)
                verts.append((x,y,z))
                if not layer:contact_back_vertices.append((x,y,z))
    for i in range(nx):
        for j in range(ny):
            a=i*(ny+1)+j;b=a+ny+1
            faces.extend([(a,a+1,b+1,b),(a+count,b+count,b+count+1,a+count+1)])
    perimeter=list(range(ny+1))+[i*(ny+1)+ny for i in range(1,nx+1)]+[nx*(ny+1)+j for j in range(ny-1,-1,-1)]+[i*(ny+1) for i in range(nx-1,0,-1)]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):faces.append((a,b,b+count,a+count))
    return mesh(name,verts,faces)
nu,nt=48,32;vs=[blade(i/nu,j/nt) for i in range(nu+1) for j in range(nt+1)]
faces=[]
for i in range(nu):
    for j in range(nt):
        a=i*(nt+1)+j;faces.append((a,a+nt+1,a+nt+2,a+1))
o=mesh('Continuous curved moldboard',vs,faces);moldboard=o;s=o.modifiers.new('Moldboard sheet','SOLIDIFY');s.thickness=.022
blade_front_bvh=BVHTree.FromPolygons([Vector(p) for p in vs],faces)
for poly in o.data.polygons:poly.use_smooth=True
# Flat reinforcing strips conform to the blade; these replace the rejected rods.
for u in [.018,.23,.46,.70,.982]:
    x=blade(u,0)[0]
    strip=fitted_skin('Flat curved bolted strap',x-.025,x+.025,.35,1.22,.009,2,32)
    for t in [.16,.30,.46,.62,.79,.955]:
        y=.35+t*.87;bolt((x,y,front_z(y)+.009),surface_normal(y),lambda x,y:front_z(y)+.009,support=strip)
tube('Rolled upper edge',[blade(i/64,1,.006) for i in range(65)],.016)

# Visible tip/flange associations from the reference. Occlusion makes association
# uncertain: keep those coordinates explicit rather than inventing a repeated grid.
pairs=[((38,533),(130,519)),((61,555),(179,541)),((57,599),(135,580)),((128,577),(228,554)),((203,574),(292,563)),((264,587),(352,577)),((303,594),(405,589))]
survey=[]
for index,(tip,flange) in enumerate(pairs):
    u,t=image_uv(flange);p=np.array(blade(u,t));p[2]=front_z(p[1])+.014;length=float(np.clip(np.linalg.norm(np.array(tip)-flange)/150,.43,.78))
    fitted_skin('Surface-conforming spike flange '+str(index+1),p[0]-.075,p[0]+.075,p[1]-.082,p[1]+.082,.014)
    for x in [-.055,.055]:
        for y in [-.061,.061]:
            q=p+np.array([x,y,0]);q[2]=front_z(q[1])+.014
            bolt(q,surface_normal(q[1]),lambda x,y:front_z(y)+.014)
    profile=[(0,.052),(.04,.052),(.055,.043),(.12,.043),(.135,.050),(.155,.050),(.17,.039),(length*.55,.029),(length*.57,.035),(length*.61,.026),(length-.055,.007),(length,.001)]
    verts=[];n=20
    for ring,(z,r) in enumerate(profile):
        for i in range(n):
            a=i*2*math.pi/n;x=p[0]+r*math.cos(a);y=p[1]+r*math.sin(a)
            # Rear annulus seats into the curved flange. Next ring is forward
            # of its entire footprint so no part of the spike floats above it.
            depth=front_z(y)+.0136 if ring==0 else max(p[2]+z,front_z(y)+.018)
            verts.append((x,y,depth))
    faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]
    o=mesh('Individually placed turned spike '+str(index+1),verts,faces)
    for poly in o.data.polygons:poly.use_smooth=True
    survey.append({'id':index+1,'tipPx':tip,'flangePx':flange,'bladeUV':[u,t],'lengthEstimateM':length,'association':'HAND_TRACE_UNVERIFIED'})

def cylinder(name,a,b,r,sides=16):
    a=Vector(a);b=Vector(b);axis=(b-a).normalized();right=axis.cross(Vector((0,1,0)) if abs(axis.y)<.9 else Vector((1,0,0))).normalized();up=axis.cross(right)
    verts=[tuple(p+right*(r*math.cos(i*2*math.pi/sides))+up*(r*math.sin(i*2*math.pi/sides))) for p in [a,b] for i in range(sides)]
    faces=[tuple(range(sides-1,-1,-1)),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    return mesh(name,verts,faces,.001)
def beam(name,a,b,width,depth):
    a=Vector(a);b=Vector(b);axis=(b-a).normalized();right=axis.cross(Vector((0,1,0)) if abs(axis.y)<.9 else Vector((1,0,0))).normalized();up=axis.cross(right)
    verts=[tuple(p+right*x*width/2+up*y*depth/2) for p in [a,b] for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    return mesh(name,verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],.004)
def side_plate(name,x,outline,thickness):
    # Shaped solid cheek in the vehicle's longitudinal plane, not a blank post.
    verts=[(x+dx,y,z) for dx in [-thickness/2,thickness/2] for z,y in outline];n=len(outline)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,verts,faces,.004)

# Short replaceable teeth: broad roots overlap a continuous curved backing bar.
# Each fixing passes through tooth + bar + moldboard and carries a rear nut.
fitted_skin('Continuous lower cutting-edge backing bar',-1.15,1.15,.35,.46,.020,32,8)
bpy.context.view_layer.update();blade_solid_bvh=BVHTree.FromObject(moldboard,bpy.context.evaluated_depsgraph_get())
teeth=[];tooth_audit=[]
for i in range(8):
    cx=-1.015+i*.29;half=.112
    # Root remains curved across the overlap, then terminates in a short point.
    rows=[(.445,half),(.415,half),(.38,half*.94),(.35,half*.86),(.30,half*.66)]
    outline=[(cx-half,.445),(cx+half,.445),(cx+half*.66,.30),(cx-.018,.225),(cx-half*.66,.30)]
    verts=[]
    def tooth_z(y):return front_z(y)+.038 if y>=.35 else front_z(.35)+(.35-y)*.40+.038
    for back in [False,True]:
        for y,w in rows:
            for x in [cx-w,cx+w]:verts.append((x,y,tooth_z(y)-(.0184 if back else 0)))
        verts.append((cx-.018,.225,tooth_z(.225)-(.0184 if back else 0)))
    count=len(verts)//2;faces=[]
    for j in range(len(rows)-1):
        a=j*2;faces.extend([(a,a+2,a+3,a+1),(a+count,a+count+1,a+count+3,a+count+2)])
    a=2*(len(rows)-1);faces.extend([(a,a+2,a+1),(a+count,a+count+1,a+count+2)])
    perimeter=[0,1,3,5,7,9,10,8,6,4,2]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):faces.append((a,b,b+count,a+count))
    tooth=mesh('Short overlapping cutting tooth '+str(i+1),verts,faces,.0015)
    tooth_bvh=BVHTree.FromPolygons([Vector(p) for p in verts],faces)
    for dx in [-.051,.051]:
        y=.408;head=Vector((cx+dx,y,tooth_z(y)));normal=surface_normal(y)
        bolt(head,normal,lambda x,y:tooth_z(y))
        # Find the physical moldboard exit rather than guessing a nut offset.
        origin=head+normal*.01;entry,n,face,d=blade_solid_bvh.ray_cast(origin,-normal,1)
        assert entry is not None,'Tooth fixing missed moldboard'
        exit,n,face,d=blade_solid_bvh.ray_cast(entry-normal*.00001,-normal,1)
        assert exit is not None,'Tooth fixing has no backing exit'
        cylinder('Through-bolt shank',head-normal*.001,exit-normal*.018,.006)
        bolt(exit,-normal)
        # A real ray must hit both the tooth and its backing, not empty space.
        hit,_,_,_=tooth_bvh.ray_cast(head+normal*.002,-normal,.03)
        assert hit is not None,'Tooth bolt outside tooth'
        tooth_audit.append({'tooth':i+1,'head':list(head),'backingExit':list(exit),'throughMaterial':True})
    teeth.append({'id':i+1,'exposedLengthM':.125,'rootOverlapM':.095,'outlineXY':outline})

# Visible reference construction: stepped bolted mounting cheeks, rectangular
# lower rails, flat diagonal straps and paired pin lugs. Hidden chassis depth
# remains an estimate; do not present it as measured from the single image.
cheek=[(1.88,.44),(2.43,.44),(2.43,.59),(2.32,.59),(2.32,.72),(2.32,1.15),(2.19,1.15),(2.19,.73),(2.08,.68),(1.88,.68)]
for side in [-1,1]:
    x=side*.84
    upright=side_plate('Stepped bolted mounting cheek',x,cheek,.032)
    # Bolted joint to a distinct blade-mounted rib. Previously these fasteners
    # passed only through this upright and had no mating component.
    rib_x=x-side*.0258
    def blade_back(y):
        hit,_,_,_=blade_solid_bvh.ray_cast(Vector((rib_x,float(y),2.0)),Vector((0,0,1)),1)
        assert hit is not None,'Upper rib misses rear blade surface'
        return hit.z
    ys=np.linspace(.70,1.18,49)
    saddle_vertices=[];saddle_faces=[];nx=8;ny=len(ys)-1;count=(nx+1)*(ny+1)
    for layer in [0,1]:
        for i in range(nx+1):
            for y in ys:
                saddle_vertices.append((rib_x-.07+.14*i/nx,float(y),blade_back(y)+(.0004 if layer==0 else -.012)))
    for i in range(nx):
        for j in range(ny):
            a=i*(ny+1)+j;b=a+ny+1
            saddle_faces.extend([(a,b,b+1,a+1),(a+count,a+count+1,b+count+1,b+count)])
    rim=list(range(ny+1))+[i*(ny+1)+ny for i in range(1,nx+1)]+[nx*(ny+1)+j for j in range(ny-1,-1,-1)]+[i*(ny+1) for i in range(nx-1,0,-1)]
    for a,b in zip(rim,rim[1:]+rim[:1]):saddle_faces.append((a,b,b+count,a+count))
    saddle=mesh('Upper curved blade saddle '+str(side),saddle_vertices,saddle_faces)
    saddle['blade_contact_vertex_count']=count
    rib_outline=[(2.20,.70)]+[(blade_back(y)-.0116,float(y)) for y in ys]+[(2.20,1.18)]
    rib=side_plate('Curved blade rib with bolted upright tab '+str(side),rib_x,rib_outline,.0204)
    beam('Rectangular chassis mounting rail',(x,.515,1.75),(x,.515,2.43),.12,.13)
    # Flat diagonal strap and small attachment pads follow the reference's
    # angular bracket silhouette, replacing the generic round diagonal tube.
    a=np.array([2.03,.64]);b=np.array([2.255,1.015]);direction=(b-a)/np.linalg.norm(b-a);angle=math.atan2(direction[1],direction[0]);outline=[]
    for center,start in [(b,angle-math.pi/2),(a,angle+math.pi/2)]:
        for t in np.linspace(start,start+math.pi,13):outline.append(tuple(center+np.array([math.cos(t),math.sin(t)])*.035))
    strap=side_plate('Rounded end diagonal mounting strap',x+side*.025,outline,.022)
    for z,y in [(2.255,1.10),(2.255,.93),(2.255,.76)]:
        p=Vector((x+side*.016,y,z));n=Vector((side,0,0));bolt(p,n)
        shank=cylinder('Upper rib joint through-bolt',p,p-n*.070,.006)
        shank['joint_first']=upright.name;shank['joint_second']=rib.name
        bolt(p-n*.052,-n,support=rib)
    for z,y in [(2.03,.64),(2.255,1.015)]:
        p=Vector((x+side*.036,y,z));n=Vector((side,0,0));bolt(p,n,support=strap)
        cylinder('Strap attachment through-bolt',p,p-n*.070,.006);bolt(p-n*.052,-n)
    side_plate('Bolted rail side reinforcing plate',x+side*.064,[(1.82,.46),(1.85,.445),(2.39,.445),(2.41,.47),(2.41,.565),(2.39,.585),(1.85,.585),(1.82,.565)],.008)
    for z in [1.91,2.13,2.35]:
        for y in [.478,.552]:
            p=Vector((x+side*.068,y,z));n=Vector((side,0,0));bolt(p,n)
            cylinder('Rail plate through-bolt',p,p-n*.145,.006);bolt(p-n*.128,-n)
    # Double lug overlaps the blade backing; pin crosses both steel leaves.
    lug_by_side={}
    for dx in [-.046,.046]:
        edge=[]
        for y in np.linspace(.46,.64,20):
            hit,_,_,_=blade_solid_bvh.ray_cast(Vector((x+dx,float(y),2.0)),Vector((0,0,1)),1)
            assert hit is not None,'Clevis edge misses blade backing'
            edge.append((hit.z+.0004,float(y)))
        lug_by_side[1 if dx>0 else -1]=side_plate('Blade attachment clevis leaf',x+dx,[(2.33,.46)]+edge+[(2.33,.64)],.020)
    side_plate('Central chassis pivot tongue',x,[(2.38,.50),(2.51,.50),(2.535,.525),(2.535,.575),(2.51,.60),(2.38,.60)],.040)
    cylinder('Clevis pivot pin',(x-.068,.55,2.49),(x+.068,.55,2.49),.010)
    bolt((x+side*.056,.55,2.49),(side,0,0),support=lug_by_side[side])
(OUT/'tooth-attachment-audit.json').write_text(json.dumps({'status':'16_FIXINGS_INTERSECT_TOOTH_AND_MOLDBOARD','fixings':tooth_audit,'teeth':teeth},indent=2))

signed_contacts=[]
for p in contact_back_vertices:
    q,n,face,d=blade_front_bvh.find_nearest(Vector(p))
    # Only the front-facing sign is used; native mesh winding may be inverted.
    signed_contacts.append((p[2]-q.z)*abs(n.z))
assert max(signed_contacts)<.00005 and min(signed_contacts)>-.001, 'Plate backing lost contact with blade'
(OUT/'contact-audit.json').write_text(json.dumps({'sampleCount':len(signed_contacts),'signedGapMinM':min(signed_contacts),'signedGapMaxM':max(signed_contacts),'maximumPermittedSeparationM':.00005,'status':'BACKING_CONTACT_PASS_NOT_REFERENCE_FIDELITY'},indent=2))
(OUT/'trace-report.json').write_text(json.dumps({'status':'UNACCEPTED_CLAY_STUDY','spikes':survey,'shortTeeth':teeth,'unresolved':'Overall reference silhouette and camera matching still require review. No acceptance inferred from this render.'},indent=2))
scene=bpy.context.scene;scene.cycles.samples=32;scene.view_settings.exposure=-1.2;cam=scene.camera
cam.data.ortho_scale=6.6;cam.location=(6,3.7,7)
forward=(Vector((0,1.6,-.15))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward)
cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();scene.render.filepath=str(OUT/'front.png');bpy.ops.render.render(write_still=True)
cam.data.ortho_scale=2.9;cam.location=(3,1.65,6.2);forward=(Vector((0,.8,2.5))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
scene.render.filepath=str(OUT/'detail.png');bpy.ops.render.render(write_still=True)
cam.location=(3,.9,2.9);forward=(Vector((.2,.8,2.5))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=1.5
scene.render.filepath=str(OUT/'grazing-contact.png');bpy.ops.render.render(write_still=True)
hidden=[]
for obj in bpy.context.scene.objects:
    if obj.type in ['MESH','CURVE'] and collection not in obj.users_collection:
        hidden.append(obj);obj.hide_render=True
cam.location=(1.7,.55,1.8);forward=(Vector((.35,.38,2.65))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.data.ortho_scale=1.25
scene.render.filepath=str(OUT/'rear-tooth-fixings.png');bpy.ops.render.render(write_still=True)
for obj in hidden:obj.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'plough-clay-study.blend'))
