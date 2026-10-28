"""Material-only comparison: larger mineral fracture pattern on unchanged geometry."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'StoneMaterialScale_v19';out.mkdir(parents=True,exist_ok=True)
plan={'source':'source-materials-fitted-v12.blend','geometryChanges':False,'stoneTextureScale':{'from':3.2,'to':1.0},'generatedAlbedoReliefMeters':.0035,'bumpStrength':.4,'albedoValueMultiplier':1.18,'saturation':.85,'roughness':.83,'purpose':'Larger fractured mineral areas rather than dense fine flecks; compare actual material render before baking','resolution':'Final bakes remain 2K; existing generated source input unchanged'}
(out/'texture-adjustment-plan.json').write_text(json.dumps(plan,indent=2))
bpy.ops.wm.open_mainfile(filepath=str(root/'source-materials-fitted-v12.blend'))
changed=[]
used={m for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render for m in o.data.materials if m}
for mat in used:
    if not mat.use_nodes:continue
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    tex=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image and 'stone-albedo-generated-v1' in n.image.filepath),None)
    if tex is None:continue
    spatial=tex.inputs['Vector'].links[0].from_node
    assert spatial.type=='VECT_MATH' and spatial.operation=='SCALE'
    spatial.inputs[3].default_value=1.0
    for node in nodes:
        if node.type=='BUMP' and node.inputs['Height'].is_linked and node.inputs['Height'].links[0].from_node==tex:
            node.inputs['Strength'].default_value=.4;node.inputs['Distance'].default_value=.0035
    # Alter the input image colour before existing per-object tint and hearth soot.
    outputs=list(tex.outputs['Color'].links)
    color=nodes.new('ShaderNodeHueSaturation');color.inputs['Saturation'].default_value=.85;color.inputs['Value'].default_value=1.18
    links.new(tex.outputs['Color'],color.inputs['Color'])
    for link in outputs:
        if link.to_node.type!='BUMP':links.new(color.outputs['Color'],link.to_socket)
    nodes.get('Principled BSDF').inputs['Roughness'].default_value=.83
    changed.append(mat.name)
assert changed
(out/'material-change-receipt.json').write_text(json.dumps({'changedMaterials':changed,'fidelity':'RENDER_REVIEW_PENDING','geometryChanges':False},indent=2))
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.filepath=str(out/'reference-material.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'stone-material.blend'));bpy.ops.render.render(write_still=True)
print('STONE_MATERIAL_COMPARISON_SAVED')
