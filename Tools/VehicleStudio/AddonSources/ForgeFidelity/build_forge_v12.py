"""Limit exposed metal wear after v11 visual inspection showed global whitening."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v11.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v11','ForgeFidelity/v12')
source=source.replace("edge.color_ramp.elements[0].position=.49", "edge.color_ramp.elements[0].position=.54")
source=source.replace("edge.color_ramp.elements[1].position=.55;edge.color_ramp.elements[1].color=(.5,.5,.5,1)", "edge.color_ramp.elements[1].position=.65;edge.color_ramp.elements[1].color=(.12,.12,.12,1)")
source=source.replace("mix.inputs[2].default_value=(.16,.14,.11,1)", "mix.inputs[2].default_value=(.075,.063,.05,1)")
exec(compile(source,__file__,'exec'))
