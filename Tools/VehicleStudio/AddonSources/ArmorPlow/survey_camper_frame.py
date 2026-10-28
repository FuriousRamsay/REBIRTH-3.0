import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
bpy.ops.wm.open_mainfile(filepath=str(P/'PloughClay/ApprovedPloughGeometry_PendingCamperMount/plough-clay-study.blend'))
mat=bpy.data.materials['Untextured source body'];report=[]
for row in json.loads((P/'running-gear.json').read_text())['meshes']:
    vs=np.array([[p['x'],p['y'],p['z']] for p in row['vertices']]);ts=np.array(row['triangles']).reshape(-1,3)
    m=bpy.data.meshes.new(row['name']);m.from_pydata(vs.tolist(),[],ts.tolist());m.update();o=bpy.data.objects.new(row['name'],m);bpy.context.collection.objects.link(o);m.materials.append(mat)
    if row['name']!='parts':continue
    parent=list(range(len(vs)))
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    points={}
    for i,p in enumerate(vs):
        key=tuple(np.round(p,4))
        if key in points:parent[find(i)]=find(points[key])
        else:points[key]=i
    for a,b,c in ts:parent[find(b)]=find(a);parent[find(c)]=find(a)
    groups={}
    for i in range(len(vs)):groups.setdefault(find(i),[]).append(i)
    for key,indices in groups.items():
        pts=vs[indices];low=pts.min(0);high=pts.max(0)
        report.append({'id':int(key),'min':low.tolist(),'max':high.tolist(),'vertices':len(indices),'extent':(high-low).tolist()})
(P/'running-gear-components.json').write_text(json.dumps(sorted(report,key=lambda r:r['extent'][2],reverse=True),indent=2))
scene=bpy.context.scene;cam=scene.camera
for name,eye,target,scale in [('frame-under',(2.8,-1,2.4),(0,.42,1.1),2.8),('frame-front',(3,.7,3.5),(.3,.55,1.8),2.6)]:
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(P/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'source-with-running-gear.blend'))

