import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'BellowsFoldSurface_v36'
bpy.ops.wm.open_mainfile(filepath=str(out/'fold-surface-clay.blend'))
receipt=json.loads((root/'BellowsLeatherNormals_v35/closeup-receipt.json').read_text());cam=bpy.context.scene.camera;cam.location=receipt['cameraLocation'];cam.data.ortho_scale=receipt['orthoScale'];bpy.context.view_layer.update()
obj=bpy.data.objects['planned_mesh_000'];tree=BVHTree.FromObject(obj,bpy.context.evaluated_depsgraph_get());inverse=obj.matrix_world.inverted();direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1));records=[]
for x,y in [(580,580),(620,600),(660,610),(700,620),(740,635),(640,670)]:
    origin=cam.matrix_world@Vector(((x/1200-.5)*.52,(.5-y/900)*.39,0))
    p,n,face,d=tree.ray_cast(inverse@origin,(inverse.to_3x3()@direction).normalized())
    if p is None:records.append({'pixel':[x,y],'hit':False});continue
    indices=list(obj.data.polygons[face].vertices);curvatures=[]
    for index in indices:
        row,col=divmod(index,256)
        if not 0<row<40:continue
        current=obj.data.vertices[index].co
        along=obj.data.vertices[(row-1)*256+col].co+obj.data.vertices[(row+1)*256+col].co-current*2
        around=obj.data.vertices[row*256+(col-1)%256].co+obj.data.vertices[row*256+(col+1)%256].co-current*2
        curvatures.append({'vertex':index,'ring':row,'column':col,'alongSecondDifference':list(along),'aroundSecondDifference':list(around)})
    records.append({'pixel':[x,y],'position':list(p),'face':face,'indices':indices,'curvatures':curvatures})
(out/'pinch-audit.json').write_text(json.dumps({'source':'fold-surface-clay.blend','camera':'1200x900 closeup, saved receipt parameters','samples':records,'limits':'Ray samples locate visible mesh; second differences are local geometry diagnostics, not a validated new contour.'},indent=2))
print(json.dumps([{'pixel':r['pixel'],'face':r.get('face'),'vertices':r.get('indices')} for r in records]))
