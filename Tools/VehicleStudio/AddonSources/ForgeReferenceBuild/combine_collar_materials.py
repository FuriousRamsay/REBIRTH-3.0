"""Combine individually inspected collar geometry and larger-scale stone material."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'CombinedMaterial_v23';out.mkdir(parents=True,exist_ok=True)
plan={'geometrySource':'CollarDetails_v22/collar-details.blend','materialSource':'StoneMaterialScale_v19/stone-material.blend','addedObjects':['collar_front_bolt_plate','collar_front_washer','collar_front_bolt','collar_inner_basin','collar_inner_plug'],'ironRoughness':.64,'ironWearColor':[.10,.085,.07],'ironWearRampMax':.30,'basinEmissionStrength':1.1,'basinPreviewPointWatts':.8,'fidelity':'SOURCE_RENDER_REVIEW_BEFORE_2K_BAKES','preservePriorFiles':True}
(out/'texture-plan.json').write_text(json.dumps(plan,indent=2))
bpy.ops.wm.open_mainfile(filepath=str(root/plan['materialSource']))
iron=bpy.data.objects['planned_mesh_003'].data.materials[0]
for mat in {m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m}:
    if not mat.use_nodes:continue
    nodes=mat.node_tree.nodes
    if not any(n.type=='TEX_IMAGE' and n.image and 'iron-albedo-generated-v1' in n.image.filepath for n in nodes):continue
    bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.64
    for node in nodes:
        if node.type=='VALTORGB' and node.inputs[0].is_linked and node.inputs[0].links[0].from_node.type=='NEW_GEOMETRY':
            node.color_ramp.elements[-1].color=(.30,.30,.30,1)
        if node.type=='MIX_RGB' and node.inputs[0].is_linked and node.inputs[0].links[0].from_node.type=='VALTORGB':
            ramp=node.inputs[0].links[0].from_node
            if ramp.inputs[0].is_linked and ramp.inputs[0].links[0].from_node.type=='NEW_GEOMETRY':node.inputs[2].default_value=(.10,.085,.07,1)
with bpy.data.libraries.load(str(root/plan['geometrySource']),link=False) as (source,target):
    assert all(name in source.objects for name in plan['addedObjects'])
    target.objects=plan['addedObjects']
for o in target.objects:
    bpy.context.scene.collection.objects.link(o);o.data.materials.clear()
    if o.name=='collar_inner_basin':
        material=bpy.data.materials.new('Forge_heated_basin');material.use_nodes=True
        nodes=material.node_tree.nodes;links=material.node_tree.links;bs=nodes.get('Principled BSDF')
        bs.inputs['Base Color'].default_value=(.07,.024,.008,1);bs.inputs['Roughness'].default_value=.9
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=90;noise.inputs['Detail'].default_value=4
        ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.22,.010,.001,1);ramp.color_ramp.elements[1].color=(1,.12,.003,1)
        links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1.1
        bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.001;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
        o.data.materials.append(material)
        light=bpy.data.lights.new('Collar basin warm illumination','POINT');light.energy=.8;light.color=(1,.12,.018);light.shadow_soft_size=.035
        lamp=bpy.data.objects.new(light.name,light);bpy.context.scene.collection.objects.link(lamp);lamp.location=o.location;lamp.location.z+=.028
    else:o.data.materials.append(iron)
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.filepath=str(out/'reference-material.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'combined-material.blend'));bpy.ops.render.render(write_still=True)
print('COMBINED_SOURCE_MATERIAL_RENDER_SAVED')
