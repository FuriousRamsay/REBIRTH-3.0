import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'HearthLipAudit_v46';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'StoneEdgeSchedule_v45/stone-edges-clay.blend'))
sc=bpy.context.scene;cam=sc.camera;deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[];owners=[]
for obj in sc.objects:
    if obj.type!='MESH' or obj.hide_render or obj.name=='Inspection ground':continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();start=len(verts);verts.extend(obj.matrix_world@v.co for v in mesh.vertices)
    for polygon in mesh.polygons:faces.append([start+i for i in polygon.vertices]);owners.append({'object':obj.name,'face':polygon.index})
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(verts,faces);direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));width,height=sc.render.resolution_x,sc.render.resolution_y;sx=cam.data.ortho_scale;sy=sx*height/width;records=[]
for x,y in [(957,445),(978,432),(1005,417),(1033,405),(1088,414),(1141,439),(1170,466)]:
    origin=cam.matrix_world@Vector(((x/width-.5)*sx,(.5-y/height)*sy,0));p,n,index,d=tree.ray_cast(origin,direction,100)
    records.append({'pixel':[x,y],'firstHit':owners[index] if p is not None else None,'position':list(p) if p is not None else None,'normal':list(n) if p is not None else None})
report={'source':'StoneEdgeSchedule_v45/stone-edges-clay.blend','samples':records,'scope':'Actual first-hit geometry at apparent arch lip; no edit or fidelity pass'}
path=out/'lip-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps(records))
