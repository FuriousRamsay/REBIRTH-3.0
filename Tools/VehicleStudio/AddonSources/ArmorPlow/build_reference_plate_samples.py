"""Separate review samples. Pixel outlines are observations; 3D registration
to this camper is an explicit adaptation, not a claim of calibrated dimensions."""
import bpy,bmesh,json,ast,math,sys,numpy as np
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
dg=bpy.context.evaluated_depsgraph_get()
# Use the actual outer skin, excluding small disconnected trim/fastener
# islands and inward-facing surfaces. Hardware must not deform a fitted plate.
skin=bpy.data.objects['body'];bm=bmesh.new();bm.from_mesh(skin.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00005);bm.faces.ensure_lookup_table();bm.normal_update()
remaining=set(bm.faces);skin_faces=[]
while remaining:
    seed=remaining.pop();island={seed};pending=[seed]
    while pending:
        for edge in pending.pop().edges:
            for face in edge.link_faces:
                if face in remaining:remaining.remove(face);island.add(face);pending.append(face)
    if sum(f.calc_area() for f in island)>.04:skin_faces.extend(f for f in island if f.normal.x>.35)
bm.verts.ensure_lookup_table();bm.verts.index_update()
vertices=[skin.matrix_world@v.co for v in bm.verts]
skin_bvh=BVHTree.FromPolygons(vertices,[[v.index for v in f.verts] for f in skin_faces]);bm.free()
def surface(y,z):
    p,n,_,_=skin_bvh.ray_cast(Vector((2,y,z)),Vector((-1,0,0)),1.4)
    if p is None:raise ValueError('Outside original outer body skin '+str((y,z)))
    return p,n
boundary_cache={}
upper_cache={}
def upper_boundary(z):
    key=round(float(z),5)
    if key not in upper_cache:
        top=None
        for y in np.arange(1.30,.28,-.004):
            try:surface(float(y),z);top=float(y);break
            except ValueError:pass
        assert top is not None
        lo=top;hi=top+.004
        for _ in range(12):
            mid=(lo+hi)/2
            try:surface(mid,z);lo=mid
            except ValueError:hi=mid
        upper_cache[key]=lo
    return upper_cache[key]
def lower_boundary(z):
    key=round(float(z),5)
    if key not in boundary_cache:
        low=None
        for y in np.arange(.28,1.36,.004):
            try:surface(float(y),z);low=float(y);break
            except ValueError:pass
        if low is None:raise ValueError('No side panel at Z '+str(z))
        lo=low-.004;hi=low
        for _ in range(12):
            mid=(lo+hi)/2
            try:surface(mid,z);hi=mid
            except ValueError:lo=mid
        low=hi
        boundary_cache[key]=low
    return boundary_cache[key]
def registration(pixel_quad,world_quad):
    # Stable local affine registration. A four-point projective transform of
    # the curved opening is invalid and explodes above those control points.
    h=np.linalg.lstsq(np.array([[x,y,1] for x,y in pixel_quad]),np.array(world_quad),rcond=None)[0]
    def project(p):return np.array([*p,1])@h
    return project
door=registration([(736,547),(958,550),(958,728),(738,706)],[(.50,1.22),(-.34,1.22),(-.34,.55),(.50,.55)])
# Four individually traced cab-door panels. The separations and exposed
# body above them are retained rather than replacing the side with a skin.
samples=[
 ('Cab door upper forward',lambda p:np.array(p),[(.530,1.220),(.120,1.220),(.120,.945),(.530,.945)]),
 ('Cab door upper rear',lambda p:np.array(p),[(.105,1.220),(-.265,1.220),(-.265,.945),(.105,.945)]),
 ('Cab door lower forward',lambda p:np.array(p),[(.530,.930),(.120,.930),(.120,.550),(.530,.550)]),
 ('Cab door lower rear',lambda p:np.array(p),[(.105,.930),(-.265,.930),(-.265,.550),(.105,.550)])]

# Register observed inner wheel-opening corners to the original camper.
# These correspondences adapt the supplied reference to the actual asset;
# every plate outline and fastener remains explicit in the source trace.
front=registration([(490,638),(615,628),(655,744),(427,722)],[(1.57,.880),(.999,.880),(.859,.500),(1.719,.520)])
rear=registration([(1144,647),(1272,640),(1296,722),(1111,731)],[(-1.30,.835),(-1.93,.835),(-2.070,.535),(-1.150,.495)])
trace=json.loads((Path(__file__).parent/'reference-fitted-panels.json').read_text())
explicit_bolts={}
for family in ['front','rear']:
    for row in trace[family]:
        samples.append((row['name'],lambda p:np.array(p),row['outline']))
records=[]

def edge_clearance(q,outline):
    inside=False;minimum=100.
    for a,b in zip(outline,outline[1:]+outline[:1]):
        d=b-a
        if d.length_squared<1e-12:continue
        t=max(0,min(1,(q-a).dot(d)/d.length_squared));minimum=min(minimum,(q-a-d*t).length)
        if (a.y>q.y)!=(b.y>q.y) and q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x:inside=not inside
    return minimum if inside else -minimum
for name,project,pixels in samples:
    outline=[Vector((*project(p),0)) for p in pixels] # z, y, dummy
    # Register the reference arch trim to the actual source wheel opening;
    # image-plane height alone places these pieces above the arch.
    if name.startswith('Cab door'):
        for p in outline:p.x=max(-.265,min(.53,p.x));p.y=max(.55,p.y)
    
    original_corners=list(outline);chamfered=[]
    for i,p in enumerate(outline):
        for neighbor in [outline[i-1],outline[(i+1)%len(outline)]]:chamfered.append(p+(neighbor-p).normalized()*.006)
    outline=chamfered
    fitted=[]
    for a,b in zip(outline,outline[1:]+outline[:1]):
        count=max(1,math.ceil((b-a).length/.012))
        for t in np.linspace(0,1,count,endpoint=False):
            q=a+(b-a)*float(t);q.y=min(upper_boundary(q.x)-.022,max(q.y,lower_boundary(q.x)+.022));fitted.append(q)
    outline=[]
    for q in fitted:
        if not outline or (q-outline[-1]).length>1e-6:outline.append(q)
    if (outline[0]-outline[-1]).length<1e-6:outline.pop()
    # Remove collinear resampling points before triangulation. Keeping hundreds
    # of nearly collinear float32 ears creates overlapping sliver triangles.
    changed=True
    while changed and len(outline)>3:
        changed=False
        for i,q in enumerate(outline):
            a=outline[i-1];b=outline[(i+1)%len(outline)];d=b-a
            if d.length_squared<1e-10:continue
            t=(q-a).dot(d)/d.length_squared
            if 0<=t<=1 and (q-a-d*t).length<.00015:
                outline.pop(i);changed=True;break
    triangles=tessellate_polygon([outline]);points=[];faces=[]
    def sheet_point(y,z):
        p,n=surface(round(float(y),6),round(float(z),6));return p+Vector((.006,0,0))
    def subdiv(a,b,c,depth):
        if depth:
            ab=(a+b)/2;bc=(b+c)/2;ca=(c+a)/2
            for tri in [(a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)]:subdiv(*tri,depth-1)
        else:
            ids=[]
            for q in [a,b,c]:
                ids.append(len(points));points.append(tuple(sheet_point(q.y,q.x)))
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
    obj['panel_outline_zy']=json.dumps([[p.x,p.y] for p in outline]);obj['panel_owner']='cab door' if name.startswith('Cab door') else 'front fender' if name.startswith('Front') else 'pickup bed'
    s=obj.modifiers.new('Separate 8 mm plate','SOLIDIFY');s.thickness=.008;s.offset=1
    bpy.context.view_layer.update();plate_surface=BVHTree.FromObject(obj,bpy.context.evaluated_depsgraph_get())
    center=sum(outline,Vector())/len(outline)
    desired=explicit_bolts.get(name,[p+(center-p).normalized()*.050 for p in original_corners])
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
        p,n,_,_=plate_surface.ray_cast(Vector((2,q.y,q.x)),Vector((-1,0,0)),1.5)
        assert p is not None,'No plate beneath bolt '+name
        before=set(collection.objects);bolt(p,n,scale=.65,support=obj)
        for hardware in set(collection.objects)-before:
            if 'washer' not in hardware.name.lower():continue
            for i in range(24):
                v=hardware.data.vertices[i];hit,hn,_,_=plate_surface.ray_cast(v.co+n*.035,-n,.070)
                assert hit is not None,'Washer extends beyond '+name
                v.co=hit;hardware.data.vertices[i+24].co=hit+n*.0039
    records.append({'name':name,'referencePixels':pixels,'minimumWasherCenterEdgeClearanceM':min(edge_clearance(q,outline) for q in placed),'status':'CLAY_SAMPLE_REQUIRES_VISUAL_REVIEW'})
scene=bpy.context.scene;cam=scene.camera
for label,eye,target,scale in ([] if '--no-render' in sys.argv else [('door',(4,1.6,.6),(.8,.94,.10),1.5),('front-arch',(4,1.7,2.7),(.8,.86,1.3),2.0),('rear-arch',(4,1.7,-2.8),(.85,.87,-1.62),2.1)]):
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(label+'.png'));bpy.ops.render.render(write_still=True)
(O/'reference-outline-registration.json').write_text(json.dumps({'source':'armor-window-grilles.png','samples':records,'scope':'Visible-side sample geometry only. No hidden-side design, installation or reference acceptance inferred.'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'reference-plate-samples.blend'))
