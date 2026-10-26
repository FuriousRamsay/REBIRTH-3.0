"""Approved 40k/16k/6k turret reduction with original UVs and transferred normals.

Supersedes the rejected 22k baked-atlas experiment. Source PBR maps are resized
without repainting. Custom normals are projected from the untouched source.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
bpy.ops.wm.open_mainfile(filepath=str(P/'RobotTurret_Separated.blend'))
sc=bpy.context.scene;source=bpy.data.objects['SOURCE_283376']
high=[bpy.data.objects[n] for n in ('FixedSupport','YawCradle','PitchBody')]
cap_material=bpy.data.materials['Concealed bearing steel']
material=source.data.materials[0].copy();material.name='Robot optimized original PBR'
texture_sizes={'basecolor':2048,'normal':1024,'roughness':256,'metallic':256}
texture_names={'basecolor':'Robot_Albedo','normal':'Robot_Normal','roughness':'Robot_Roughness','metallic':'Robot_Metallic'}
images={}
for node in material.node_tree.nodes:
    if node.type!='TEX_IMAGE':continue
    token=next(t for t in texture_sizes if t in node.image.name.lower())
    im=node.image.copy();im.name=texture_names[token];im.scale(texture_sizes[token],texture_sizes[token])
    im.filepath_raw=str(P/(texture_names[token]+'.png'));im.file_format='PNG';im.save()
    node.image=im;images[token]=im
packed=bpy.data.images.new('Robot_MetallicSmoothness',width=256,height=256,alpha=True)
packed.colorspace_settings.name='Non-Color'
mp=list(images['metallic'].pixels);rp=list(images['roughness'].pixels)
packed.pixels=[c for i in range(0,len(mp),4) for c in (mp[i],mp[i],mp[i],1-rp[i])]
packed.filepath_raw=str(P/'Robot_MetallicSmoothness.png');packed.file_format='PNG';packed.save()

def normals(o):
    # The imported FBX uses custom split normals. Simply recalculating after
    # mesh separation loses its shading, especially around the bearing and bolts.
    bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o
    source.hide_set(False)
    mod=o.modifiers.new('Preserve source shading','DATA_TRANSFER');mod.object=source
    mod.use_loop_data=True;mod.data_types_loops={'CUSTOM_NORMAL'};mod.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    source.hide_set(True)

report={'source_triangles':283376,'approved_targets':[40000,16000,6000],
        'shading':'Original UVs and PBR maps retained; custom split normals transferred from original imported mesh. No generated texture atlas.',
        'textures':texture_sizes,'unity_crunch':'pending Unity import','lods':[]}
lods=[]
for h,budget in zip(high,(8000,10000,22000)):
    h.data.calc_loop_triangles()
    for level,ratio in ((0,1),(1,.4),(2,.15)):
        o=h.copy();o.data=h.data.copy();sc.collection.objects.link(o);o.name=h.name+'_LOD'+str(level)
        o.hide_set(False);o.hide_render=False
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        dec=o.modifiers.new('Silhouette reduction','DECIMATE');dec.ratio=budget*ratio/len(h.data.loop_triangles);dec.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=dec.name);normals(o)
        o.data.materials.clear();o.data.materials.append(material);o.data.materials.append(cap_material)
        o.data.calc_loop_triangles()
        tree=BVHTree.FromPolygons([v.co for v in o.data.vertices],[tuple(t.vertices) for t in o.data.loop_triangles],all_triangles=True)
        errors=sorted(tree.find_nearest(v.co)[3] for v in list(h.data.vertices)[::17])
        report['lods'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'custom_normals':o.data.has_custom_normals,
                             'distance_p95_m':errors[int(.95*len(errors))],'distance_max_m':max(errors)})
        lods.append(o);o.hide_render=level!=0
    h.hide_set(True);h.hide_render=True
source.hide_render=True;source.hide_set(True)
for level in range(3):report['LOD'+str(level)+'_triangles']=sum(r['triangles'] for r in report['lods'] if r['name'].endswith('LOD'+str(level)))
report['reduction_percent']=100*(1-report['LOD0_triangles']/283376)

root=bpy.data.objects['RustyRobotTurret'];yaw=bpy.data.objects['TurretYaw'];pitch=bpy.data.objects['TurretPitch']
for o in lods:
    o.data.transform(o.parent.matrix_world.inverted()@o.matrix_world)
    o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_basis=Matrix.Identity(4)
muzzle=bpy.data.objects.new('TurretMuzzle',None);sc.collection.objects.link(muzzle);muzzle.parent=pitch
muzzle.matrix_world=Matrix.Translation(Vector((0,-.499,.529)))
bpy.context.view_layer.update()

# Explicit canonical Unity coordinates: X right, Y up, Z barrel forward.
def unity(v):return {'x':v.x,'y':v.z,'z':-v.y}
meshes=[]
for o in lods:
    mesh=o.data;mesh.calc_loop_triangles();pos=[];ns=[];uvs=[];surface=[];caps=[]
    for t in mesh.loop_triangles:
        start=len(pos)
        for li in t.loops:
            pos.append(unity(mesh.vertices[mesh.loops[li].vertex_index].co));ns.append(unity(mesh.corner_normals[li].vector))
            uv=mesh.uv_layers.active.data[li].uv;uvs.append({'x':uv.x,'y':uv.y})
        (caps if t.material_index==1 else surface).extend((start,start+1,start+2))
    meshes.append({'name':o.name,'parent':'TurretPitch' if o.name.startswith('PitchBody') else 'TurretYaw' if o.name.startswith('YawCradle') else 'Root',
                   'vertices':pos,'normals':ns,'uv':uvs,'surfaceTriangles':surface,'capTriangles':caps})
P.joinpath('RobotTurret_UnityMeshes.json').write_text(json.dumps({'pitchPivot':unity(pitch.location),'muzzlePosition':unity(muzzle.location),'meshes':meshes},separators=(',',':')))
bpy.ops.object.select_all(action='DESELECT')
for o in [root,yaw,pitch,muzzle]+lods:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(P/'RustyRobotTurret_Rigged.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',apply_scale_options='FBX_SCALE_ALL')
P.joinpath('optimization-report.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'RustyRobotTurret_Optimized.blend'))
cam=sc.camera;cam.data.ortho_scale=1.25;target=Vector((0,0,.44));sc.cycles.samples=32
for label,eye,original in [('source-matched',(-2,-3,1.8),True),('optimized-quarter',(-2,-3,1.8),False),('optimized-side',(-3,0,.44),False),('optimized-front',(0,-3,.44),False),('optimized-top',(0,0,4),False)]:
    source.hide_render=not original
    for o in lods:o.hide_render=original or not o.name.endswith('LOD0')
    cam.location=eye;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(P/(label+'.png'));bpy.ops.render.render(write_still=True)
source.hide_render=True

