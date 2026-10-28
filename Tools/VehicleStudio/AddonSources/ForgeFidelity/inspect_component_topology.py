"""Inspect evaluated component topology and mounting openings, not visual quality."""
import bpy,bmesh,sys,json
from pathlib import Path
from mathutils import Vector
folder=Path(sys.argv[sys.argv.index('--')+1]);bpy.ops.wm.open_mainfile(filepath=str(folder/'forge.blend'))
deps=bpy.context.evaluated_depsgraph_get();results=[]
for original in bpy.context.scene.objects:
 if original.type!='MESH' or not original.name.startswith(('Hollow crucible','Reference thick open vent','Securing foot','Pleated fan leather')):continue
 obj=original.evaluated_get(deps);mesh=obj.to_mesh();bm=bmesh.new();bm.from_mesh(mesh)
 item={'name':original.name,'vertices':len(mesh.vertices),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges)}
 if item['nonManifoldEdges']==0:item['signedVolumeLocal']=bm.calc_volume(signed=True)
 if original.name.startswith('Securing foot'):
  x,y,z=original.location;hx=x+(.027 if x>0 else -.027);inverse=obj.matrix_world.inverted()
  origin=inverse@Vector((hx,y,z+.06));direction=(inverse.to_3x3()@Vector((0,0,-1))).normalized()
  hit,point,normal,index=obj.ray_cast(origin,direction,distance=.20)
  item['mountingHoleCentreClear']=not hit
 bm.free();obj.to_mesh_clear();results.append(item)
receipt={'status':'COMPONENT_TOPOLOGY_INSPECTION_NOT_FIDELITY_ACCEPTANCE','components':results}
(folder/'component-topology-audit.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
