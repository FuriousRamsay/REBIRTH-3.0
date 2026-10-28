"""Plan donor-shaped bounded infill for measured wide exterior mortar patches."""
import bpy,json
from pathlib import Path
from mathutils import Vector,Matrix
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'ReferenceMortarFit_v17';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MortarRelief_v16/mortar-relief.blend'))
audit=json.loads((root/'MortarRelief_v16/mortar-width-audit.json').read_text());parts=[]
for i,patch in enumerate(audit['patches']):
    samples=patch['samples'];points=[Vector(s['position']) for s in samples]
    center=sum(points,Vector())/len(points)
    if center.z<.20:continue
    n=sum((Vector(s['normal']) for s in samples),Vector()).normalized()
    u=n.cross(Vector((0,0,1)))
    if u.length<.1:u=n.cross(Vector((1,0,0)))
    u.normalize();v=n.cross(u).normalized()
    coordinates=[((p-center).dot(u),(p-center).dot(v)) for p in points]
    size=[max(c[k] for c in coordinates)-min(c[k] for c in coordinates)+.014 for k in range(2)]
    if min(size)<.02:continue
    nearest=samples[len(samples)//2]['nearestStone']
    parts.append({'id':f'stone_reference_mortar_patch_{i:02d}','donor':nearest,'position':list(center+n*.006),'basis':[list(u),list(v),list(n)],'dimensions':[min(size[0],.12),min(size[1],.12),.070],'pixelCell':patch['pixelCell'],'trim':'Difference against all visible non-core meshes','jointMarginMeters':.003,'validation':'Observed visible exterior core; donor shape/clearance require clay review'})
(out/'part-plan.json').write_text(json.dumps({'source':'MortarRelief_v16/mortar-relief.blend','parts':parts,'geometryGate':'Render review required; no material advancement'},indent=2))
sc=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or o.name in ('Inspection ground','furnace_core'):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices);vertices.extend(o.matrix_world@p.co for p in mesh.vertices)
    faces.extend([start+j for j in p.vertices] for p in mesh.polygons);ev.to_mesh_clear()
mesh=bpy.data.meshes.new('Patch clearance');mesh.from_pydata(vertices,[],faces);mesh.update()
cutter=bpy.data.objects.new('Temporary clearance cutter',mesh);sc.collection.objects.link(cutter);cutter.hide_render=True
results=[]
for part in parts:
    donor=bpy.data.objects[part['donor']];ev=donor.evaluated_get(deps);data=ev.to_mesh()
    points=[donor.matrix_world@p.co for p in data.vertices]
    low=Vector(tuple(min(p[k] for p in points) for k in range(3)));high=Vector(tuple(max(p[k] for p in points) for k in range(3)))
    donorCenter=(low+high)/2;extent=high-low
    basis=[Vector(b) for b in part['basis']];center=Vector(part['position']);dimensions=part['dimensions']
    newpoints=[]
    for p in points:
        q=p-donorCenter
        newpoints.append(center+sum((basis[k]*q[k]/max(extent[k],.00001)*dimensions[k] for k in range(3)),Vector()))
    faces=[list(p.vertices) for p in data.polygons];ev.to_mesh_clear()
    mesh=bpy.data.meshes.new(part['id']);mesh.from_pydata(newpoints,[],faces);mesh.update()
    o=bpy.data.objects.new(part['id'],mesh);sc.collection.objects.link(o);o.data.materials.append(bpy.data.materials['Neutral untextured clay'])
    mod=o.modifiers.new('Clear existing stones and hardware','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    results.append({'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons)})
bpy.data.objects.remove(cutter,do_unlink=True)
(out/'geometry-receipt.json').write_text(json.dumps({'parts':results,'fidelity':'CLAY_REVIEW_PENDING','jointMargin':'Not enforced by current boolean; inspect joints'},indent=2))
sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'reference-fit.blend'));bpy.ops.render.render(write_still=True)
print('REFERENCE_MORTAR_PATCH_CANDIDATE_SAVED')
