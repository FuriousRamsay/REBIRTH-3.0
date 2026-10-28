"""Build measured 3D stone infill and trim against evaluated neighbors."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline/MasonryJointFit_v1')
plan=json.loads((root/'gap-infill-plan-v3.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']))
out=root.parent/'MasonryInfill_v3';out.mkdir(parents=True,exist_ok=True)
deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[]
for o in bpy.context.scene.objects:
    if not o.name.startswith(('stone_','inferred_rear_stone_')):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();base=len(verts)
    verts.extend(o.matrix_world@v.co for v in mesh.vertices)
    faces.extend([base+i for i in p.vertices] for p in mesh.polygons);ev.to_mesh_clear()
mesh=bpy.data.meshes.new('Neighbor stone trim volume');mesh.from_pydata(verts,[],faces);mesh.update()
cutter=bpy.data.objects.new('Neighbor trim helper',mesh);bpy.context.collection.objects.link(cutter);cutter.hide_render=True
mat=bpy.data.materials['Neutral untextured clay'];receipts=[]
for piece in plan['pieces']:
    bm=bmesh.new()
    for sample in piece['samples']:
        p=Vector(sample['position']);normal=Vector(sample['normal']).normalized()
        tangent=normal.cross(Vector((0,0,1)))
        if tangent.length<.01:tangent=normal.cross(Vector((0,1,0)))
        tangent.normalize();up=normal.cross(tangent).normalized()
        for d in [-plan['rearDepth'],plan['frontRelief']]:
            for a,b in [(-1,-1),(-1,1),(1,-1),(1,1)]:bm.verts.new(p+normal*d+(tangent*a+up*b)*plan['sampleBuffer'])
    result=bmesh.ops.convex_hull(bm,input=list(bm.verts))
    unused=[v for v in bm.verts if not v.link_faces]
    if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    mesh=bpy.data.meshes.new(piece['id']);bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new('stone_'+piece['id'],mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
    mod=obj.modifiers.new('Trim against neighboring stone solids','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bevel=obj.modifiers.new('Small fractured edge radius','BEVEL');bevel.width=.002;bevel.segments=1
    receipts.append({'object':obj.name,'vertices':len(obj.data.vertices),'polygons':len(obj.data.polygons)})
bpy.data.objects.remove(cutter,do_unlink=True)
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'clay-infill.png')
(out/'infill-receipt.json').write_text(json.dumps({'pieces':receipts,'status':'CLAY_REVIEW_PENDING','geometrySource':str(root/'joint-fit.blend'),'limits':'Backing remains unchanged; infill boundaries/shape and overlaps between infill pieces still need inspection.'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'infill.blend'))
bpy.ops.render.render(write_still=True)
