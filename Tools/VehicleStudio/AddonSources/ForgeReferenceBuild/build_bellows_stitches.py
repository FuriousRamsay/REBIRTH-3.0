import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsStitches_v34'
plan=json.loads((out/'geometry-plan.json').read_text())
assert all(abs((Vector(p['endpoints'][0])-Vector(p['endpoints'][1])).length-.006)<.00001 for p in plan['parts'])
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']));material=bpy.data.materials['Left lining neutral clay']
for part in plan['parts']:
    curve=bpy.data.curves.new(part['id'],'CURVE');curve.dimensions='3D';curve.bevel_depth=part['radiusMeters'];curve.bevel_resolution=2;curve.use_fill_caps=True
    spline=curve.splines.new('POLY');spline.points.add(1)
    for point,co in zip(spline.points,part['endpoints']):point.co=(*co,1)
    obj=bpy.data.objects.new(part['id'],curve);bpy.context.collection.objects.link(obj);obj.data.materials.append(material)
scene=bpy.context.scene;scene.cycles.device='CPU';scene.cycles.samples=32
assert not (out/'bellows-stitches-clay.blend').exists()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'bellows-stitches-clay.blend'))
scene.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'addedStitches':len(plan['parts']),'originalBodyUnchanged':True,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
