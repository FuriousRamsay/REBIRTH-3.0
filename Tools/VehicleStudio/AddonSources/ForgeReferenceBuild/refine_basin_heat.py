import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'BasinHeat_v25';out.mkdir(parents=True,exist_ok=True)
(out/'texture-plan.json').write_text(json.dumps({'source':'CombinedMaterial_v23/combined-material.blend','geometry':'BasinGeometry_v24/basin-clay.blend: Irregular refractory basin','noiseScale':9,'darkThreshold':.48,'hotThreshold':.62,'emissionStrength':2.8,'previewLightWatts':.25,'purpose':'Visible charcoal patches and hot crevices instead of uniformly orange disk','stage':'Source comparison; not final baked map'},indent=2))
bpy.ops.wm.open_mainfile(filepath=str(root/'CombinedMaterial_v23/combined-material.blend'))
o=bpy.data.objects['collar_inner_basin'];material=o.data.materials[0]
with bpy.data.libraries.load(str(root/'BasinGeometry_v24/basin-clay.blend'),link=False) as (source,target):
    target.meshes=['Irregular refractory basin']
o.data=target.meshes[0];o.data.materials.clear();o.data.materials.append(material)
for modifier in list(o.modifiers):o.modifiers.remove(modifier)
nodes=material.node_tree.nodes;bs=nodes.get('Principled BSDF');bs.inputs['Emission Strength'].default_value=2.8
noise=next(n for n in nodes if n.type=='TEX_NOISE');noise.inputs['Scale'].default_value=9
ramp=next(n for n in nodes if n.type=='VALTORGB')
ramp.color_ramp.elements[0].position=.48;ramp.color_ramp.elements[0].color=(.003,.0002,0,1)
ramp.color_ramp.elements[1].position=.62;ramp.color_ramp.elements[1].color=(1,.070,.002,1)
lamp=bpy.data.objects['Collar basin warm illumination'];lamp.data.energy=.25
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.filepath=str(out/'reference-material.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'basin-heat.blend'));bpy.ops.render.render(write_still=True)
print('BASIN_HEAT_COMPARISON_SAVED')
