"""Separate review samples. Pixel outlines are observations; 3D registration
to this camper is an explicit adaptation, not a claim of calibrated dimensions."""
import bpy,bmesh,json,ast,math,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import tessellate_polygon
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'PlateSamples';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'ConnectedGrilles/camper-connected-grilles.blend'))
for name in ['Reference plough clay','Camper mounted plough receivers','Reference connected window guards']:bpy.data.collections[name].hide_render=True
collection=bpy.data.collections.new('Reference traced plating samples');bpy.context.scene.collection.children.link(collection)
mat=bpy.data.materials['Untextured guard']
tree=ast.parse((Path(__file__).parent/'build_plough_clay_study.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'mesh','bolt'}],type_ignores=[]),'fabrication','exec'))
dg=bpy.context.evaluated_depsgraph_get();hosts=[BVHTree.FromObject(bpy.data.objects[n],dg) for n in ['body','camper_body']]
def surface(y,z):
    hits=[]
    for b in hosts:
        p,_,_,d=b.ray_cast(Vector((2,y,z)),Vector((-1,0,0)),1.5)
        if p is not None:hits.append(p.x)
    if not hits:raise ValueError('Sample outline outside source metal '+str((y,z)))
    return max(hits)
def registration(pixel_quad,world_quad):
    a=[];b=[]
    for (x,y),(z,h) in zip(pixel_quad,world_quad):a.extend([[x,y,1,0,0,0,-z*x,-z*y],[0,0,0,x,y,1,-h*x,-h*y]]);b.extend([z,h])
    h=np.append(np.linalg.solve(a,b),1).reshape(3,3)
    def project(p):q=h@np.array([*p,1]);return q[:2]/q[2]
    return project
door=registration([(736,547),(958,550),(958,728),(738,706)],[(.50,1.22),(-.34,1.22),(-.34,.55),(.50,.55)])
# Four individually traced cab-door panels. The separations and exposed
# body above them are retained rather than replacing the side with a skin.
samples=[('Cab door upper forward',door,[(741,552),(824,553),(826,611),(741,610)]),('Cab door upper rear',door,[(834,553),(951,555),(953,614),(835,612)]),('Cab door lower forward',door,[(742,620),(826,621),(828,698),(744,691)]),('Cab door lower rear',door,[(836,623),(953,624),(953,717),(839,705)])]
front=registration([(419,524),(719,546),(712,709),(415,658)],[(2.02,1.16),(.55,1.16),(.55,.53),(2.02,.53)])
samples += [('Front arch crown',front,[(461,562),(593,569),(609,589),(484,584)]),('Front arch shoulder',front,[(602,569),(651,577),(678,607),(654,625),(623,594)]),('Front fender upper plate',front,[(480,526),(565,534),(566,562),(482,555)])]
rear=registration([(976,548),(1458,529),(1456,731),(976,733)],[(-.46,1.22),(-2.78,1.22),(-2.78,.55),(-.46,.55)])
samples += [('Rear arch crown',rear,[(1140,554),(1252,545),(1276,578),(1166,589)]),('Rear arch forward shoulder',rear,[(1103,580),(1131,560),(1157,592),(1125,622),(1104,614)]),('Rear arch rear shoulder',rear,[(1262,546),(1300,550),(1323,602),(1291,615),(1278,583)])]
records=[]
# Replace the rejected image-plane arch patches with broad five-piece armor
# following the measured original wheel opening. Width is a 3D adaptation;
# five divisions and crown/shoulder/leg construction follow the reference.
samples=[s for s in samples if not ('arch' in s[0].lower())]
arch_data=json.loads((O/'actual-arch-survey.json').read_text())
for family,limits in [('front',(.859,1.719)),('rear',(-2.070,-1.150))]:
    values=np.array([p for p in arch_data[family] if p[1] is not None]);zs=np.linspace(*limits,81)
    inner=np.array([(z,float(np.interp(z,values[:,0],values[:,1]))+.018) for z in zs])
    tangent=np.gradient(inner,axis=0);normal=np.stack([-tangent[:,1],tangent[:,0]],axis=1);normal/=np.linalg.norm(normal,axis=1)[:,None]
    widths=.18+.10*np.maximum(normal[:,1],0)
    outer=inner+normal*widths[:,None]
    if family=='front':outer[:,1]=np.minimum(outer[:,1],1.105)
    distance=np.r_[0,np.cumsum(np.linalg.norm(np.diff(inner,axis=0),axis=1))]
    divisions=[0,.19,.37,.63,.81,1]
    for i,(a,b) in enumerate(zip(divisions,divisions[1:])):
        low=a*distance[-1]+(.004 if i else 0);high=b*distance[-1]-(.004 if i<4 else 0)
        ds=np.linspace(low,high,13)
        inside=np.stack([np.interp(ds,distance,inner[:,j]) for j in range(2)],axis=1)
        outside=np.stack([np.interp(ds,distance,outer[:,j]) for j in range(2)],axis=1)
        polygon=np.concatenate([inside,outside[::-1]])
        samples.append((family.title()+' contour plate '+str(i+1),lambda p:np.array(p),polygon.tolist()))

def edge_clearance(q,outline):
    inside=False;minimum=100.
    for a,b in zip(outline,outline[1:]+outline[:1]):
        d=b-a;t=max(0,min(1,(q-a).dot(d)/d.length_squared));minimum=min(minimum,(q-a-d*t).length)
        if (a.y>q.y)!=(b.y>q.y) and q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x:inside=not inside
    return minimum if inside else -minimum
for name,project,pixels in samples:
    outline=[Vector((*project(p),0)) for p in pixels] # z, y, dummy
    if 'Front contour plate' in name:
        for q in outline:
            try:surface(q.y,q.x)
            except ValueError:
                for dy in np.arange(.005,.125,.005):
                    try:surface(q.y+dy,q.x);q.y+=float(dy)+.005;break
                    except ValueError:pass
    # Register the reference arch trim to the actual source wheel opening;
    # image-plane height alone places these pieces above the arch.
    if name.startswith('Cab door'):
        for p in outline:p.x=max(-.265,min(.46,p.x));p.y=max(.60,p.y)
    original_corners=list(outline);chamfered=[]
    for i,p in enumerate(outline):
        for neighbor in [outline[i-1],outline[(i+1)%len(outline)]]:chamfered.append(p+(neighbor-p).normalized()*.006)
    if 'contour plate' not in name:outline=chamfered
    triangles=tessellate_polygon([outline]);points=[];faces=[]
    def subdiv(a,b,c,depth):
        if depth:
            ab=(a+b)/2;bc=(b+c)/2;ca=(c+a)/2
            for tri in [(a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)]:subdiv(*tri,depth-1)
        else:
            ids=[]
            for q in [a,b,c]:
                ids.append(len(points));points.append((surface(q.y,q.x)+.006,q.y,q.x))
            faces.append(tuple(ids))
    try:
        for tri in triangles:subdiv(*[outline[v] if isinstance(v,int) else v for v in tri],3)
    except ValueError as error:
        records.append({'name':name,'referencePixels':pixels,'status':'OUTLINE_REGISTRATION_REQUIRES_CORRECTION','reason':str(error)});continue
    obj=mesh(name,points,faces)
    # Weld subdivision vertices before making a sheet. Independent triangles
    # previously extruded their own rims, producing the jagged broken edges.
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    average=sum((f.normal*f.calc_area() for f in bm.faces),Vector())
    if average.x<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    s=obj.modifiers.new('Separate 8 mm plate','SOLIDIFY');s.thickness=.008;s.offset=1
    center=sum(outline,Vector())/len(outline)
    desired=[p+(center-p).normalized()*.045 for p in original_corners] if 'contour plate' not in name else [outline[j]*.72+outline[-j-1]*.28 for j in [3,9]]+[outline[j]*.28+outline[-j-1]*.72 for j in [3,9]]
    placed=[]
    for q in desired:
        # The complete washer disk, plus 4 mm edge margin, must be inside.
        if edge_clearance(q,outline)<.017:
            options=[Vector((z,y,0)) for z in np.linspace(min(p.x for p in outline),max(p.x for p in outline),45) for y in np.linspace(min(p.y for p in outline),max(p.y for p in outline),45)]
            options=[p for p in options if edge_clearance(p,outline)>=.017]
            assert options,'No safe washer placement '+name
            q=min(options,key=lambda p:(q-p).length)
        if any((q-p).length<.030 for p in placed):continue
        placed.append(q);assert edge_clearance(q,outline)>=.017
        try:bolt((surface(q.y,q.x)+.014,q.y,q.x),(1,0,0),scale=.65,support=obj)
        except ValueError:pass
    records.append({'name':name,'referencePixels':pixels if 'contour plate' not in name else None,'minimumWasherCenterEdgeClearanceM':min(edge_clearance(q,outline) for q in placed),'status':'CLAY_SAMPLE_REQUIRES_VISUAL_REVIEW'})
scene=bpy.context.scene;cam=scene.camera
for label,eye,target,scale in [('door',(4,1.6,.6),(.8,.94,.10),1.5),('front-arch',(4,1.7,2.7),(.8,.86,1.3),2.0),('rear-arch',(4,1.7,-2.8),(.85,.87,-1.62),2.1)]:
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(label+'.png'));bpy.ops.render.render(write_still=True)
(O/'reference-outline-registration.json').write_text(json.dumps({'source':'armor-window-grilles.png','samples':records,'scope':'Visible-side sample geometry only. No hidden-side design, installation or reference acceptance inferred.'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'reference-plate-samples.blend'))
