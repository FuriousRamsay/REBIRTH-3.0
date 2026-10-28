import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrucibleRimBreaks_v70';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SidePatchVolume_v62/side-patch-volume-clay.blend'));obj=bpy.data.objects['planned_mesh_002'];points=[obj.matrix_world@v.co for v in obj.data.vertices]
top=max(p.z for p in points);cx=(min(p.x for p in points)+max(p.x for p in points))/2;cy=(min(p.y for p in points)+max(p.y for p in points))/2
chips=[{'angleDegrees':a,'halfWidthDegrees':w,'depthMeters':d} for a,w,d in [(-155,12,.004),(-110,10,.008),(-50,9,.005),(10,12,.006),(95,10,.004),(150,11,.007)]];changes=[]
for vertex,p in zip(obj.data.vertices,points):
    if p.z<top-.030:continue
    theta=math.atan2(p.y-cy,p.x-cx);weight=max(0,min(1,(p.z-(top-.030))/.030));drop=0
    for chip in chips:
        difference=abs((theta-math.radians(chip['angleDegrees'])+math.pi)%math.tau-math.pi);width=math.radians(chip['halfWidthDegrees'])
        if difference<width:drop=max(drop,chip['depthMeters']*(1-difference/width))
    radial=.002*(math.sin(theta*7)+.4*math.sin(theta*13))*weight
    target=p+Vector((math.cos(theta)*radial,math.sin(theta)*radial,-drop*weight));changes.append({'index':vertex.index,'from':list(p),'to':list(target)})
assert changes
plan={'source':'SidePatchVolume_v62/side-patch-volume-clay.blend','object':obj.name,'centerXY':[cx,cy],'topZ':top,'chips':chips,'affectedHeightMeters':.030,'method':'Six bounded irregular breaks in upper30 mm of existing crucible wall; measured per-vertex changes. Radial variation max2.8 mm, depth max8 mm. Pose, hollow interior, floor and hardware fixed. Angles/detail inferred from reference style, not exact hidden geometry.','vertexChanges':changes,'status':'CLAY_REVIEW_REQUIRED'}
path=out/'geometry-plan.json';assert not path.exists();path.write_text(json.dumps(plan,indent=2));print(json.dumps({'object':obj.name,'dimensions':list(obj.dimensions),'topZ':top,'plannedVertices':len(changes),'maxMoveMeters':max((Vector(c['to'])-Vector(c['from'])).length for c in changes)}))
