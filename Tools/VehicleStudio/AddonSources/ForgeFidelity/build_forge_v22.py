"""Close crucible underside and make the bellows leather a correctly oriented thin shell."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v22')
if out.exists():raise RuntimeError('Preserve prior candidate')
out.mkdir(parents=True);bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v21/forge.blend')
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Hollow crucible')]:
 mesh=obj.data;verts=[tuple(v.co) for v in mesh.vertices];faces=[tuple(p.vertices) for p in mesh.polygons]
 for i in range(24):faces.append((i,72+i,72+(i+1)%24,(i+1)%24))
 new=bpy.data.meshes.new('Closed refractory cup');new.from_pydata(verts,[],faces);new.update()
 materials=list(mesh.materials);obj.data=new
 for material in materials:new.materials.append(material)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Pleated fan leather')]:
 # The two sides need opposite outward normals. Source Cycles views did not prove engine culling.
 mean_y=sum(v.co.y for v in obj.data.vertices)/len(obj.data.vertices)
 normal_y=sum(p.normal.y*p.area for p in obj.data.polygons)
 if mean_y*normal_y<0:
  bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
 solid=obj.modifiers.new('Physical leather thickness','SOLIDIFY');solid.thickness=.0015;solid.offset=-1;solid.use_even_offset=True
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
cam=scene.camera;cam.location=(-.95,-.82,.62);cam.rotation_euler=(Vector((-.41,-.01,.30))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=70
scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.filepath=str(out/'bellows-detail.png');bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'TOPOLOGY_CORRECTION_NOT_FIDELITY_ACCEPTANCE','source':'v21 saved scene','changes':['Closed crucible underside annulus','Corrected outward leather orientation','1.5mm leather shell'],'maps':'Existing v21 bake is a baseline diagnostic, not current v22 export'},indent=2))
