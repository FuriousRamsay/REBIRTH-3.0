"""Audit real saved Blender state; numerical receipts do not certify visual fidelity."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
folder=Path(args[0]);bpy.ops.wm.open_mainfile(filepath=str(folder/'forge.blend'))
scene=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get()
meshes=[];corners=[]
for obj in scene.objects:
 if obj.type!='MESH':continue
 evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
 meshes.append({'name':obj.name,'triangles':len(mesh.loop_triangles),'materials':[m.name if m else None for m in obj.data.materials]})
 corners.extend([list(obj.matrix_world@Vector(c)) for c in obj.bound_box]);evaluated.to_mesh_clear()
materials=[]
for material in bpy.data.materials:
 if not material.use_nodes:continue
 p=material.node_tree.nodes.get('Principled BSDF')
 if p is None:continue
 materials.append({'name':material.name,'colorLink':[l.from_node.name for l in p.inputs['Base Color'].links],
  'normalLink':[l.from_node.name for l in p.inputs['Normal'].links],
  'images':[{'name':n.image.name,'colorSpace':n.image.colorspace_settings.name,'packed':bool(n.image.packed_file),'projection':n.projection} for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]})
receipt={'status':'SAVED_SCENE_AUDIT_NOT_FIDELITY_ACCEPTANCE','boundsMin':[min(c[i] for c in corners) for i in range(3)],'boundsMax':[max(c[i] for c in corners) for i in range(3)],
 'meshCount':len(meshes),'triangles':sum(x['triangles'] for x in meshes),'meshes':meshes,'materials':materials,
 'camera':{'location':list(scene.camera.location),'lens':scene.camera.data.lens},'display':scene.view_settings.view_transform,'exposure':scene.view_settings.exposure,
 'lights':[{'name':o.name,'type':o.data.type,'power':o.data.energy,'position':list(o.location)} for o in scene.objects if o.type=='LIGHT']}
(folder/'saved-scene-audit.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps({k:v for k,v in receipt.items() if k not in ['meshes','materials','lights']}))
