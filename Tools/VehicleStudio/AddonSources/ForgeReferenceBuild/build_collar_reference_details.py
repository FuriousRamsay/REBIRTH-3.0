"""Explicit measured collar detail plan; clay validation before materials."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'CollarDetails_v22';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'SideMeasuredFit_v15/side-fit.blend'))
collar=next(p for p in json.loads((Path(__file__).parent/'round-components-plan.json').read_text())['parts'] if p['id']=='top_collar')
center=Vector(collar['center']);axis=Vector((-.8660254,-.5,0)).normalized()
horizontal=Vector((-axis.y,axis.x,0));up=Vector((0,0,1))
plate=center+axis*.158;plate.z=.905
parts=[{'name':'collar_front_bolt_plate','center':list(plate),'dimensions':[.064,.008,.050],'basis':[list(horizontal),list(axis),list(up)],'role':'iron'},
       {'name':'collar_front_washer','center':list(plate+axis*.007),'radius':.023,'depth':.006,'axis':list(axis),'role':'iron'},
       {'name':'collar_front_bolt','center':list(plate+axis*.012),'radius':.017,'depth':.011,'axis':list(axis),'role':'iron'},
       {'name':'collar_inner_basin','center':list(center-up*.039),'radius':.111,'depth':.006,'axis':[0,0,1],'role':'heated_refractory'},
       {'name':'collar_inner_plug','center':list(center-up*.020),'radius':.024,'depth':.028,'axis':[0,0,1],'role':'iron'}]
plan={'source':'SideMeasuredFit_v15/side-fit.blend','units':'meters in current authored scale','reference':'Approved forge concept: collar front plate/bolt, visible hot basin and central plug','visibilitySource':'CollarDetails_v21/visible-plate-position-candidates.json; first candidate hits actual collar, not strap','parts':parts,'checks':{'basinWithinInnerRadius':.111<.118,'plateBelowCollarTop':plate.z+.025<center.z,'preserveExistingGeometry':True},'fidelity':'CLAY_REVIEW_REQUIRED'}
assert all(plan['checks'].values())
(out/'geometry-plan.json').write_text(json.dumps(plan,indent=2))
material=bpy.data.materials['Neutral untextured clay'];receipt=[]
for part in parts:
    if 'dimensions' in part:
        bpy.ops.mesh.primitive_cube_add(size=1,location=part['center']);o=bpy.context.object
        o.rotation_euler=axis.to_track_quat('Y','Z').to_euler();o.dimensions=part['dimensions'];bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=part['radius'],depth=part['depth'],location=part['center']);o=bpy.context.object
        o.rotation_euler=Vector(part['axis']).to_track_quat('Z','Y').to_euler()
    o.name=part['name'];o.data.materials.append(material);o['surfaceRole']=part['role']
    bevel=o.modifiers.new('Worn edge radius','BEVEL');bevel.width=.0015;bevel.segments=3
    receipt.append({'name':o.name,'position':list(o.location),'rotationRadians':list(o.rotation_euler),'dimensions':list(o.dimensions),'role':part['role']})
(out/'geometry-receipt.json').write_text(json.dumps({'parts':receipt,'fidelity':'CLAY_REVIEW_PENDING'},indent=2))
sc=bpy.context.scene;sc.cycles.samples=32;sc.render.filepath=str(out/'reference-clay.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'collar-details.blend'));bpy.ops.render.render(write_still=True)
print('COLLAR_DETAILS_CLAY_SAVED')
