"""Correct cup floor topology: separate inner floor and outer underside disks."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v23')
if out.exists():raise RuntimeError('Preserve prior candidate')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v22/forge.blend')
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Hollow crucible')]:
 old=obj.data;materials=list(old.materials)
 verts=[tuple(v.co) for v in old.vertices];faces=[tuple(p.vertices) for p in list(old.polygons)[:96]]
 verts.append((-.19,-.29,.055));center=len(verts)-1
 for i in range(24):faces.append((center,(i+1)%24,i))
 mesh=bpy.data.meshes.new('Manifold refractory cup with floor thickness');mesh.from_pydata(verts,[],faces);mesh.update();obj.data=mesh
 for material in materials:mesh.materials.append(material)
 for polygon in mesh.polygons:
  if polygon.index<24 or 48<=polygon.index<72:polygon.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
cam=scene.camera;cam.location=(-.55,-1.05,.64);cam.rotation_euler=(Vector((0,-.10,.25))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=70
scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.filepath=str(out/'hearth-detail.png');bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'FLOOR_TOPOLOGY_REVIEW_NOT_ACCEPTED','source':'v22 saved scene','fix':'Removed triple-edge annulus and added distinct outer bottom disk; restored cup wall smooth flags'},indent=2))
