from pathlib import Path
import xml.etree.ElementTree as E
root=Path('.')
r=E.parse('Config/XUi_InGame/windows.xml').getroot()
cat=next(n for n in r.iter() if n.get('name')=='creativeCatalogue')
rows=[n for n in cat.iter() if (n.get('name') or '').startswith('creativeCatalogueRow')]
assert len(rows)==14 and all(len(list(n.iter('item_stack')))==13 for n in rows)
vp=next(n for n in cat.iter() if n.get('name')=='creativeCatalogueViewport');assert vp.get('width')=='858' and vp.get('height')=='792'
assert 97+792==int(cat.get('height'))
inv=next(n for n in r.iter() if n.get('name')=='creativeInventory')
rows=[n for n in inv.iter() if (n.get('name') or '').startswith('characterBagRow')]
assert all(len(list(n.iter('item_stack')))==11 for n in rows[:-1])
panel=next(n for n in inv.iter() if n.get('name')=='survivorOverviewBackpackPanel')
assert len([n for n in panel if (n.get('name') or '').startswith('creativeStat')])==7
# This edit is restricted to the creative window; compare every other window structurally.
before=E.parse('_Documentation/UiFeedback_20261009d/before/Config/XUi_InGame/windows.xml').getroot()
def norm(n):return n.tag,sorted(n.attrib.items()),(n.text or '').strip(),[norm(c) for c in n]
def removecreative(r):
 for p in r.iter():
  for c in list(p):
   if c.tag=='window' and any(x.get('name')=='creativeCatalogue' for x in c.iter()):p.remove(c);return
removecreative(r);removecreative(before);assert norm(r)==norm(before),'unrelated windows changed'
st=E.parse('Config/XUi_InGame/station_workspace.xml').getroot();templates=E.parse('Config/XUi_InGame/station_templates.xml').getroot();defined={n.tag for a in templates for n in a}
for win in st.iter('window'):
 for n in win.iter():
  if n.tag.startswith('rebirth_station_shared_'):assert n.tag in defined
 for n in win.iter('item_stack'):assert n.get('repeat_i') is not None
for name in ['rebirthStationRootBasic','rebirthStationRootFuel']:
 win=next(n for n in st.iter('window') if n.get('name')==name);assert any(n.tag=='rebirth_station_shared_15' for n in win.iter())
print('PASS: creative 13-column pool, full-height catalogue, 11-column backpack, seven attributes, no unrelated XML changes, station tools and mortar template indices.')
