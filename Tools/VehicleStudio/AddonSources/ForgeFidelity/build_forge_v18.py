"""Reference-driven hearth ledge, coal crust, vent collar and worn timber revision."""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector,noise
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v18')
if out.exists():raise RuntimeError('Preserve prior candidate')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/v17/forge.blend')
stone=bpy.data.materials['Rough dark fieldstone'];iron=bpy.data.materials['Pitted forged iron'];wood=bpy.data.materials['Worn oak rails'];ember=bpy.data.materials['Localized ember cracks']
texture=Path(__file__).with_name('Textures')/'wood-albedo-v1.png'
if not texture.exists():raise RuntimeError('Preserve authored wood texture before rendering')
image=bpy.data.images.load(str(texture));image.colorspace_settings.name='sRGB';image.pack()
# Grain follows each component's longest dimension; planar face UVs avoid box-projection streaks.
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH' and wood in list(o.data.materials)]:
 mesh=obj.data
 lo=[min(v.co[i] for v in mesh.vertices) for i in range(3)];hi=[max(v.co[i] for v in mesh.vertices) for i in range(3)]
 lengths=[hi[i]-lo[i] for i in range(3)];long=max(range(3),key=lambda i:lengths[i])
 uv=mesh.uv_layers.new(name='TimberGrainUV')
 for poly in mesh.polygons:
  normal_axis=max(range(3),key=lambda i:abs(poly.normal[i]))
  u=long if long!=normal_axis else next(i for i in range(3) if i!=normal_axis)
  v=next(i for i in range(3) if i!=normal_axis and i!=u)
  for loop in poly.loop_indices:
   p=mesh.vertices[mesh.loops[loop].vertex_index].co
   uv.data[loop].uv=((p[u]-lo[u])/max(lengths[u],.0001),(p[v]-lo[v])/max(lengths[v],.0001))
nodes=wood.node_tree.nodes;links=wood.node_tree.links;p=nodes.get('Principled BSDF')
tex=nodes.new('ShaderNodeTexImage');tex.image=image;uv=nodes.new('ShaderNodeUVMap');uv.uv_map='TimberGrainUV'
links.new(uv.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],p.inputs['Base Color'])
# Sparse irregular coal fissures replace the recognisable Voronoi web.
nodes=ember.node_tree.nodes;links=ember.node_tree.links;p=nodes.get('Principled BSDF')
field=nodes.new('ShaderNodeTexNoise');field.inputs['Scale'].default_value=5;field.inputs['Detail'].default_value=7;field.inputs['Roughness'].default_value=.8
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.interpolation='LINEAR'
ramp.color_ramp.elements[0].position=.44;ramp.color_ramp.elements[0].color=(0,0,0,1)
ramp.color_ramp.elements[1].position=.475;ramp.color_ramp.elements[1].color=(0,0,0,1)
peak=ramp.color_ramp.elements.new(.457);peak.color=(1,1,1,1)
power=nodes.new('ShaderNodeMath');power.operation='MULTIPLY';power.inputs[1].default_value=4
links.new(field.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],power.inputs[0]);links.new(power.outputs[0],p.inputs['Emission Strength'])
for light in [o for o in bpy.context.scene.objects if o.type=='LIGHT' and o.data.type=='POINT']:light.data.energy=2.4
random.seed(180)
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith(('Front hearth sill stone','Open iron exhaust collar')):bpy.data.objects.remove(obj,do_unlink=True)
def chipped_brick(name,loc,size):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);obj=bpy.context.object;obj.name=name;obj.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bevel=obj.modifiers.new('Worn hearth ledge edges','BEVEL');bevel.width=.009;bevel.segments=2;bpy.ops.object.modifier_apply(modifier=bevel.name)
 sub=obj.modifiers.new('Hearth ledge fracture relief','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=2;bpy.ops.object.modifier_apply(modifier=sub.name)
 for vertex in obj.data.vertices:vertex.co+=vertex.normal*.002*noise.noise(vertex.co*120)
 obj.data.materials.append(stone)
 for poly in obj.data.polygons:poly.use_smooth=True
 return obj
for i in range(3):chipped_brick('Thick front hearth ledge_%d'%i,(-.10+i*.10,-.20,.11),(.101,.095,.057))
# Short thick annular iron collar, with open centre and a recessed warm interior rim.
verts=[];rings=[(.080,.530),(.080,.584),(.056,.584),(.056,.530)]
for r,z in rings:
 for i in range(48):
  t=2*math.pi*i/48;verts.append((r*math.cos(t),r*math.sin(t),z))
faces=[]
for j in range(4):
 for i in range(48):faces.append((j*48+i,j*48+(i+1)%48,((j+1)%4)*48+(i+1)%48,((j+1)%4)*48+i))
mesh=bpy.data.meshes.new('Thick annular vent');mesh.from_pydata(verts,[],faces);mesh.update()
collar=bpy.data.objects.new('Reference thick open vent collar',mesh);bpy.context.collection.objects.link(collar);collar.data.materials.append(iron)
bevel=collar.modifiers.new('Vent rim eased edges','BEVEL');bevel.width=.002;bevel.segments=2
# New parts need explicit source texture UVs as well.
for obj in [o for o in bpy.context.scene.objects if o.name.startswith(('Thick front hearth ledge','Reference thick open vent'))]:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.cycles.samples=32;bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
for name,loc,target,lens,w,h in [('geometry-review',(-1.05,-1.8,1.05),(-.09,-.015,.29),60,1400,1100),('hearth-detail',(-.55,-1.05,.64),(0,-.10,.25),70,1100,900),('bellows-detail',(-.95,-.82,.62),(-.41,-.01,.30),70,1100,900)]:
 camera=scene.camera;camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=lens
 scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps({'status':'REFERENCE_FEATURE_REVISION_NOT_ACCEPTED','source':'v17 saved high-resolution scene','changed':['Worn timber image and aligned UV','Irregular coal fissures','Thick hearth ledge','Thick open vent collar'],'lightingChange':'Hearth point light2.4W; other saved studio values retained'},indent=2))
