"""Recess and roughen mortar behind fitted stone, preserving prior revisions."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MasonryMortar_v5';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'MasonryInfill_v3/infill.blend'))
sc=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[]
for o in sc.objects:
    if not o.name.startswith(('stone_','inferred_rear_stone_')):continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(verts)
    verts.extend(o.matrix_world@v.co for v in mesh.vertices)
    faces.extend([start+i for i in p.vertices] for p in mesh.polygons);ev.to_mesh_clear()
tree=BVHTree.FromPolygons(verts,faces)
core=bpy.data.objects['furnace_core'];bm=bmesh.new();bm.from_mesh(core.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=2,use_grid_fill=True)
bm.normal_update();changed=0
plan=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text());axis=Vector(plan['outwardAxis'])
for v in bm.verts:
    if .29<v.co.z<.76 and abs(v.co.dot(axis)-plan['frontDepth'])<.055:continue
    near,n,index,dist=tree.find_nearest(v.co)
    if near is not None and dist<.13 and (near-v.co).dot(v.normal)>-.002:
        v.co-=v.normal*.008;changed+=1
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(core.data);bm.free()
texture=bpy.data.textures.new('Uneven mortar aggregate geometry',type='CLOUDS');texture.noise_scale=.022;texture.noise_depth=2
group=core.vertex_groups.new(name='Exterior mortar relief')
for v in core.data.vertices:
    if not (.29<v.co.z<.76 and abs(v.co.dot(axis)-plan['frontDepth'])<.065):group.add([v.index],1,'REPLACE')
mod=core.modifiers.new('Small irregular mortar ridges and pits','DISPLACE');mod.texture=texture;mod.texture_coords='GLOBAL';mod.strength=.001;mod.mid_level=.6;mod.vertex_group=group.name
core['surfaceRole']='Recessed uneven mortar; no broad flat backing-panel appearance intended'
receipt={'status':'CLAY_RENDER_REVIEW_REQUIRED','recessedVertices':changed,'recess':.008,'reliefStrength':.001,'hearthRimProtected':True,'source':str(root/'MasonryInfill_v3/infill.blend'),'limits':['Recess selection inferred from neighboring solids','Joint widths and intersections require rendered/geometry review','No fidelity pass implied']}
(out/'mortar-receipt.json').write_text(json.dumps(receipt,indent=2))
sc.cycles.samples=32;sc.render.filepath=str(out/'clay-mortar.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'mortar.blend'));bpy.ops.render.render(write_still=True)
