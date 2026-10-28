"""Inspect actual generated GLB in Blender from multiple directions; no quality pass."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
depth=int(sys.argv[sys.argv.index('--depth')+1]) if '--depth' in sys.argv else 8
seed=int(sys.argv[sys.argv.index('--seed')+1]) if '--seed' in sys.argv else 42
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
source=trial/'Outputs'/f'forge-depth{depth}-seed{seed}'/'forge-generated.glb'
if '--source' in sys.argv:source=Path(sys.argv[sys.argv.index('--source')+1])
assert source.exists(),str(source)
out=trial/'Outputs'/f'forge-depth{depth}-seed{seed}'/'ClayReview'
if '--output-dir' in sys.argv:out=Path(sys.argv[sys.argv.index('--output-dir')+1])
assert out.resolve().is_relative_to(trial.resolve()),'Review output must stay within isolated E trial'
assert not (out/'render-receipt.json').exists(),'Preserve previous review; choose a fresh output directory'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert objects
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
low=Vector([min(p[k] for p in points) for k in range(3)]);high=Vector([max(p[k] for p in points) for k in range(3)]);target=(low+high)/2;size=high-low
mat=bpy.data.materials.new('Neutral clay');mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.48,.48,.48,1);bs.inputs['Roughness'].default_value=.78
for o in objects:o.data.materials.clear();o.data.materials.append(mat)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,low.z-.015));floor=bpy.context.object
ground=bpy.data.materials.new('Studio ground');ground.use_nodes=True;ground.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.08,.085,.09,1);ground.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9;floor.data.materials.append(ground)
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.resolution_x=1448;sc.render.resolution_y=1086;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Review world');sc.world.use_nodes=True;sc.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.3
for name,offset,power,area in [('Key',(-3,-4,5),700,4),('Fill',(4,2,3),450,3)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=area
    lamp=bpy.data.objects.new(name,light);sc.collection.objects.link(lamp);lamp.location=target+Vector(offset);lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.cameras.new('Clay review camera');cam=bpy.data.objects.new(camera.name,camera);sc.collection.objects.link(cam);sc.camera=cam
camera.type='ORTHO';camera.ortho_scale=max(size.x,size.y,size.z)*1.7
views=[]
for angle in [45,135,225,315]:
    radians=math.radians(angle);cam.location=target+Vector((math.cos(radians)*5,math.sin(radians)*5,2.7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    path=out/f'clay-angle-{angle}.png';sc.render.filepath=str(path);bpy.ops.render.render(write_still=True);views.append(str(path))
cam.location=target+Vector((.001,-.001,5));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(out/'clay-top.png');bpy.ops.render.render(write_still=True);views.append(sc.render.filepath)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'generated-clay-review.blend'))
report={'source':str(source),'meshObjects':len(objects),'vertices':sum(len(o.data.vertices) for o in objects),'polygons':sum(len(o.data.polygons) for o in objects),'boundsMin':list(low),'boundsMax':list(high),'images':views,'fidelity':'VISUAL_REVIEW_REQUIRED','scope':'Actual generated shape only. No textures or installation; camera directions do not assert front orientation.'}
(out/'render-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
