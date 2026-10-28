"""Execute measured surface seam paths without modifying the bellows body."""
import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsSeams_v33'
plan=json.loads((out/'geometry-plan.json').read_text())
assert len(plan['parts'])==4
for part in plan['parts']:
    assert part['radiusMeters']==.0025 and len(part['points'])>=15
    assert max((Vector(a)-Vector(b)).length for a,b in zip(part['points'],part['points'][1:]))<.02
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']))
material=bpy.data.materials.get('Left lining neutral clay');assert material
for part in plan['parts']:
    curve=bpy.data.curves.new(part['id'],'CURVE');curve.dimensions='3D';curve.resolution_u=1;curve.bevel_depth=part['radiusMeters'];curve.bevel_resolution=3;curve.use_fill_caps=True
    spline=curve.splines.new('POLY');spline.points.add(len(part['points'])-1)
    for point,co in zip(spline.points,part['points']):point.co=(*co,1)
    obj=bpy.data.objects.new(part['id'],curve);bpy.context.collection.objects.link(obj);obj.data.materials.append(material)
scene=bpy.context.scene;scene.cycles.device='CPU';scene.cycles.samples=32
assert not (out/'bellows-seams-clay.blend').exists()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'bellows-seams-clay.blend'))
scene.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'addedSeams':4,'radiusMeters':.0025,'originalGeometryChanged':False,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
