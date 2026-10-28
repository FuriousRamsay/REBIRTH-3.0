"""Surface-specific grain, stitches, edge wear and crack-driven coal emission."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v10.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v10','ForgeFidelity/v11')
extra='''
thread=mat('Waxed flax stitches',(.13,.085,.045),0,.9)
# Sewn seams follow all four raised fold edges, with separate short stitches.
for side in [-1,1]:
 for row in [0,2,4,6]:
  t=row/6
  for j in range(4):
   x,w=profile[j];x2,w2=profile[j+1]
   for k in range(7):
    f=(k+.3)/7;px=x+(x2-x)*f;pw=w+(w2-w)*f
    pz=.31-abs(px+.28)*.32+2*abs(px+.28)*.32*t
    rod('Individual leather seam stitch',(px,side*(pw+.013),pz-.0015),(px+.0025,side*(pw+.013),pz+.0015),.00065,thread)
# Directional wood grain is physically aligned to each board's long X axis.
nodes=wood.node_tree.nodes;links=wood.node_tree.links;p=nodes.get('Principled BSDF')
coords=nodes.new('ShaderNodeTexCoord');stretch=nodes.new('ShaderNodeVectorMath');stretch.operation='MULTIPLY';stretch.inputs[1].default_value=(3,120,18)
grain=nodes.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=1;grain.inputs['Detail'].default_value=5
links.new(coords.outputs['Generated'],stretch.inputs[0]);links.new(stretch.outputs[0],grain.inputs['Vector'])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.012,.004,.001,1);ramp.color_ramp.elements[1].color=(.14,.065,.023,1)
links.new(grain.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.001;bump.inputs['Strength'].default_value=.5
links.new(grain.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
# Fine leather pores with sparse creases, separate from wood.
nodes=leather.node_tree.nodes;links=leather.node_tree.links;p=nodes.get('Principled BSDF')
pores=nodes.new('ShaderNodeTexVoronoi');pores.inputs['Scale'].default_value=180
bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0007;bump.inputs['Strength'].default_value=.45
links.new(pores.outputs['Distance'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
p.inputs['Roughness'].default_value=.58
# Bright coals have a charred crust; emission is restricted to irregular cracks.
nodes=ember.node_tree.nodes;links=ember.node_tree.links;p=nodes.get('Principled BSDF')
crack=nodes.new('ShaderNodeTexVoronoi');crack.feature='DISTANCE_TO_EDGE';crack.inputs['Scale'].default_value=8
mask=nodes.new('ShaderNodeValToRGB');mask.color_ramp.elements[0].position=.035;mask.color_ramp.elements[0].color=(1,1,1,1);mask.color_ramp.elements[1].position=.075;mask.color_ramp.elements[1].color=(0,0,0,1)
links.new(crack.outputs['Distance'],mask.inputs[0]);power=nodes.new('ShaderNodeMath');power.operation='MULTIPLY';power.inputs[1].default_value=5
links.new(mask.outputs[0],power.inputs[0]);links.new(power.outputs[0],p.inputs['Emission Strength']);p.inputs['Base Color'].default_value=(.008,.005,.003,1)
# Exposed iron edges use geometry curvature as a separate wear signal.
nodes=iron.node_tree.nodes;links=iron.node_tree.links;p=nodes.get('Principled BSDF')
geometry=nodes.new('ShaderNodeNewGeometry');edge=nodes.new('ShaderNodeValToRGB')
edge.color_ramp.elements[0].position=.49;edge.color_ramp.elements[0].color=(0,0,0,1);edge.color_ramp.elements[1].position=.55;edge.color_ramp.elements[1].color=(.5,.5,.5,1)
links.new(geometry.outputs['Pointiness'],edge.inputs[0]);previous=p.inputs['Base Color'].links[0].from_socket
mix=nodes.new('ShaderNodeMixRGB');mix.inputs[2].default_value=(.16,.14,.11,1);links.new(edge.outputs[0],mix.inputs[0]);links.new(previous,mix.inputs[1]);links.new(mix.outputs[0],p.inputs['Base Color'])
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
exec(compile(source,__file__,'exec'))
