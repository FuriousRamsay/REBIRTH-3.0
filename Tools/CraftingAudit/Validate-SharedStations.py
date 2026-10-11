from pathlib import Path
import copy,xml.etree.ElementTree as E
r=E.parse('Config/XUi_InGame/station_templates.xml').getroot()
templates={e.tag:e for e in r.find('append')}
def expand(node):
 for c in list(node):
  if c.tag.startswith('rebirth_station_shared_'):
   assert c.tag in templates,c.tag
   i=list(node).index(c);node.remove(c)
   for sub in templates[c.tag]:node.insert(i,copy.deepcopy(sub));i+=1
  else:expand(c)
 # references may have expanded into containers
 for c in list(node):
  if any(x.tag.startswith('rebirth_station_shared_') for x in c.iter()):expand(c)
def named(root,name):return next(x for x in root.iter() if x.get('name')==name)
def canonical(x):return (x.tag,tuple(sorted(x.attrib.items())),(x.text or '').strip(),tuple(canonical(c) for c in x))
personal=named(E.parse('Config/XUi_InGame/windows.xml').getroot(),'rebirthPersonalCraftingRoot')
windows=E.parse('Config/XUi_InGame/station_workspace.xml').getroot()
for w in windows.iter('window'):
 expand(w)
 for id in ['rebirthCraftingLeftZone','rebirthCraftingCenterZone','rebirthCraftingTopZone']:
  assert canonical(named(w,id))==canonical(named(personal,id)),(w.get('name'),id)
 assert len(list(named(w,'rebirthCraftingQueueRowsHost')))==16
 grid=named(w,'rebirthCraftingInventoryScroll').find('.//grid')
 assert grid.get('cols')=='11'
 assert len([x for x in w.iter() if x.get('controller','').startswith('RebirthCraftingRecipeCatalogue,')])==1
 assert not any(x.get('name')=='herbsLabel' for x in w.iter())
 assert not any(x.get('controller')=='RecipeList' for x in w.iter())
 if w.get('name')=='rebirthStationRootMilling':
  assert len(list(named(w,'rebirthMillingProcessor')))==12
 print('PASS',w.get('name'),'canonical list/details/requirements/backpack/top; 16 saved queue cells; no herbs')
route=E.parse('Config/_Cooking/workspace_xui.xml').getroot()
a=next(e for e in route.iter('append') if 'MortarPestle' in e.get('xpath',''))
assert a.find('window').get('name')=='rebirthStationRootMilling'
print('PASS mortar route uses shared station presentation')
