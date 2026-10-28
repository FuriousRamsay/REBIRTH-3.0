"""Compare the reduced geometry using unchanged source materials and lighting."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);out=Path(args[1])
if out.exists():raise RuntimeError('Preserve comparison renders')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath=str(folder/'atlas-prepared.blend'))
scene=bpy.context.scene;camera=scene.camera;scene.cycles.samples=32
scene.render.filepath=str(out/'reduced-full.png');bpy.ops.render.render(write_still=True)
camera.location=(-.95,-.82,.62);camera.rotation_euler=(Vector((-.41,-.01,.30))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.lens=70;scene.render.resolution_x=1100;scene.render.resolution_y=900
scene.render.filepath=str(out/'reduced-bellows.png');bpy.ops.render.render(write_still=True)
(out/'comparison-receipt.json').write_text(json.dumps({'status':'GEOMETRY_REDUCTION_COMPARISON_NOT_FIDELITY_PASS','source':str(folder),'materials':'Original authored graphs with explicit SourceUV bindings','lighting':'Unchanged saved source lighting','maps':'Not baked material proof'},indent=2))
