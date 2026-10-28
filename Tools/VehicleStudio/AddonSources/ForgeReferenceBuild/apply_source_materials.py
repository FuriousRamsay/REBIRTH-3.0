"""Material-only source preview on the user's approved, unchanged clay geometry."""
import bpy, json, hashlib, sys
from pathlib import Path

root = Path(__file__).parent
out = Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
source = out/'approved-clay.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest().upper() == 'D344B3F7897BA45C81E1C0DBEA06CC42BAD630E2AD2A6AA3E74E24DC4B07F72A'
fitted='--fitted-masonry' in sys.argv
if fitted:source=out/'SideMeasuredFit_v15/side-fit.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
def load(name): return json.loads((root/name).read_text())
roles = []
def add(role, name=None):
    i = len(roles)
    roles.append((name or f'planned_mesh_{i:03d}', role))
add('leather'); add('mortar','furnace_core')
for p in load('round-components-plan.json')['parts']: add('crucible' if p['id']=='crucible' else 'iron')
for p in load('crucible-hardware-plan.json')['parts']: add('iron')
for p in load('bulk-masonry-plan.json')['parts']: add('stone','stone_'+p['id'])
for p in load('mounting-feet-plan.json')['parts']: add('iron')
for p in load('upright-harness-plan.json')['parts']: add('iron','harness_'+p['id'])
for p in load('bellows-board-plan.json')['parts']: add('wood')
for p in load('frame-hardware-plan.json')['parts']: add('iron')
for p in load('nozzle-hardware-plan.json')['parts']: add('iron')
for p in load('bellows-frame-plan.json')['records']:
    if 'axisEndpoints' in p: add('wood' if 'crossSection' in p else 'iron')
add('iron')
rolemap = dict(roles)
palette = {
 'stone': ((.018,.019,.019,1),(.11,.095,.073,1),.84,0,24,.003),
 'mortar': ((.018,.016,.013,1),(.055,.049,.04,1),.95,0,110,.001),
 'iron': ((.024,.027,.029,1),(.09,.038,.014,1),.58,.72,55,.0005),
 'leather': ((.024,.008,.004,1),(.12,.044,.018,1),.73,0,170,.0006),
 'wood': ((.033,.018,.009,1),(.16,.087,.038,1),.79,0,48,.0008),
 'crucible': ((.055,.04,.025,1),(.27,.22,.15,1),.87,0,100,.0014),
 'coal': ((.008,.006,.004,1),(.035,.026,.018,1),.92,0,80,.001)
}
materials = {}
for role,(low,high,rough,metal,scale,depth) in palette.items():
    m=bpy.data.materials.new('Forge_'+role); m.use_nodes=True
    n=m.node_tree.nodes; l=m.node_tree.links; bs=n.get('Principled BSDF')
    bs.inputs['Roughness'].default_value=rough; bs.inputs['Metallic'].default_value=metal
    tc=n.new('ShaderNodeTexCoord'); noise=n.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value=scale; noise.inputs['Detail'].default_value=5; noise.inputs['Roughness'].default_value=.7
    if role=='wood':
        mapping=n.new('ShaderNodeVectorMath'); mapping.operation='MULTIPLY'; mapping.inputs[1].default_value=(1,12,12)
        l.new(tc.outputs['Object'],mapping.inputs[0]); l.new(mapping.outputs[0],noise.inputs['Vector'])
    else: l.new(tc.outputs['Object'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position=.25; ramp.color_ramp.elements[0].color=low
    ramp.color_ramp.elements[1].position=.8; ramp.color_ramp.elements[1].color=high
    l.new(noise.outputs['Fac'],ramp.inputs[0]); l.new(ramp.outputs['Color'],bs.inputs['Base Color'])
    if role=='stone':
        # Mineral inclusions at a separate scale, without painting illumination.
        fine=n.new('ShaderNodeTexNoise'); fine.inputs['Scale'].default_value=460; fine.inputs['Detail'].default_value=2
        l.new(tc.outputs['Object'],fine.inputs['Vector'])
        fleck=n.new('ShaderNodeValToRGB'); fleck.color_ramp.elements[0].position=.58; fleck.color_ramp.elements[0].color=(0,0,0,1)
        fleck.color_ramp.elements[1].position=.73; fleck.color_ramp.elements[1].color=(.65,.65,.65,1)
        l.new(fine.outputs['Fac'],fleck.inputs[0])
        mix=n.new('ShaderNodeMixRGB'); mix.blend_type='MIX'; mix.inputs[2].default_value=(.29,.25,.18,1)
        l.new(fleck.outputs['Color'],mix.inputs[0]); l.new(ramp.outputs['Color'],mix.inputs[1]); l.new(mix.outputs[0],bs.inputs['Base Color'])
        fracture=n.new('ShaderNodeTexNoise'); fracture.inputs['Scale'].default_value=135; fracture.inputs['Detail'].default_value=7; fracture.inputs['Roughness'].default_value=.8
        l.new(tc.outputs['Object'],fracture.inputs['Vector'])
        veins=n.new('ShaderNodeValToRGB'); veins.color_ramp.elements[0].position=.51; veins.color_ramp.elements[0].color=(0,0,0,1)
        veins.color_ramp.elements[1].position=.64; veins.color_ramp.elements[1].color=(.7,.7,.7,1)
        l.new(fracture.outputs['Fac'],veins.inputs[0])
        mineral=n.new('ShaderNodeMixRGB'); mineral.inputs[2].default_value=(.32,.27,.19,1)
        l.new(veins.outputs['Color'],mineral.inputs[0]); l.new(mix.outputs[0],mineral.inputs[1]); l.new(mineral.outputs[0],bs.inputs['Base Color'])
    if role=='iron':
        ramp.color_ramp.elements[0].position=.60; ramp.color_ramp.elements[1].position=.79
        rust=n.new('ShaderNodeValToRGB'); rust.color_ramp.elements[0].position=.60; rust.color_ramp.elements[0].color=(1,1,1,1)
        rust.color_ramp.elements[1].position=.76; rust.color_ramp.elements[1].color=(0,0,0,1)
        l.new(noise.outputs['Fac'],rust.inputs[0]); l.new(rust.outputs['Color'],bs.inputs['Metallic'])
        geo=n.new('ShaderNodeNewGeometry'); edge=n.new('ShaderNodeValToRGB')
        edge.color_ramp.elements[0].position=.49; edge.color_ramp.elements[0].color=(0,0,0,1)
        edge.color_ramp.elements[1].position=.57; edge.color_ramp.elements[1].color=(.6,.6,.6,1)
        l.new(geo.outputs['Pointiness'],edge.inputs[0])
        wear=n.new('ShaderNodeMixRGB'); wear.inputs[2].default_value=(.17,.15,.12,1)
        l.new(edge.outputs['Color'],wear.inputs[0]); l.new(ramp.outputs['Color'],wear.inputs[1]); l.new(wear.outputs[0],bs.inputs['Base Color'])
    bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.3; bump.inputs['Distance'].default_value=depth
    l.new(noise.outputs['Fac'],bump.inputs['Height']); l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    if role=='stone':
        chips=n.new('ShaderNodeBump'); chips.inputs['Strength'].default_value=.55; chips.inputs['Distance'].default_value=.0025
        l.new(fracture.outputs['Fac'],chips.inputs['Height']); l.new(bump.outputs['Normal'],chips.inputs['Normal']); l.new(chips.outputs['Normal'],bs.inputs['Normal'])
    if role=='leather':
        pores=n.new('ShaderNodeTexVoronoi'); pores.feature='DISTANCE_TO_EDGE'; pores.inputs['Scale'].default_value=650
        l.new(tc.outputs['Object'],pores.inputs['Vector'])
        grain=n.new('ShaderNodeBump'); grain.inputs['Strength'].default_value=.35; grain.inputs['Distance'].default_value=.0005
        l.new(pores.outputs['Distance'],grain.inputs['Height']); l.new(bump.outputs['Normal'],grain.inputs['Normal']); l.new(grain.outputs['Normal'],bs.inputs['Normal'])
    if role in ('stone','iron','leather','wood','crucible'):
        image=bpy.data.images.load(str(root/f'Textures/{role}-albedo-generated-v1.png'),check_existing=True)
        tex=n.new('ShaderNodeTexImage'); tex.image=image; tex.projection='BOX'; tex.projection_blend=.22
        spatial=n.new('ShaderNodeVectorMath'); spatial.operation='SCALE'; spatial.inputs[3].default_value={'stone':3.2,'iron':2.4,'leather':2.0,'wood':1.7,'crucible':3.0}[role]
        l.new(tc.outputs['Object'],spatial.inputs[0]); l.new(spatial.outputs[0],tex.inputs['Vector']); l.new(tex.outputs['Color'],bs.inputs['Base Color'])
        surface=n.new('ShaderNodeBump'); surface.inputs['Strength'].default_value=.2; surface.inputs['Distance'].default_value=.001
        l.new(tex.outputs['Color'],surface.inputs['Height']); l.new(surface.outputs['Normal'],bs.inputs['Normal'])
        if role=='wood':
            age=n.new('ShaderNodeHueSaturation'); age.inputs['Saturation'].default_value=.78; age.inputs['Value'].default_value=.55
            l.new(tex.outputs['Color'],age.inputs['Color']); l.new(age.outputs['Color'],bs.inputs['Base Color'])
        if role=='stone':
            info=n.new('ShaderNodeObjectInfo'); variation=n.new('ShaderNodeMapRange')
            variation.inputs['To Min'].default_value=.62; variation.inputs['To Max'].default_value=1.05
            l.new(info.outputs['Random'],variation.inputs['Value'])
            tint=n.new('ShaderNodeMixRGB'); tint.blend_type='MULTIPLY'; tint.inputs[0].default_value=1
            l.new(tex.outputs['Color'],tint.inputs[1]); l.new(variation.outputs[0],tint.inputs[2]); l.new(tint.outputs[0],bs.inputs['Base Color'])
        if role=='iron':
            channels=n.new('ShaderNodeSeparateColor'); l.new(tex.outputs['Color'],channels.inputs['Color'])
            redness=n.new('ShaderNodeMath'); redness.operation='SUBTRACT'
            l.new(channels.outputs['Red'],redness.inputs[0]); l.new(channels.outputs['Green'],redness.inputs[1])
            oxide=n.new('ShaderNodeMath'); oxide.operation='MULTIPLY'; oxide.inputs[1].default_value=14; oxide.use_clamp=True
            l.new(redness.outputs[0],oxide.inputs[0])
            clean=n.new('ShaderNodeMath'); clean.operation='SUBTRACT'; clean.inputs[0].default_value=1; clean.use_clamp=True
            l.new(oxide.outputs[0],clean.inputs[1]); l.new(clean.outputs[0],bs.inputs['Metallic'])
            worn=n.new('ShaderNodeMixRGB'); worn.inputs[2].default_value=(.21,.18,.14,1)
            l.new(edge.outputs['Color'],worn.inputs[0]); l.new(tex.outputs['Color'],worn.inputs[1]); l.new(worn.outputs[0],bs.inputs['Base Color'])
            bs.inputs['Roughness'].default_value=.47
    if role=='coal':
        bs.inputs['Emission Color'].default_value=(1,.055,.003,1)
        glow=n.new('ShaderNodeValToRGB'); glow.color_ramp.elements[0].position=.43; glow.color_ramp.elements[0].color=(0,0,0,1)
        glow.color_ramp.elements[1].position=.62; glow.color_ramp.elements[1].color=(1,1,1,1)
        power=n.new('ShaderNodeMath'); power.operation='MULTIPLY'; power.inputs[1].default_value=2.8
        l.new(noise.outputs['Fac'],glow.inputs[0]); l.new(glow.outputs['Color'],power.inputs[0]); l.new(power.outputs[0],bs.inputs['Emission Strength'])
    materials[role]=m
assigned=[]; missing=[]
for o in bpy.context.scene.objects:
    if o.type!='MESH' or o.hide_render or o.name=='Inspection ground': continue
    role=rolemap.get(o.name)
    if o.name.startswith(('stone_','inferred_rear_stone_','hearth_stones_')): role='stone'
    if o.name.startswith('floor_coal_'): role='coal'
    if role is None: missing.append(o.name); continue
    mat=materials[role]
    if role=='wood':
        mat=mat.copy(); mat.name='Forge_wood_grain_'+o.name
        nodes=mat.node_tree.nodes; links=mat.node_tree.links
        tex=next(t for t in nodes if t.type=='TEX_IMAGE')
        tc=next(t for t in nodes if t.type=='TEX_COORD')
        split=nodes.new('ShaderNodeSeparateXYZ'); links.new(tc.outputs['Object'],split.inputs[0])
        order=sorted(range(3),key=lambda i:o.dimensions[i],reverse=True)
        combine=nodes.new('ShaderNodeCombineXYZ')
        links.new(split.outputs[order[1]],combine.inputs['X']); links.new(split.outputs[order[2]],combine.inputs['Y']); links.new(split.outputs[order[0]],combine.inputs['Z'])
        scale=nodes.new('ShaderNodeVectorMath'); scale.operation='SCALE'; scale.inputs[3].default_value=1.7
        links.new(combine.outputs[0],scale.inputs[0]); links.new(scale.outputs[0],tex.inputs['Vector']); tex.projection='BOX'; tex.projection_blend=.12
    if o.name.startswith('hearth_stones_'):
        mat=mat.copy(); mat.name='Forge_soot_'+o.name
        nodes=mat.node_tree.nodes; links=mat.node_tree.links; bs=nodes.get('Principled BSDF')
        previous=bs.inputs['Base Color'].links[0].from_socket
        soot=nodes.new('ShaderNodeMixRGB'); soot.blend_type='MULTIPLY'; soot.inputs[0].default_value=1; soot.inputs[2].default_value=(.28,.24,.20,1)
        links.new(previous,soot.inputs[1]); links.new(soot.outputs[0],bs.inputs['Base Color'])
    o.data.materials.clear(); o.data.materials.append(mat); assigned.append({'object':o.name,'role':role})
assert not missing, f'Unmapped mesh roles: {missing}'
sc=bpy.context.scene; sc.render.resolution_x=1448; sc.render.resolution_y=1086; sc.render.resolution_percentage=100
sc.world.use_nodes=True
background=sc.world.node_tree.nodes.get('Background'); background.inputs['Color'].default_value=(.025,.028,.03,1); background.inputs['Strength'].default_value=.35
ground=bpy.data.objects.get('Inspection ground')
if ground:
    for m in ground.data.materials:
        if m and m.use_nodes:
            b=m.node_tree.nodes.get('Principled BSDF')
            if b:b.inputs['Base Color'].default_value=(.035,.038,.04,1)
fill=bpy.data.objects.get('Soft fill')
if fill:fill.data.energy=70
coals=[o for o in sc.objects if o.name.startswith('floor_coal_')]
if coals:
    from mathutils import Vector
    centre=sum((o.matrix_world @ Vector(c) for o in coals for c in o.bound_box),Vector())/(len(coals)*8)
    bpy.ops.object.light_add(type='POINT',location=centre+Vector((0,0,.055)))
    fire=bpy.context.object; fire.name='Hearth preview illumination'; fire.data.energy=5; fire.data.color=(1,.19,.035); fire.data.shadow_soft_size=.08
suffix='fitted-v12' if fitted else 'v10'
sc.cycles.samples=48; sc.render.filepath=str(out/f'source-material-preview-{suffix}.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/f'source-materials-{suffix}.blend'))
(out/f'material-assignment-{suffix}.json').write_text(json.dumps({'assigned':assigned,'geometryChangedDuringMaterialStage':False,'geometrySource':str(source),'mapsBaked':False,'status':'SOURCE_PREVIEW_REQUIRES_VISUAL_REVIEW'},indent=2))
bpy.ops.render.render(write_still=True)
