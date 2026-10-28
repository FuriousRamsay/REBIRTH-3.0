"""Plan, validate and apply bounded existing-stone edge changes near lower joints."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'WallJointEdges_v18';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MortarRelief_v16/mortar-relief.blend'))
audit=json.loads((root/'MortarRelief_v16/mortar-width-audit.json').read_text())
allowed={'stone_under_sill_right','stone_jamb_left_bottom','stone_base_far_right'}
samples={name:[] for name in allowed}
for patch in audit['patches']:
    for s in patch['samples']:
        if s['nearestStone'] in allowed and .20<s['position'][2]<.29:
            samples[s['nearestStone']].append(Vector(s['position']))
parts=[]
for name,targets in samples.items():
    if not targets:continue
    o=bpy.data.objects[name];changes=[]
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        # Hearth arch/sill starts above this protected elevation.
        if p.z>.285:continue
        target=min(targets,key=lambda t:(p-t).length);delta=target-p;distance=delta.length
        if not .002<distance<.070:continue
        move=delta.normalized()*min(.008,distance*.25)*max(0,1-distance/.08)
        changes.append({'vertex':v.index,'fromWorld':list(p),'toWorld':list(p+move),'movementMeters':move.length})
    parts.append({'object':name,'position':list(o.location),'rotationRadians':list(o.rotation_euler),'unchangedObjectTransform':True,'vertexChanges':changes})
plan={'source':'MortarRelief_v16/mortar-relief.blend','parts':parts,'maximumVertexMoveMeters':.008,'protectedElevationAbove':.285,'noAddedObjects':True,'limitations':'Local near-gap edges only. Intersections and broad residual gaps require actual clay review.'}
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
assert all(c['movementMeters']<=.00800001 and c['toWorld'][2]<.293 for part in parts for c in part['vertexChanges'])
assert all(part['object'] in allowed for part in parts)
for part in parts:
    o=bpy.data.objects[part['object']];inverse=o.matrix_world.inverted()
    for change in part['vertexChanges']:o.data.vertices[change['vertex']].co=inverse@Vector(change['toWorld'])
    o.data.update()
(out/'geometry-receipt.json').write_text(json.dumps({'changedObjects':[p['object'] for p in parts],'changedVertices':sum(len(p['vertexChanges']) for p in parts),'maximumMove':max((c['movementMeters'] for p in parts for c in p['vertexChanges']),default=0),'boundsGate':'PASS','fidelity':'CLAY_REVIEW_PENDING'},indent=2))
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'wall-joints.blend'));bpy.ops.render.render(write_still=True)
print('WALL_JOINT_CANDIDATE_SAVED')
