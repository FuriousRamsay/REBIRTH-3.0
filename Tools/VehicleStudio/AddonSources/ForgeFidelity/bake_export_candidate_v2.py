"""Build UV atlas on reduced candidate and explicitly select BakeUV for bake output."""
from pathlib import Path
source=Path(__file__).with_name('bake_export_candidate.py').read_text()
# Reduction must happen before atlas generation, not after dense geometry has fragmented its UVs.
start=source.index(' if len(obj.data.polygons)>1000:')
end=source.index(" obj.data.calc_loop_triangles();after+=",start)
source=source[:start]+source[end:]
extra='''
source_triangles=0
for obj in meshes:
 bpy.context.view_layer.objects.active=obj
 for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 obj.data.calc_loop_triangles();source_triangles+=len(obj.data.loop_triangles)
 if len(obj.data.polygons)>1000:
  dec=obj.modifiers.new('Pre-atlas review reduction','DECIMATE');dec.ratio=.08
  bpy.ops.object.modifier_apply(modifier=dec.name)
print('PRE_ATLAS_REDUCTION_READY',flush=True)
'''
source=source.replace('materials=set(',extra+'\nmaterials=set(')
source=source.replace("obj.data.uv_layers.active=obj.data.uv_layers['BakeUV']", "obj.data.uv_layers.active=obj.data.uv_layers['BakeUV'];obj.data.uv_layers['BakeUV'].active_render=True")
source=source.replace("normal_space='TANGENT')", "normal_space='TANGENT',uv_layer='BakeUV')")
source=source.replace("print('ATLAS_UV_READY',flush=True)", "bpy.ops.wm.save_as_mainfile(filepath=str(out/'atlas-prepared.blend'));print('ATLAS_UV_READY',flush=True)")
source=source.replace("'trianglesBefore':before", "'trianglesSource':source_triangles,'trianglesBefore':before")
source=source.replace("'requires':'Bake inspection", "'surfaceDetail':'Reduced geometry plus original shader relief; must compare against high-resolution reference','requires':'Bake inspection")
exec(compile(source,__file__,'exec'))
