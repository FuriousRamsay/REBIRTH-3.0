"""Bounded geometry relief on exterior mortar only; preserve chamber and stones."""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MortarRelief_v16';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SideMeasuredFit_v15/side-fit.blend'))
core=bpy.data.objects['furnace_core']
hearth=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text())
axis=Vector(hearth['outwardAxis']);front=hearth['frontDepth']
plan={'source':str(root/'SideMeasuredFit_v15/side-fit.blend'),'object':core.name,'rotationChange':[0,0,0],'positionChange':[0,0,0],'maximumReliefMeters':.0025,'featureScaleMeters':[.021,.009],'protectHearthDepthMeters':.075,'preserveAllOtherObjects':True,'purpose':'Break flat mortar surface into small irregular ridges/pits; joint-width issues remain a separate check'}
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
bm=bmesh.new();bm.from_mesh(core.data)
bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
bm.normal_update();changed=0;maximum=0
for v in bm.verts:
    world=core.matrix_world@v.co
    if .26<world.z<.80 and abs(world.dot(axis)-front)<.075:continue
    # Only outward-facing shell; never alter the inner hearth liner.
    radial=Vector((world.x,world.y,0))
    normal=core.matrix_world.to_3x3()@v.normal
    if normal.dot(radial)<=.03 or world.z<.06:continue
    coarse=math.sin(world.x/ .021+math.sin(world.z/.019))*math.cos(world.y/.023-world.z/.027)
    fine=math.sin((world.x+world.y)/.009)*math.cos(world.z/.011)
    displacement=.0018*coarse+.0007*fine
    v.co+=v.normal*displacement;changed+=1;maximum=max(maximum,abs(displacement))
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(core.data);bm.free()
core['surfaceRole']='Uneven recessed mortar, bounded geometric ridges and pits'
(out/'geometry-receipt.json').write_text(json.dumps({'changedVertices':changed,'maximumDisplacement':maximum,'clayReview':'PENDING','jointWidth':'NOT_VERIFIED','otherGeometryUnchanged':True},indent=2))
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'mortar-relief.blend'));bpy.ops.render.render(write_still=True)
print('MORTAR_RELIEF_CLAY_SAVED_REVIEW_PENDING')
