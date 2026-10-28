import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownHeadContact_v42'
bpy.ops.wm.open_mainfile(filepath=str(root/'CrownRivets_v41/crown-rivets-clay.blend'))
names=['harness_left_front_strap','harness_right_front_strap','harness_crown_cross_band','harness_crown_diagonal_left'];deps=bpy.context.evaluated_depsgraph_get();records=[]
for name in ('harness_crown_left_cross','harness_crown_front_cross','harness_crown_right_cross'):
    obj=bpy.data.objects[name];points=[obj.matrix_world@v.co for v in obj.data.vertices];base=sum(points[:48],Vector())/48;top=sum(points[-48:],Vector())/48;axis=(top-base).normalized();candidates=[]
    for bandName in names:
        band=bpy.data.objects[bandName];tree=BVHTree.FromObject(band,deps);inverse=band.matrix_world.inverted()
        hit,n,index,d=tree.ray_cast(inverse@(base+axis*.12),(inverse.to_3x3()@(-axis)).normalized(),.25)
        near,nn,idx,distance=tree.find_nearest(inverse@base)
        candidates.append({'band':bandName,'axialHit':hit is not None,'hitPoint':list(band.matrix_world@hit) if hit is not None else None,'nearestDistanceMeters':distance,'nearestNormalAlignment':(band.matrix_world.to_3x3()@nn).normalized().dot(axis) if near is not None else None})
    records.append({'object':name,'base':list(base),'axis':list(axis),'candidates':candidates})
path=out/'all-band-contact-candidates.json';assert not path.exists();path.write_text(json.dumps(records,indent=2));print(json.dumps(records))
