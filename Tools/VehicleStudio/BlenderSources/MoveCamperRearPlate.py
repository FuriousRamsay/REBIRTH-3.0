"""Move only the rear half of the camper's existing license-plate mesh.

The Blender source is a geometry reference.  Unity applies the recorded world-X
delta to the current cumulative prefab, preserving its UVs and materials.
"""
import bpy
import json
from pathlib import Path

output = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearPlate_20261023')
source = output / 'camper-rear-plate.blend'
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
matches = [obj for obj in bpy.data.objects if obj.type == 'MESH' and obj.name.endswith('/licence_plate')]
if len(matches) != 1:
    raise RuntimeError(f'Expected one plate mesh, found {len(matches)}')
plate = matches[0]
# RenderOriginalCamperClay maps Unity (x,y,z) to Blender (x,-z,y).
rear = [vertex for vertex in plate.data.vertices if vertex.co.y > 3.0]
if len(rear) != 64 or len(plate.data.vertices) != 128:
    raise RuntimeError(f'Unexpected rear plate topology: {len(rear)}/{len(plate.data.vertices)}')
old_min = min(v.co.x for v in rear)
old_max = max(v.co.x for v in rear)
if not (-0.84 < old_min < -0.81 and -0.54 < old_max < -0.51):
    raise RuntimeError(f'Unexpected rear plate bounds: {old_min}..{old_max}')
target_center = -0.825
delta_x = target_center - (old_min + old_max) / 2
for vertex in rear:
    vertex.co.x += delta_x
plate.data.update()
receipt = {
    'source': str(source), 'mesh': plate.name,
    'rearVertices': len(rear), 'allVertices': len(plate.data.vertices),
    'originalX': [old_min, old_max], 'deltaX': delta_x,
    'finalX': [min(v.co.x for v in rear), max(v.co.x for v in rear)],
    'frontVerticesUnchanged': True,
}
bpy.ops.wm.save_as_mainfile(filepath=str(output / 'camper-rear-plate-final.blend'))
(output / 'plate-delta.json').write_text(json.dumps(receipt, indent=2) + '\n')
print('CAMPER_REAR_PLATE', json.dumps(receipt))
