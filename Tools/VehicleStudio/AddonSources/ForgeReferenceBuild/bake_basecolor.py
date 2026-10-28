"""Bake source albedo only, with lighting excluded, to a 2048px atlas."""
import bpy, json
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'Baked2K_v1'; out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'uv-prepared-v9.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and o.name!='Inspection ground']
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
image=bpy.data.images.new('Forge_BaseColor_2K',width=2048,height=2048,alpha=False)
image.colorspace_settings.name='sRGB'
materials={m for o in objects for m in o.data.materials if m}
for m in materials:
    n=m.node_tree.nodes.new('ShaderNodeTexImage'); n.image=image; n.name='BAKE_TARGET_BASECOLOR'; m.node_tree.nodes.active=n
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=1
sc.render.bake.use_pass_direct=False; sc.render.bake.use_pass_indirect=False; sc.render.bake.use_pass_color=True
sc.render.bake.margin=8; sc.render.bake.use_clear=True
(out/'bake-state.json').write_text(json.dumps({'status':'RUNNING','resolution':[2048,2048],'objects':len(objects)}))
bpy.ops.object.bake(type='DIFFUSE')
image.filepath_raw=str(out/'forge-basecolor-2k.png'); image.file_format='PNG'; image.save()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'source-with-basecolor-bake.blend'))
(out/'bake-state.json').write_text(json.dumps({'status':'BASECOLOR_SAVED_REVIEW_PENDING','resolution':list(image.size),'objects':len(objects),'lightingIncluded':False,'remaining':['normal','roughness','metallic','emission','baked-scene visual comparison','Unity verification']},indent=2))
print('BASECOLOR_BAKE_SAVED')
