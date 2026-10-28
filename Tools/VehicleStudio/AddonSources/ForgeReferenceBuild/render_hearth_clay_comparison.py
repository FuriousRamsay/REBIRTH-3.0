"""Identical neutral-light views of saved before/after geometry."""
import bpy,json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'HearthLiningFit_v31';results=[]
for label,source in [('before',root/'MortarContactFit_v29/mortar-contact.blend'),('after',out/'hearth-lining-clay.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    for obj in list(scene.objects):
        if obj.type=='LIGHT' and obj.data.type=='POINT':bpy.data.objects.remove(obj,do_unlink=True)
    mat=bpy.data.materials.new('Neutral geometry inspection');mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.42,.42,.42,1);bs.inputs['Roughness'].default_value=.8
    for obj in scene.objects:
        if obj.type=='MESH' and obj.name!='Inspection ground':obj.data.materials.clear();obj.data.materials.append(mat)
    scene.cycles.device='CPU';scene.cycles.samples=32
    scene.render.filepath=str(out/f'neutral-{label}.png');assert not Path(scene.render.filepath).exists()
    bpy.ops.render.render(write_still=True);results.append({'label':label,'source':str(source),'image':scene.render.filepath})
(out/'neutral-comparison-receipt.json').write_text(json.dumps({'views':results,'lighting':'Existing studio area lights, all local point lights removed','materials':'One non-emissive neutral Principled material','fidelity':'VISUAL_REVIEW_REQUIRED'},indent=2))
