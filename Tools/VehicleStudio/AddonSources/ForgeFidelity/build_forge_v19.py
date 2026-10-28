"""Chipped rims, refractory cup material and explicit front strip UVs."""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v19')
if out.exists():raise RuntimeError('Preserve prior candidate')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v18/forge.blend')
random.seed(190)
stone=bpy.data.materials['Rough dark fieldstone'];iron=bpy.data.materials['Pitted forged iron']
cup_material=stone.copy();cup_material.name='Sooted refractory crucible'
nodes=cup_material.node_tree.nodes;links=cup_material.node_tree.links;p=nodes.get('Principled BSDF')
grain=nodes.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=18;grain.inputs['Detail'].default_value=7
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.023,.015,.008,1);ramp.color_ramp.elements[1].color=(.24,.16,.075,1)
links.new(grain.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.92
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Hollow crucible')]:
 obj.data.materials.clear();obj.data.materials.append(cup_material)
 for vertex in obj.data.vertices:
  x,y,z=vertex.co;angle=math.atan2(y+.29,x+.19)
  if z>.17:vertex.co.z-=.005*(1+math.sin(angle*5.3))+.007*max(0,math.cos(angle*9.1))
  elif z<.08:vertex.co.z+=.002*math.sin(angle*7)
 # Close the previously open floor with an actual refractory bottom.
 mesh=obj.data;verts=[tuple(v.co) for v in mesh.vertices];faces=[tuple(poly.vertices) for poly in mesh.polygons]
 verts.append((-.19,-.29,.07));center=len(verts)-1
 for i in range(24):faces.append((center,72+i,72+(i+1)%24))
 replacement=bpy.data.meshes.new('Hollow crucible with real floor');replacement.from_pydata(verts,[],faces);replacement.update()
 obj.data=replacement;obj.data.materials.append(cup_material)
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Reference thick open vent')]:
 # Matching inner/outer notch elevations keep the lip thickness coherent.
 for vertex in obj.data.vertices:
  if vertex.co.z>.575:
   angle=math.atan2(vertex.co.y,vertex.co.x)
   vertex.co.z-=.002*(1+math.sin(angle*7))+.003*max(0,math.cos(angle*11))
 # Recessed warm inner wall, separate from exterior rusty iron.
 hot=iron.copy();hot.name='Vent recessed hot inner iron';shader=hot.node_tree.nodes.get('Principled BSDF')
 shader.inputs['Emission Color'].default_value=(.7,.10,.007,1);shader.inputs['Emission Strength'].default_value=.6
 obj.data.materials.append(hot)
 for poly in obj.data.polygons:
  if 96<=poly.index<144:poly.material_index=1
# Arc-length strip UV: each width pair shares the same length coordinate.
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Profile fitted front strap')]:
 mesh=obj.data;layer=mesh.uv_layers.active or mesh.uv_layers.new(name='StripUV')
 centers=[(mesh.vertices[i].co+mesh.vertices[i+1].co)/2 for i in range(0,len(mesh.vertices),2)]
 distances=[0.0]
 for i in range(1,len(centers)):distances.append(distances[-1]+(centers[i]-centers[i-1]).length)
 for poly in mesh.polygons:
  for loop in poly.loop_indices:
   index=mesh.loops[loop].vertex_index;layer.data[loop].uv=(distances[index//2]/max(distances[-1],.0001),float(index%2)*.16)
# New cup floor needs texture coordinates; strips keep their authored UVs.
for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Hollow crucible')]:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
for name,loc,target,lens,w,h in [('geometry-review',(-1.05,-1.8,1.05),(-.09,-.015,.29),60,1400,1100),('hearth-detail',(-.55,-1.05,.64),(0,-.10,.25),70,1100,900)]:
 cam=scene.camera;cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
 scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'RIM_AND_STRIP_REVIEW_NOT_ACCEPTED','source':'v18 saved scene','changed':['Crucible chipped rim and actual floor','Refractory cup material','Vent lip notches and recessed warm iron','Arc-length front strip UV'],'lights':'Unchanged v18 studio lights'},indent=2))
