"""Bake the independently inspected/repacked prepared atlas; do not regenerate its UVs."""
from pathlib import Path
source=Path(__file__).with_name('bake_export_candidate.py').read_text()
source=source.replace("folder/'forge.blend'", "folder/'atlas-prepared.blend'")
start=source.index('# Keep the source UV')
end=source.index('targets={}',start)
source=source[:start]+source[end:]
start=source.index(' if len(obj.data.polygons)>1000:')
end=source.index(' obj.data.calc_loop_triangles();after+=',start)
source=source[:start]+source[end:]
source=source.replace("normal_space='TANGENT')", "normal_space='TANGENT',uv_layer='BakeUV')")
source=source.replace('scene.render.bake.margin=12','scene.render.bake.margin=2')
exec(compile(source,__file__,'exec'))
