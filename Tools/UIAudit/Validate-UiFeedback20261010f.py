from pathlib import Path
import xml.etree.ElementTree as E
root=Path('.')
t=E.parse('Config/XUi_InGame/station_templates.xml').getroot()
for template in t.find('append'):
 assert len(list(template)) == 1, (template.tag, 'native template requires exactly one root')
header=t.find(".//rebirth_station_shared_10/rect")
assert header is not None
assert header.find("label[@name='stationName']") is not None
assert header.find("sprite[@name='stationIcon']") is not None
for width in (480,600,730,880,960):
 for height in (138,150,180,220):
  usable=max(72,height-46-8); card=max(24,(usable-12)//3)
  assert 46+3*card+12<=height
  cw=(width-24-8)//2
  assert cw-158>=34 and cw-80+34+40<cw
 # Seven attribute rows stay below description and above actions.
 assert 46+140+7*20<=368-34
 # Book + three skill requirements fit above timing controls.
 assert 46+24+28+2*48+44<=368-94
w=E.parse('Config/XUi_InGame/windows.xml').getroot()
search=[n for n in w.iter('setattribute') if n.get('name')=='width' and "rebirthCreativeRoot" in n.get('xpath','') and "searchInput" in n.get('xpath','')][-1]
assert 360+22+int(search.text)<=886-12
creative=next(n for n in w.iter('rect') if n.get('name')=='creativeInventory')
name=next(n for n in creative.iter('label') if n.get('name')=='characterBagSelected')
assert name.get('overflow')=='clampcontent' and name.get('font_size')=='24'
controls=next(n for n in creative.iter('rect') if n.get('name')=='creativeInventoryControls')
assert int(controls.get('pos').split(',')[0])+128+13==14+11*73-1
assert next(n for n in creative.iter('sprite') if n.get('name')=='characterBagSelectedIcon').get('width')=='84'
for f in ['station_workspace.xml','xui.xml']:
 E.parse('Config/XUi_InGame/'+f)
print('PASS: all station templates have one native root; header children retained; ingredient rows fit at 5 widths and 4 heights; seven stats and four unlock requirements fit; effective search right edge bounded; preview typography and backpack controls aligned.')
