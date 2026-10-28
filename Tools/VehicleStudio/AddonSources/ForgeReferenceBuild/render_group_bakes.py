"""Reopen disk maps and render model using three baked 2K material groups."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'GroupedBaked2K_v3'
bpy.ops.wm.open_mainfile(filepath=str(root/'group-atlas-source.blend'))
groups=json.loads((root/'group-atlas-plan.json').read_text())['groups'];report=[]
for group,names in groups.items():
    mat=bpy.data.materials.new('Forge_Final2K_'+group);mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
    for channel in ['normal','basecolor','roughness','metallic','emission']:
        path=out/group/f'forge-{channel}-2k.png';assert path.exists(),str(path)
        image=bpy.data.images.load(str(path),check_existing=False);assert tuple(image.size)==(2048,2048)
        image.colorspace_settings.name='sRGB' if channel in ('basecolor','emission') else 'Non-Color'
        tex=nodes.new('ShaderNodeTexImage');tex.image=image
        if channel=='normal':
            norm=nodes.new('ShaderNodeNormalMap');links.new(tex.outputs['Color'],norm.inputs['Color']);links.new(norm.outputs[0],bs.inputs['Normal'])
        else:links.new(tex.outputs['Color'],bs.inputs[{'basecolor':'Base Color','roughness':'Roughness','metallic':'Metallic','emission':'Emission Color'}[channel]])
        report.append({'group':group,'channel':channel,'path':str(path),'size':list(image.size),'colorSpace':image.colorspace_settings.name})
    bs.inputs['Emission Strength'].default_value=2.8
    for name in names:
        o=bpy.data.objects[name];o.data.materials.clear();o.data.materials.append(mat)
    receiptPath=out/group/'bake-state.json'
    receipt=json.loads(receiptPath.read_text())
    if 'groupRenderProduced' not in receipt:
        (out/group/'legacy-receipt-before-status-correction.json').write_text(receiptPath.read_text())
        receipt.update(status='MAPS_SAVED_REVIEW_PENDING',group=group,groupRenderProduced=False,statusCorrection='Per-group render was intentionally skipped; combined renderer is separate.')
        receiptPath.write_text(json.dumps(receipt,indent=2))
sc=bpy.context.scene;sc.cycles.samples=48;sc.render.filepath=str(out/'grouped-baked-reference.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'grouped-baked-only.blend'));bpy.ops.render.render(write_still=True)
(out/'disk-map-verification.json').write_text(json.dumps({'maps':report,'allMapsReopened':True,'resolutionVerified':True,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2));print('GROUPED_BAKED_RENDER_SAVED')
