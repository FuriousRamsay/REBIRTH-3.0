import bpy,json,math,random
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'BasinGeometry_v24';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'CollarDetails_v22/collar-details.blend'))
o=bpy.data.objects['collar_inner_basin'];center=o.location.copy()
plan={'source':'CollarDetails_v22/collar-details.blend','object':o.name,'center':list(center),'radius':.111,'rings':6,'segments':64,'topReliefBoundMeters':.003,'baseThicknessMeters':.006,'centralPlugClearance':'Existing plug covers central surface; no change to plug/collar','seed':2419,'geometryGate':'Clay render before heat material'}
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
rng=random.Random(2419);verts=[(0,0,.003)];faces=[]
for ring in range(1,7):
    radius=.111*ring/6
    for j in range(64):
        a=math.tau*j/64;relief=.0018*math.sin(a*7+ring*1.37)+rng.uniform(-.0012,.0012)
        # Keep outer edge seated under collar lip.
        if ring==6:relief*=.25
        verts.append((radius*math.cos(a),radius*math.sin(a),.003+relief))
for j in range(64):faces.append((0,1+j,1+(j+1)%64))
for ring in range(1,6):
    low=1+(ring-1)*64;high=1+ring*64
    for j in range(64):faces.append((low+j,high+j,high+(j+1)%64,low+(j+1)%64))
bottom=len(verts);verts.append((0,0,-.003));edgeStart=len(verts)
for j in range(64):
    a=math.tau*j/64;verts.append((.111*math.cos(a),.111*math.sin(a),-.003))
top=1+5*64
for j in range(64):
    faces.append((top+j,edgeStart+j,edgeStart+(j+1)%64,top+(j+1)%64))
    faces.append((bottom,edgeStart+(j+1)%64,edgeStart+j))
mesh=bpy.data.meshes.new('Irregular refractory basin');mesh.from_pydata(verts,[],faces);mesh.update()
material=o.data.materials[0];o.data=mesh;o.data.materials.append(material);o.rotation_euler=(0,0,0)
for modifier in list(o.modifiers):o.modifiers.remove(modifier)
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'basin-clay.blend'));bpy.ops.render.render(write_still=True)
(out/'geometry-receipt.json').write_text(json.dumps({'vertices':len(verts),'polygons':len(faces),'maxTopReliefMeters':max(abs(v[2]-.003) for v in verts[1:385]),'clayReview':'PENDING'},indent=2))
print('BASIN_CLAY_SAVED')
