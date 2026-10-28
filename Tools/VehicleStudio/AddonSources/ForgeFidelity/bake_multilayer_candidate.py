"""Preserve explicit source UV layers; only BakeUV is exported as target UV0."""
from pathlib import Path
source=Path(__file__).with_name('bake_export_candidate_v2.py').read_text()
source=source.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(source,__file__,'exec'))
old=""" if not obj.data.uv_layers:obj.data.uv_layers.new(name='SourceUV')
 else:obj.data.uv_layers.active.name='SourceUV'
"""
new=""" if not obj.data.uv_layers:obj.data.uv_layers.new(name='OriginalUV')
 original=next((u for u in obj.data.uv_layers if u.active_render),obj.data.uv_layers.active)
 saved=obj.data.uv_layers.new(name='SourceUV')
 for i,item in enumerate(original.data):saved.data[i].uv=item.uv.copy()
"""
if old not in source:raise RuntimeError('UV preparation changed; re-audit source')
source=source.replace(old,new)
source=source.replace(" obj.data.uv_layers.remove(obj.data.uv_layers['SourceUV'])", " for layer in list(obj.data.uv_layers):\n  if layer.name!='BakeUV':obj.data.uv_layers.remove(layer)")
# User requirement: all new baked texture maps remain at 2048 square.
# The underlying bake source already creates 2048-square maps.
source=source.replace("bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.003)", "bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.003)\nbpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.0015)")
source=source.replace('scene.render.bake.margin=12','scene.render.bake.margin=3')
source=source.replace("out/'baked-high.blend'","out/'baked-source.blend'")
# Emit retains linear HDR intensity; PNG would clip values above one.
source=source.replace("alpha=False)","alpha=False,float_buffer=True)")
source=source.replace("'sRGB' if label in ['albedo','emission'] else 'Non-Color'", "'sRGB' if label=='albedo' else 'Non-Color'")
old="image.filepath_raw=str(out/(label+'.png'));image.file_format='PNG';image.save();targets[label]=image"
new="image.filepath_raw=str(out/(label+('.exr' if label=='emission' else '.png')));image.file_format='OPEN_EXR' if label=='emission' else 'PNG';scene.render.image_settings.color_depth='16';image.save();targets[label]=image"
if old not in source:raise RuntimeError('Bake save semantics changed; re-audit')
source=source.replace(old,new)
exec(compile(source,__file__,'exec'))
