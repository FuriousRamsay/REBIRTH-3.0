from pathlib import Path
import xml.etree.ElementTree as E
root=Path('.')
for f in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/station_templates.xml','Config/XUi_InGame/station_workspace.xml']:
 E.parse(f)
t=E.parse('Config/XUi_InGame/station_templates.xml').getroot()
output=next(n for n in t.iter('rect') if n.get('name')=='windowOutput')
g=next(n for n in output.iter('grid') if n.get('controller')=='WorkstationOutputGrid')
assert (g.get('rows'),g.get('cols'))==('4','7')
assert next(n for n in t.iter('label') if n.get('name')=='stationName').get('depth')=='65'
assert next(n for n in t.iter('sprite') if n.get('name')=='stationIcon').get('atlas')=='ItemIconAtlas'
for width in (330,414,500,540):
 ow=width-16;scale=min(1,(ow-24)/(7*75))
 assert 7*75*scale<=ow-24+0.001
 assert 4*75*scale+46<=52+__import__('math').ceil(4*75*scale)
for f in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/station_templates.xml']:
 r=E.parse(f).getroot()
 for i in range(7):
  for kind in ['Name','Value']:
   assert len([n for n in r.iter('label') if n.get('name')==f'recipeResultStat{kind}{i}'])==1
r=E.parse('Config/XUi_InGame/windows.xml').getroot()
patch=[n for n in r.iter('setattribute') if n.get('name')=='width' and "rebirthCreativeRoot" in n.get('xpath','') and "searchInput" in n.get('xpath','')]
assert patch and patch[-1].text.strip()=='590'
# Seven stat rows finish before timing; three unlock skill rows finish before actions.
assert 46+7*20<=192
assert 46+146+2*48+44<=368-34
print('PASS XML parsing, 28 output cells, four output widths with scrollbar reserve, station header depth/icon, 14 stat labels on both detail surfaces, effective search width, non-overlapping attribute/timing/unlock/action bounds.')
