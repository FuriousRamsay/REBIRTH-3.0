import bpy,json,sys
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownLayers_v39';out.mkdir(parents=True,exist_ok=True)
rigid='--rigid-layer' in sys.argv
if rigid:out=root/'CrownRigidLayers_v40';out.mkdir(parents=True,exist_ok=True)
parts=[{'object':'harness_crown_cross_band','modifier':'Fit to stone surface','fromOffset':.003,'toOffset':.014},{'object':'harness_crown_diagonal_left','modifier':'Fit to stone surface','fromOffset':.003,'toOffset':.025}]
plan={'source':'BellowsLeatherNormals_v35/leather-normals-clay.blend','status':'EXPERIMENT_REQUIRES_CONTACT_AND_CLAY_REVIEW','parts':parts,'method':'Keep 9 mm band thickness. Raise cross band 11 mm and diagonal 22 mm along shrinkwrap contact normals. Original front straps stay at 3 mm. Reposition crown rivets by closest evaluated surface difference.','limits':'Full band lift may create excess clearance outside overlaps; inspect before accepting.'}
if rigid:plan['method']='Freeze original evaluated shrinkwrap surface first. Translate cross band +11 mm Z, diagonal +22 mm Z without changing any fitted surface shape. Preserve solidify and bevel. Refit associated rivet positions by nearest surface difference.'
path=out/'geometry-plan.json'
if '--apply' not in sys.argv:
    assert not path.exists();path.write_text(json.dumps(plan,indent=2));print('LAYER_PLAN_SAVED');sys.exit(0)
assert json.loads(path.read_text())==plan
bpy.ops.wm.open_mainfile(filepath=str(root/plan['source']))
from mathutils.bvhtree import BVHTree
old={};deps=bpy.context.evaluated_depsgraph_get()
for part in parts:
    obj=bpy.data.objects[part['object']];modifier=obj.modifiers[part['modifier']];assert abs(modifier.offset-part['fromOffset'])<.00001
    old[obj.name]=BVHTree.FromObject(obj,deps)
    if rigid:
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.location.z+=part['toOffset']-part['fromOffset']
    else:modifier.offset=part['toOffset']
bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();new={name:BVHTree.FromObject(bpy.data.objects[name],deps) for name in old}
moved=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or obj.name not in ('harness_crown_left_cross','harness_crown_front_cross','harness_crown_right_cross'):continue
    center=sum((obj.matrix_world@v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
    candidates=[(tree.find_nearest(center),name) for name,tree in old.items()];hit,name=min(candidates,key=lambda pair:pair[0][3])
    if hit[0] is None or hit[3]>.05:continue
    band=bpy.data.objects[name]
    closest=new[name].find_nearest(band.matrix_world.inverted()@hit[0])[0]
    if closest is None:continue
    closest=band.matrix_world@closest
    delta=closest-hit[0];obj.location+=delta;moved.append({'object':obj.name,'delta':list(delta)})
sc=bpy.context.scene;sc.cycles.device='CPU';sc.cycles.samples=32
assert not (out/'crown-layers-clay.blend').exists()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'crown-layers-clay.blend'));sc.render.filepath=str(out/'reference-clay.png');bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'modifiedBands':parts,'rivetsMoved':moved,'fidelity':'VISUAL_REVIEW_PENDING'},indent=2))
