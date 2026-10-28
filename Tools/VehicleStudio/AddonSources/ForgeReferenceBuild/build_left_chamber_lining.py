"""Execute explicit measured plan and save isolated clay candidate."""
import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'LeftChamberLining_v32'
plan=json.loads((out/'geometry-plan.json').read_text());assert len(plan['parts'])==3
for part in plan['parts']:
    points=[Vector(p) for p in part['vertices']];assert len(points)==18
    assert .01<max((p-Vector(part['center'])).length for p in points)<.10
    assert abs(Vector(part['normal']).length-1)<.001
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']))
mat=bpy.data.materials.new('Left lining neutral clay');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.42,.42,.42,1);bs.inputs['Roughness'].default_value=.8
for part in plan['parts']:
    n=6;faces=[]
    for row in range(2):
        for j in range(n):faces.append([row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j])
    faces.extend([list(range(n-1,-1,-1)),list(range(2*n,3*n))])
    mesh=bpy.data.meshes.new(part['id']);mesh.from_pydata(part['vertices'],[],faces);mesh.update()
    obj=bpy.data.objects.new(part['id'],mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
    bevel=obj.modifiers.new('Small fractured edge','BEVEL');bevel.width=.0015;bevel.segments=2
for obj in list(bpy.context.scene.objects):
    if obj.type=='LIGHT' and obj.data.type=='POINT':bpy.data.objects.remove(obj,do_unlink=True)
    elif obj.type=='MESH' and obj.name!='Inspection ground':obj.data.materials.clear();obj.data.materials.append(mat)
scene=bpy.context.scene;scene.cycles.device='CPU';scene.cycles.samples=32
assert not (out/'left-lining-clay.blend').exists()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'left-lining-clay.blend'))
scene.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'addedMeshes':3,'originalGeometryChanged':False,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
