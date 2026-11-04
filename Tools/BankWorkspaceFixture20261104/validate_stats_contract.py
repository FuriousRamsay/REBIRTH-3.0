"""Check bank XML against the exact native PowerSourceStats.Init lookups.

This is a controller/XML contract test, not a game startup or visual test.
"""
from pathlib import Path
import re
from lxml import etree as E

ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'_Documentation/BankLoadFix_20261104'
native=(DOC/'PowerSourceStats.native.cs.txt').read_text(encoding='utf-8-sig')
init=native.split('public override void Init()',1)[1].split('public void btnRefuel_OnHover',1)[0]
lookups=set(re.findall(r'GetChildById\("([^"]+)"\)',init))
assert {'sprFuelFill','sprFillPotential','windowIcon','btnRefuel','btnOn','clickable','lblOnOff','sprOnOff'}==lookups

def window(path):
    return E.parse(str(path)).xpath("//window[@name='windowPowerSourceStats']")[0]

def missing(root):
    return {name for name in lookups if not root.xpath('.//*[@name=$name]',name=name)}

before=window(DOC/'standalone_bank_windows.before.xml')
assert missing(before)=={'sprFuelFill','sprFillPotential'},missing(before)
current=window(ROOT/'Config/XUi_InGame/standalone_bank_windows.xml')
assert not missing(current),missing(current)
for name in ('sprFuelFill','sprFillPotential','sprOnOff'):
    nodes=current.xpath('.//*[@name=$name]',name=name)
    assert len(nodes)==1 and nodes[0].tag=='sprite',name
assert current.xpath(".//label[@name='lblOnOff']")
for name in ('btnOn','btnRefuel'):
    buttons=current.xpath(".//*[@name=$name]//button[@name='clickable']",name=name)
    assert len(buttons)==1,name
fuel=current.xpath(".//rect[@name='bankMetric2']")[0]
assert fuel.get('visible')=='{showfuel}'
for name,binding in [('sprFuelFill','fuelfill'),('sprFillPotential','potentialfuelfill')]:
    sprite=fuel.xpath('.//sprite[@name=$name]',name=name)[0]
    assert sprite.get('fill')=='{'+binding+'}' and sprite.get('type')=='filled'
    assert 'case "'+binding+'":' in native
    x,y=map(int,sprite.get('pos').split(','))
    assert 0<=x and x+int(sprite.get('width'))<=int(fuel.get('width'))
    assert 0<=-y and -y+int(sprite.get('height'))<=int(fuel.get('height'))
for name in ('sprFuelFill','sprFillPotential'):
    altered=E.fromstring(E.tostring(current));node=altered.xpath('.//*[@name=$name]',name=name)[0]
    node.getparent().remove(node);assert missing(altered)=={name}
print('PASS: pre-fix XML reproduces missing pair; all 8 native lookup names/types/scoped buttons now present; real fuel bindings and card bounds valid; removing either required sprite is detected.')
print('Offline contract validation only. No game launch, save load, or native UI initialization performed.')
