"""Clay-only joint-fit diagnostic; original approved model is preserved."""
import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MasonryJointFit_v1';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'approved-clay.blend'))
core=bpy.data.objects['furnace_core']
tree=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get())
changes=[]
for o in bpy.context.scene.objects:
    if not o.name.startswith(('stone_','inferred_rear_stone_')):continue
    pts=[v.co.copy() for v in o.data.vertices]
    centre=sum(pts,Vector())/len(pts)
    hit=tree.find_nearest(centre)
    if hit[0] is None:raise RuntimeError('Missing furnace surface')
    normal=hit[1].normalized()
    # Broaden each block across its joint plane, preserving protrusion depth.
    # A diagnostic only: interpenetrations and outline drift must be reviewed.
    for v in o.data.vertices:
        delta=v.co-centre;depth=normal*delta.dot(normal)
        v.co=centre+depth+(delta-depth)*1.12
    changes.append({'object':o.name,'jointPlaneExpansion':1.12,'depthChanged':False})
# Recess exposed backing slightly; retain chamber geometry and normals.
# This experiment leaves backing unchanged to isolate masonry fit first.
report={'status':'CLAY_DIAGNOSTIC_REVIEW_REQUIRED','changes':changes,'coreChanged':False,'approval':'NOT_APPROVED','limits':['Neighbor intersections must be checked','Outer silhouette may drift','Remaining core exposure must be measured; this is not a completed stone construction fix']}
(out/'joint-fit-plan-and-receipt.json').write_text(json.dumps(report,indent=2))
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'clay-joint-fit.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'joint-fit.blend'))
bpy.ops.render.render(write_still=True)
