"""Direct comparison for user-reported invisible mortar and front-only ironwork."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'VisibleMortarIronwork_v27';out.mkdir(parents=True,exist_ok=True)
plan={'materialSource':'BasinHeat_v25/basin-heat.blend','rearIronSource':'RearIronwork_v26/rear-ironwork-clay.blend','mortarGeometrySource':'MortarRelief_v16/mortar-relief.blend','stoneGeometryChanges':False,'mortarAlbedoLinearRange':[[.035,.032,.027],[.11,.105,.09]],'mortarRoughness':.95,'mortarBumpDistanceMeters':.003,'purpose':'Show real recessed mortar geometry separately from dark stone and carry fitted iron onto rear','gate':'Actual front and rear render review required; no baking or installation'}
(out/'comparison-plan.json').write_text(json.dumps(plan,indent=2))
bpy.ops.wm.open_mainfile(filepath=str(root/plan['materialSource']))
sc=bpy.context.scene;iron=bpy.data.objects['planned_mesh_003'].data.materials[0]
oldCore=bpy.data.objects['furnace_core'];mortar=oldCore.data.materials[0]
with bpy.data.libraries.load(str(root/plan['rearIronSource']),link=False) as (source,target):
    names=[name for name in source.objects if name.startswith('harness_rear_')];assert len(names)==10
    target.objects=names
for o in target.objects:sc.collection.objects.link(o);o.data.materials.clear();o.data.materials.append(iron)
with bpy.data.libraries.load(str(root/plan['mortarGeometrySource']),link=False) as (source,target):target.objects=['furnace_core']
newCore=target.objects[0];sc.collection.objects.link(newCore)
assert all(abs(newCore.matrix_world[i][j]-oldCore.matrix_world[i][j])<1e-5 for i in range(4) for j in range(4))
oldCore.name='Preserved core before mortar relief';oldCore.hide_render=True;oldCore.hide_set(True)
newCore.name='furnace_core';newCore.data.materials.clear();newCore.data.materials.append(mortar)
nodes=mortar.node_tree.nodes;bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.95
ramps=[n for n in nodes if n.type=='VALTORGB'];assert len(ramps)==1
ramps[0].color_ramp.elements[0].color=(.035,.032,.027,1);ramps[0].color_ramp.elements[-1].color=(.11,.105,.09,1)
for n in nodes:
    if n.type=='BUMP':n.inputs['Distance'].default_value=.003;n.inputs['Strength'].default_value=.55
sc.cycles.device='CPU';sc.cycles.samples=48;sc.render.filepath=str(out/'reference-material.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'visible-mortar-ironwork.blend'));bpy.ops.render.render(write_still=True)
from mathutils import Vector
target=Vector((.03,.43,.48));cam=sc.camera;cam.location=target+Vector((.9,1.7,.9));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.0
sc.render.filepath=str(out/'rear-material.png');bpy.ops.render.render(write_still=True)
print('VISIBLE_MORTAR_AND_REAR_IRON_COMPARISON_SAVED')
