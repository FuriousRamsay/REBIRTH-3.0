from pathlib import Path
import xml.etree.ElementTree as ET,re,json,sys
root=Path(__file__).resolve().parents[2];prod=Path(r'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0');vanilla=Path(r'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/blocks.xml');allblocks={b.attrib['name']:b for b in ET.parse(vanilla).getroot().findall('block')};names=['palletCartonMilitaryBase','palletCartonMilitaryCap','palletCartonMilitaryTile','palletCartonMilitaryTileCovered'];decl={n:{} for n in names}
def props(el,prefix=()):
 out={}
 for p in el.findall('property'):
  if 'class' in p.attrib:out.update(props(p,prefix+(p.attrib['class'],)))
  else:out[prefix+(p.attrib['name'],)]=p.attrib.get('value','')
 return out
for n in names:decl[n]=props(allblocks[n])
changed=(root/'Tools/MilitaryBaseCompositeCompatibility/blocks.xml.baseline.txt') if '--negative' in sys.argv else root/'Config/_LootConversions/blocks.xml'
for p in [changed,prod/'Config/_BlockPickup/blocks.xml']:
 for op in ET.parse(p).getroot().findall('append'):
  for n in re.findall(r"@name='([^']+)'",op.attrib.get('xpath','')):
   if n in decl:decl[n].update(props(op))
def resolved(n):
 d=decl[n];parent=d.get(('Extends',));return {**(resolved(parent) if parent in decl else {}),**d}
rows={n:{'/'.join(k):v for k,v in resolved(n).items()} for n in names}
for n in names[:2]:
 assert rows[n].get('Class')=='RebirthLegacyMilitaryPalletBase, RebirthUtils','saved base/cap must resolve native-compatible schema'
 assert rows[n]['CompositeFeatures/TEFeatureStorage/LootList']=='rebirthMilitaryAmmoPalletLarge'
 assert rows[n].get('RebirthPickup')!='Allow','no base/cap pickup metadata introduced'
for n in names[2:]:
 assert rows[n]['Class']=='CompositeTileEntity','tile must retain independent native class'
 assert rows[n]['CompositeFeatures/TEFeatureStorage/LootList']=='rebirthMilitaryAmmoPalletLarger'
 assert rows[n]['RebirthPickup']=='Allow','tile pickup policy unchanged'
print('PASS native XML base/cap/tile inheritance and saved storage schema; per-instance runtime Deny has no XML inheritance leak')
(root/'Tools/MilitaryBaseCompositeCompatibility/xml-result.json').write_text(json.dumps(rows,indent=2))
