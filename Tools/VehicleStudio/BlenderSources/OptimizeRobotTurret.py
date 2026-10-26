"""Reduce the separated turret, bake normals and export three articulated LODs."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019')
OUT=P/'Output'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'RobotTurret_Separated.blend'))
sc=bpy.context.scene
high=[bpy.data.objects[n] for n in ('FixedSupport','YawCradle','PitchBody')]
source=bpy.data.objects['SOURCE_283376']
source.hide_render=True; source.hide_set(True)
oldmat=high[0].data.materials[0]
cap_material=bpy.data.materials['Concealed bearing steel']
cap_bake=bpy.data.images.new('Unused cap bake',width=16,height=16)
cap_node=cap_material.node_tree.nodes.new('ShaderNodeTexImage');cap_node.image=cap_bake
cap_material.node_tree.nodes.active=cap_node
images={}
for node in oldmat.node_tree.nodes:
    if node.type=='TEX_IMAGE':
        for token in ('basecolor','roughness','metallic','normal'):
            if token in node.image.name.lower():images[token]=node.image

def resize(token,name,size):
    im=images[token].copy();im.name=name;im.scale(size,size)
    im.filepath_raw=str(OUT/(name+'.png'));im.file_format='PNG';im.save();return im
albedo=resize('basecolor','Robot_Albedo',2048)
rough=resize('roughness','Robot_Roughness',256)
metal=resize('metallic','Robot_Metallic',256)
packed=bpy.data.images.new('Robot_MetallicSmoothness',width=256,height=256,alpha=True)
packed.colorspace_settings.name='Non-Color'
mp=list(metal.pixels);rp=list(rough.pixels)
packed.pixels=[c for i in range(0,len(mp),4) for c in (mp[i],mp[i],mp[i],1-rp[i])]
packed.filepath_raw=str(OUT/'Robot_MetallicSmoothness.png');packed.file_format='PNG';packed.save()
normal=bpy.data.images.new('Robot_Normal',width=1024,height=1024,alpha=False)
normal.colorspace_settings.name='Non-Color'
normal.generated_color=(.5,.5,1,1)

def mat():
    m=bpy.data.materials.new('Robot optimized PBR');m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links;bs=n.get('Principled BSDF')
    for im,socket in ((albedo,'Base Color'),(rough,'Roughness'),(metal,'Metallic')):
        t=n.new('ShaderNodeTexImage');t.image=im;l.new(t.outputs['Color'],bs.inputs[socket])
    t=n.new('ShaderNodeTexImage');t.image=normal
    nm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],bs.inputs['Normal'])
    return m

lods=[];report={'source_triangles':283376,'lods':[],'textures':{'albedo':2048,'normal':1024,'roughness':256,'metallic':256,'metallic_smoothness':256},'unity_crunch':'pending Unity import'}
targets=(3000,5000,14000)
for h,target in zip(high,targets):
    h.hide_render=False;h.hide_set(False);h.data.calc_loop_triangles()
    o=h.copy();o.data=h.data.copy();bpy.context.scene.collection.objects.link(o);o.name=h.name+'_LOD0'
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    d=o.modifiers.new('LOD0 reduction','DECIMATE');d.ratio=target/len(h.data.loop_triangles);d.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=d.name);o.data.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co for v in o.data.vertices],[tuple(t.vertices) for t in o.data.loop_triangles],all_triangles=True)
    distances=sorted(tree.find_nearest(v.co)[3] for v in list(h.data.vertices)[::13])
    report['lods'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'surface_distance_p95':distances[int(.95*len(distances))],'surface_distance_max':max(distances)})
    bake=bpy.data.materials.new(o.name+' bake');bake.use_nodes=True
    t=bake.node_tree.nodes.new('ShaderNodeTexImage');t.image=normal;bake.node_tree.nodes.active=t
    o.data.materials.clear();o.data.materials.append(bake);o.data.materials.append(cap_material)
    # Bake against the untouched imported mesh: this preserves the supplied
    # custom split normals instead of baking re-created cut-face normals.
    h.hide_render=True;h.hide_set(True);source.hide_render=False;source.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT');source.select_set(True);o.select_set(True);bpy.context.view_layer.objects.active=o
    sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.render.threads_mode='FIXED';sc.render.threads=8
    sc.render.bake.use_selected_to_active=True;sc.render.bake.use_clear=False
    sc.render.bake.cage_extrusion=.006;sc.render.bake.max_ray_distance=.02;sc.render.bake.margin=4;sc.render.bake.normal_space='TANGENT'
    bpy.ops.object.bake(type='NORMAL')
    h.hide_render=True;h.hide_set(True);source.hide_render=True;source.hide_set(True);lods.append(o)
level0=list(lods)
# Tripo's source UVs contain reused/overlapping regions. A new shared atlas is
# needed for a reliable high-to-low bake after geometric reduction.
bpy.ops.object.select_all(action='DESELECT')
for o in level0:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=level0[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.006,area_weight=.1)
bpy.ops.uv.pack_islands(rotate=True,margin=.004)
bpy.ops.object.mode_set(mode='OBJECT')
def fresh(name,size,color):
    im=bpy.data.images.new(name+' atlas',width=size,height=size,alpha=False)
    im.colorspace_settings.name=color;return im
albedo=fresh('Robot_Albedo',2048,'sRGB');rough=fresh('Robot_Roughness',256,'Non-Color');metal=fresh('Robot_Metallic',256,'Non-Color');normal=fresh('Robot_Normal',1024,'Non-Color')
normal.generated_color=(.5,.5,1,1)
source.hide_render=False;source.hide_set(False)
sn=oldmat.node_tree.nodes;sl=oldmat.node_tree.links;output=sn.get('Material Output');bsdf=sn.get('Principled BSDF')
emission=sn.new('ShaderNodeEmission')
for token,target in [('basecolor',albedo),('roughness',rough),('metallic',metal),('normal',normal)]:
    if token=='normal':sl.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    else:
        texture=next(n for n in sn if n.type=='TEX_IMAGE' and n.image==images[token])
        sl.new(texture.outputs['Color'],emission.inputs['Color']);sl.new(emission.outputs[0],output.inputs['Surface'])
    for o in level0:
        bake=o.data.materials[0]
        node=next(n for n in bake.node_tree.nodes if n.type=='TEX_IMAGE');node.image=target;bake.node_tree.nodes.active=node
        bpy.ops.object.select_all(action='DESELECT');source.select_set(True);o.select_set(True);bpy.context.view_layer.objects.active=o
        bpy.ops.object.bake(type='NORMAL' if token=='normal' else 'EMIT')
    if token=='normal':
        pixels=list(target.pixels)
        for i in range(0,len(pixels),4):
            if max(pixels[i:i+3])<.02:pixels[i:i+3]=[.5,.5,1]
        target.pixels=pixels
    target.filepath_raw=str(OUT/({'basecolor':'Robot_Albedo','roughness':'Robot_Roughness','metallic':'Robot_Metallic','normal':'Robot_Normal'}[token]+'.png'));target.file_format='PNG';target.save()
sl.new(bsdf.outputs['BSDF'],output.inputs['Surface']);source.hide_render=True;source.hide_set(True)
mp=list(metal.pixels);rp=list(rough.pixels)
packed.pixels=[c for i in range(0,len(mp),4) for c in (mp[i],mp[i],mp[i],1-rp[i])];packed.save()
material=mat()
for o in level0:
    o.data.materials.clear();o.data.materials.append(material);o.data.materials.append(cap_material)
    for level,ratio in ((1,8000/22000),(2,3000/22000)):
        child=o.copy();child.data=o.data.copy();bpy.context.scene.collection.objects.link(child);child.name=o.name.replace('LOD0','LOD'+str(level))
        bpy.ops.object.select_all(action='DESELECT');child.select_set(True);bpy.context.view_layer.objects.active=child
        d=child.modifiers.new('Distance LOD','DECIMATE');d.ratio=ratio;d.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=d.name)
        child.data.calc_loop_triangles();report['lods'].append({'name':child.name,'triangles':len(child.data.loop_triangles)})
        child.hide_render=True;lods.append(child)
for level in range(3):report['LOD'+str(level)+'_triangles']=sum(r['triangles'] for r in report['lods'] if r['name'].endswith('LOD'+str(level)))
report['reduction_percent']=100*(1-report['LOD0_triangles']/283376)
report['normal_bake']='Original unmodified high mesh to shared non-overlapping LOD0 atlas; albedo/roughness/metallic emission bakes and tangent-space normal bake with supplied normals, 1024'
OUT.joinpath('optimization-report.json').write_text(json.dumps(report,indent=2))

# Canonical transforms: remove parent inverse from meshes while preserving the
# rest geometry. FBX contains one root, yaw, pitch and muzzle plus three LODs.
for obj in lods:
    world=obj.matrix_world.copy();parent=obj.parent
    transform=parent.matrix_world.inverted()@world
    obj.data.transform(transform);obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
root=bpy.data.objects['RustyRobotTurret'];yaw=bpy.data.objects['TurretYaw'];pitch=bpy.data.objects['TurretPitch']
muzzle=bpy.data.objects.new('TurretMuzzle',None);sc.collection.objects.link(muzzle);muzzle.parent=pitch
muzzle.matrix_world=Matrix.Translation(Vector((0,-.499,.529)))
# Blender's -Y gun direction maps to Unity +Z under FBX's forward -Z/up Y.
muzzle.rotation_euler=(0,0,0)
for o in lods:o.hide_set(False)
# Explicit Unity coordinates avoid relying on FBX pre-rotation conventions for
# runtime yaw (local Y), pitch (local X) and muzzle forward (local +Z).
def unity(v):return {'x':v.x,'y':v.z,'z':-v.y}
unity_meshes=[]
for obj in lods:
    mesh=obj.data;mesh.calc_loop_triangles();positions=[];normals=[];uvs=[];surface=[];caps=[]
    for tri in mesh.loop_triangles:
        first=len(positions)
        for li in tri.loops:
            positions.append(unity(mesh.vertices[mesh.loops[li].vertex_index].co))
            normals.append(unity(mesh.corner_normals[li].vector))
            uv=mesh.uv_layers.active.data[li].uv;uvs.append({'x':uv.x,'y':uv.y})
        (caps if tri.material_index==1 else surface).extend((first,first+2,first+1))
    unity_meshes.append({'name':obj.name,'parent':'TurretPitch' if obj.name.startswith('PitchBody') else 'TurretYaw' if obj.name.startswith('YawCradle') else 'Root',
                         'vertices':positions,'normals':normals,'uv':uvs,'surfaceTriangles':surface,'capTriangles':caps})
OUT.joinpath('RobotTurret_UnityMeshes.json').write_text(json.dumps({'pitchPivot':unity(pitch.location),'muzzlePosition':unity(muzzle.location),'meshes':unity_meshes},separators=(',',':')))
bpy.ops.object.select_all(action='DESELECT')
for o in [root,yaw,pitch,muzzle]+lods:o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/'RustyRobotTurret_Rigged.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',apply_scale_options='FBX_SCALE_ALL')
for o in lods:o.hide_render=not o.name.endswith('LOD0')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RustyRobotTurret_Optimized.blend'))
sc.cycles.samples=32
cam=sc.camera;cam.data.ortho_scale=1.25;target=Vector((0,0,.44))
for label,eye in [('optimized-quarter',(-2,-3,1.8)),('optimized-side',(-3,0,.44)),('optimized-front',(0,-3,.44)),('optimized-top',(0,0,4))]:
    cam.location=eye;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
