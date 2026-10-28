"""Unaccepted geometry study: follow actual window outlines, not AABBs.
Blender coordinates retain Unity XYZ during authoring; camera up is Unity Y.
No material/texturing, FBX deployment or bundle installation in this stage.
"""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
OUT=ROOT/'GuardClay'; OUT.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

def material(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.65
    return m
clay=material('Untextured source body',(.28,.28,.28))
glass=material('Window silhouette',(.035,.045,.05))
guard=material('Untextured guard',(.48,.48,.48))

def mesh(name,verts,faces,mat):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);o.data.materials.append(mat);return o

def tube(name,points,radius,mat=guard,closed=False):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=radius;c.bevel_resolution=3;c.resolution_u=1
    s=c.splines.new('POLY');s.points.add(len(points)-1)
    for a,p in zip(s.points,points):a.co=(*p,1)
    s.use_cyclic_u=closed
    o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(mat);return o

def convex(points):
    pts=sorted(set(map(tuple,points)))
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo=[];hi=[]
    for p in pts:
        while len(lo)>1 and cross(lo[-2],lo[-1],p)<=1e-8:lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi)>1 and cross(hi[-2],hi[-1],p)<=1e-8:hi.pop()
        hi.append(p)
    return np.array(lo[:-1]+hi[:-1])

def rounded(hull,r=.045):
    out=[]
    for i,p in enumerate(hull):
        a=hull[i-1]-p;b=hull[(i+1)%len(hull)]-p
        a=p+a/np.linalg.norm(a)*min(r,np.linalg.norm(a)*.22)
        b=p+b/np.linalg.norm(b)*min(r,np.linalg.norm(b)*.22)
        for t in np.linspace(0,1,9):out.append((1-t)**2*a+2*t*(1-t)*p+t*t*b)
    return np.array(out)

def fitting_corners(hull):
    # Tessellated glass curvature is not a set of construction joints. Reduce
    # collinear/rounded samples to the window's principal silhouette corners.
    hull=list(hull)
    while len(hull)>4:
        errors=[]
        for i,p in enumerate(hull):
            a=hull[i-1];b=hull[(i+1)%len(hull)];v=b-a
            q=a+np.clip(np.dot(p-a,v)/np.dot(v,v),0,1)*v
            errors.append(np.linalg.norm(p-q))
        k=int(np.argmin(errors))
        if errors[k]>.025:break
        hull.pop(k)
    return np.array(hull)

def cut(hull,axis,k):
    hits=[]
    for a,b in zip(hull,np.roll(hull,-1,axis=0)):
        if (a[axis]<=k<b[axis]) or (b[axis]<=k<a[axis]):
            t=(k-a[axis])/(b[axis]-a[axis]);hits.append(a+t*(b-a))
    return hits

report=[]
for row in json.loads((ROOT/'surfaces.json').read_text())['meshes']:
    vs=np.array([[p['x'],p['y'],p['z']] for p in row['vertices']]);ts=np.array(row['triangles']).reshape(-1,3)
    mesh(row['name'],vs.tolist(),ts.tolist(),glass if row['name']=='windows' else clay)
    if row['name']!='windows':continue
    # Weld geometric seams before finding independent glass components.
    parent=list(range(len(vs)))
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    bypos={}
    for i,p in enumerate(vs):
        k=tuple(np.round(p,4))
        if k in bypos:parent[find(i)]=find(bypos[k])
        else:bypos[k]=i
    for a,b,c in ts:parent[find(b)]=find(a);parent[find(c)]=find(a)
    groups={}
    for i in range(len(vs)):groups.setdefault(find(i),[]).append(i)
    for indices in groups.values():
        pts=np.unique(np.round(vs[indices],4),axis=0);center=pts.mean(axis=0)
        if len(pts)<4:continue
        _,s,vh=np.linalg.svd(pts-center,full_matrices=False);normal=vh[-1]
        if s[1]<.08:continue
        if abs(normal[0])>.75:normal*=np.sign(normal[0]*center[0])
        elif abs(normal[2])>.65:normal*=np.sign(normal[2]*(center[2]+.6))
        else:continue
        # Windows can be double surfaces. Preserve their raked plane and contour.
        up=np.array([0.,1.,0.]);right=np.cross(up,normal);right/=np.linalg.norm(right);up=np.cross(normal,right)
        flat=np.stack([(pts-center)@right,(pts-center)@up],axis=1);hull=fitting_corners(convex(flat))
        size=np.ptp(hull,axis=0)
        if size[0]<.20 or size[1]<.20 or size[0]>1.9 or size[1]>1.2:continue
        n=len(report);name=f'Guard{n:02d}';origin=center+normal*.070
        def world(p,depth=0):return (origin+right*p[0]+up*p[1]+normal*depth).tolist()
        outline=rounded(hull);tube(name+' rounded continuous frame',[world(p) for p in outline],.026,closed=True)
        # Reference distinction: heavy frame, finer square grid, separate collars.
        for axis in (0,1):
            low=hull[:,axis].min();high=hull[:,axis].max();count=max(2,round((high-low)/.075))
            for k in np.linspace(low,high,count+1)[1:-1]:
                ends=cut(hull,axis,k)
                if len(ends)==2:tube(name+' clipped fine grid',[world(p,-.006) for p in ends],.0045)
        for i,p in enumerate(hull):
            # Real modeled bend/collar and backing foot; not square corner bars.
            a=hull[i-1]-p;b=hull[(i+1)%len(hull)]-p
            for direction in (a,b):
                direction/=np.linalg.norm(direction);q=p+direction*.075
                tube(name+' corner sleeve',[world(q-direction*.014),world(q+direction*.014)],.034)
            # Mount foot lies on window surround plane. Location remains reviewable.
            q=p+(p-hull.mean(axis=0))*.045
            corners=[world(q+np.array(d),-.045) for d in [(-.033,-.04),(.033,-.04),(.033,.04),(-.033,.04)]]
            foot=mesh(name+' separate mounting foot',corners,[(0,1,2,3)],guard)
            mod=foot.modifiers.new('Plate thickness','SOLIDIFY');mod.thickness=.012
            tube(name+' stand off',[world(q,-.045),world(p)],.013)
            tube(name+' mounting fastener',[world(q,-.035),world(q,-.018)],.011)
        report.append({'name':name,'center':center.tolist(),'normal':normal.tolist(),'silhouettePlane':hull.tolist(),'size':size.tolist(),'source':'actual connected glass vertices','status':'CLAY_STUDY_NOT_ACCEPTED'})

(OUT/'geometry-report.json').write_text(json.dumps(report,indent=2))
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.15,.15,1)
for location,power,size in [((-4,6,7),1800,6),((5,4,-2),1300,5)]:
    data=bpy.data.lights.new('Studio','AREA');data.energy=power;data.shape='DISK';data.size=size
    o=bpy.data.objects.new('Studio',data);bpy.context.collection.objects.link(o);o.location=location;o.rotation_euler=(Vector((0,1.5,-.4))-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Clay inspection');cam=bpy.data.objects.new('Clay inspection',camdata);bpy.context.collection.objects.link(cam);bpy.context.scene.camera=cam
camdata.type='ORTHO';camdata.ortho_scale=6.1
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
for name,eye in [('front',(-6,4,7)),('side',(-7,2,-.5))]:
    cam.location=eye;cam.rotation_euler=(Vector((0,1.6,-.4))-cam.location).to_track_quat('-Z','Y').to_euler()
    # Camera uses Unity's Y-up rather than Blender's default Z-up.
    forward=(Vector((0,1.6,-.4))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward)
    from mathutils import Matrix
    cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
    scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'guard-clay-study.blend'))
print('GUARD_CLAY_STUDY_COMPLETE',len(report))
