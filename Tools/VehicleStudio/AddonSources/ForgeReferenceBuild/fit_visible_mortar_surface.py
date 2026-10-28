"""Bounded outer-mortar contact fit; protect chamber/collar and every stone."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MortarContactFit_v29';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'VisibleMortarIronwork_v27/visible-mortar-ironwork.blend'))
sc=bpy.context.scene;core=bpy.data.objects['furnace_core'];deps=bpy.context.evaluated_depsgraph_get()
center=Vector((.2169066,.4890315,0));hearth=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text());axis=Vector(hearth['outwardAxis']);side=Vector((-axis.y,axis.x,0));profile=hearth['openingProfileSideZ']
collar=next(p for p in json.loads((Path(__file__).parent/'round-components-plan.json').read_text())['parts'] if p['id']=='top_collar');collarCenter=Vector(collar['center'])
def in_profile(x,z):
    inside=False
    for i,a in enumerate(profile):
        b=profile[(i+1)%len(profile)]
        if (a[1]>z)!=(b[1]>z) and x<(b[0]-a[0])*(z-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside
verts=[];faces=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or not o.name.startswith(('stone_','inferred_rear_stone_')):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(verts);verts.extend(o.matrix_world@v.co for v in mesh.vertices)
    for p in mesh.polygons:
        world=o.matrix_world@p.center;radial=Vector((world.x-center.x,world.y-center.y,0))
        normal=o.matrix_world.to_3x3()@p.normal
        if normal.dot(radial)>.02:faces.append([start+i for i in p.vertices])
    ev.to_mesh_clear()
tree=BVHTree.FromPolygons(verts,faces);changes=[];inverse=core.matrix_world.inverted()
for v in core.data.vertices:
    p=core.matrix_world@v.co;radial=Vector((p.x-center.x,p.y-center.y,0))
    if radial.length<.05 or p.z<.025:continue
    normal=core.matrix_world.to_3x3()@v.normal
    if normal.dot(radial)<.015:continue
    if p.dot(axis)<hearth['frontDepth']+.015 and in_profile(p.dot(side),p.z):continue
    if p.z>.810 and Vector((p.x-collarCenter.x,p.y-collarCenter.y,0)).length<.153:continue
    radial.normalize();near,n,index,distance=tree.find_nearest(p)
    if near is None or distance>.080:continue
    amount=max(0,min(.025,(near-p).dot(radial)-.008))
    if amount<.001:continue
    destination=p+radial*amount
    changes.append({'vertex':v.index,'from':list(p),'to':list(destination),'moveMeters':amount,'nearestOuterStoneDistance':distance})
plan={'source':'VisibleMortarIronwork_v27/visible-mortar-ironwork.blend','object':'furnace_core','vertexChanges':changes,'maximumMoveMeters':.025,'targetRecessMeters':.008,'protectedChamber':'Authored opening polygon and depth','protectedCollarRadius':.153,'stoneGeometryUnchanged':True,'quality':'RENDER_REQUIRED_NO_FIDELITY_PASS'}
assert changes and all(c['moveMeters']<=.02500001 for c in changes)
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
for change in changes:core.data.vertices[change['vertex']].co=inverse@Vector(change['to'])
core.data.update()
(out/'geometry-receipt.json').write_text(json.dumps({'changedVertices':len(changes),'maxMoveMeters':max(c['moveMeters'] for c in changes),'stonesChanged':False,'fidelity':'RENDER_REVIEW_PENDING'},indent=2))
sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.filepath=str(out/'reference-material.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'mortar-contact.blend'));bpy.ops.render.render(write_still=True)
print('MORTAR_CONTACT_CANDIDATE_SAVED')
