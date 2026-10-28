"""Fit traced panel outlines to source surfaces; report unsupported points.
This is a clay fitting study, deliberately not an installable asset.
"""
import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import tessellate_polygon
ROOT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
OUT=ROOT/'ArmorClay';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'PloughClay/plough-clay-study.blend'))
trace=json.loads((Path(__file__).parent/'reference-landmarks.json').read_text())
mat=bpy.data.materials.get('Untextured guard')
coll=bpy.data.collections.new('Traced surface-fitted armor');bpy.context.scene.collection.children.link(coll)
body=bpy.data.objects['body'];bv=BVHTree.FromPolygons([v.co for v in body.data.vertices],[p.vertices[:] for p in body.data.polygons],all_triangles=True)
camper=bpy.data.objects['camper_body'];cv=BVHTree.FromPolygons([v.co for v in camper.data.vertices],[p.vertices[:] for p in camper.data.polygons],all_triangles=True)

def transform(source,dest):
    A=[];B=[]
    for (x,y),(u,v) in zip(source,dest):
        A.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]]);B.extend([u,v])
    H=np.append(np.linalg.solve(A,B),1).reshape(3,3)
    def project(p):
        q=H@np.array([*p,1]);return q[:2]/q[2]
    return project
# Per body-region mapping preserves the reference's relative plate outlines while
# accommodating the actual camper's different wheelbase/cab proportions. These
# anchor selections are authored estimates, not image-calibrated dimensions.
cab=transform([[714,542],[953,542],[955,719],[713,719]],[[.44,1.18],[-.40,1.18],[-.40,.53],[.44,.53]])
bed=transform([[970,545],[1354,548],[1358,684],[973,720]],[[-.58,1.14],[-2.62,1.14],[-2.62,.54],[-.58,.54]])
rear=transform([[1360,548],[1468,545],[1468,678],[1360,687]],[[-2.65,1.18],[-3.30,1.18],[-3.30,.54],[-2.65,.54]])
upper=transform([[970,281],[1452,337],[1455,497],[970,496]],[[-.55,2.20],[-3.25,2.20],[-3.25,1.40],[-.55,1.40]])
arch=transform([[400,565],[680,565],[700,758],[400,758]],[[1.98,1.10],[.60,1.10],[.60,.47],[1.98,.47]])
report=[]
for row in trace['armorReference']['plates']:
    name=row['id'];path=np.array(row['outline'],float)
    mapping=arch if name.startswith('front-arch') else cab if name.startswith('cab-door') else upper if name in ['hatch','rear-quarter-upper','side-bracket-left','side-bracket-right'] else rear if name.startswith('rear-quarter') else bed
    surface=cv if mapping in [upper,rear] else bv
    for side in [-1,1]:
        positions=[];unsupported=[]
        for px in path:
            z,y=mapping(px);hit,n,face,distance=surface.ray_cast(Vector((side*2.,float(y),float(z))),Vector((-side,0,0)),4.)
            if hit is None:
                # Bounded fitting to an actual adjacent body edge; never invent a
                # default flat side depth across wheel openings or body gaps.
                hit,n,face,distance=surface.find_nearest(Vector((side*.89,float(y),float(z))),.12)
                if hit is None:
                    unsupported.append(px.tolist());continue
            # The surface itself, including cab/bed folds, determines depth.
            positions.append((hit.x+side*.028,hit.y,hit.z))
        entry={'id':name,'side':side,'pixelOutline':path.tolist(),'unsupported':unsupported,'status':'NO_CLAIM_OF_ACCEPTANCE'}
        if unsupported:
            entry['status']='REJECTED_FIT_UNSUPPORTED_POINT';report.append(entry);continue
        # Corner-only fitting leaves a flat sheet cutting through curved doors.
        # Subdivide in the side plane and project EVERY generated vertex to the
        # measured source surface, retaining the traced perimeter.
        verts=[];faces=[]
        for tri in tessellate_polygon([[Vector(p) for p in positions]]):
            a,b,c=[Vector(positions[v]) if isinstance(v,int) else v for v in tri];steps=max(1,int(max((a-b).length,(a-c).length,(b-c).length)/.045)+1)
            ids={}
            for i in range(steps+1):
                for j in range(steps+1-i):
                    p=a+(b-a)*(i/steps)+(c-a)*(j/steps)
                    hit,n,face,distance=surface.ray_cast(Vector((side*2,p.y,p.z)),Vector((-side,0,0)),4.)
                    if hit is not None:p=Vector((hit.x+side*.028,hit.y,hit.z))
                    ids[i,j]=len(verts);verts.append(tuple(p))
            for i in range(steps):
                for j in range(steps-i):
                    f=(ids[i,j],ids[i+1,j],ids[i,j+1])
                    if (Vector(verts[f[1]])-Vector(verts[f[0]])).cross(Vector(verts[f[2]])-Vector(verts[f[0]])).x*side<0:f=f[::-1]
                    faces.append(f)
                    if j<steps-i-1:
                        f=(ids[i+1,j],ids[i+1,j+1],ids[i,j+1])
                        if (Vector(verts[f[1]])-Vector(verts[f[0]])).cross(Vector(verts[f[2]])-Vector(verts[f[0]])).x*side<0:f=f[::-1]
                        faces.append(f)
        m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
        o=bpy.data.objects.new(name+str(side),m);coll.objects.link(o);m.materials.append(mat)
        s=o.modifiers.new('Separate steel plate','SOLIDIFY');s.thickness=.018
        b=o.modifiers.new('Fabricated cut edge','BEVEL');b.width=.004;b.segments=2
        entry['verticesUnityM']=positions;report.append(entry)

(OUT/'fit-report.json').write_text(json.dumps(report,indent=2))
scene=bpy.context.scene;scene.view_settings.exposure=-1.4;cam=scene.camera;cam.data.ortho_scale=6.6;cam.location=(6,3.7,7)
forward=(Vector((0,1.6,-.15))-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
scene.render.filepath=str(OUT/'front.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'armor-clay-study.blend'))
print('ARMOR_STUDY',len(report),'unsupported',sum(bool(r['unsupported']) for r in report))
