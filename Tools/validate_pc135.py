from pathlib import Path
import xml.etree.ElementTree as E
import re,zipfile
from PIL import Image
backup=Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/Character_PC135_Backup_20260912')
p=Path('Config/XUi_InGame/windows.xml');s=p.read_text();old=(backup/p).read_text(encoding='utf-8-sig')
def split(s):
 a=s.index('<window name="rebirthSurvivorCharacterWindow"');b=s.index('</window>',a)+9;return s[:a],s[a:b],s[b:]
a,xml,b=split(s);aa,ox,bb=split(old);assert (a,b)==(aa,bb),'Non-Character XML changed'
w=E.fromstring(xml);o=E.fromstring(ox)
def canonical(n):return n.tag,sorted(n.attrib.items()),[canonical(c) for c in n]
assert [canonical(n) for n in w.iter('equipment_stack_sdcs')]==[canonical(n) for n in o.iter('equipment_stack_sdcs')]
print('PASS: Non-Character XML and native equipment controls preserved.')
def get(name):
 n=w.find('.//*[@name="'+name+'"]');assert n is not None,name;return n
for name in ['survivorProgressionSelectedPanel','survivorProgressionKnowledgePanel','survivorConditionDetailsPanel','survivorConditionOverviewPanel']:
 n=get(name);assert n.tag=='panel' and n.get('clipping')=='SoftClip'
 # Direct authored children, including list hosts, must fit without relying on clipping.
 for c in n:
  if 'pos' not in c.attrib:continue
  x,y=map(float,c.get('pos').split(','));width=float(c.get('width','0'));height=float(c.get('height','0'))
  assert x>=0 and x+width<=int(n.get('width')) and -y+height<=int(n.get('height')),(name,c.attrib)
print('PASS: Progression/Conditions column content fits explicit clipping bounds.')
met=get('survivorMetabolismPanel')
for c in met:
 x,y=map(float,c.get('pos','0,0').split(','));width=float(c.get('width','0'));height=float(c.get('height','0'))
 assert 0<=x and x+width<=1666 and -y+height<=813,c.attrib
bindings=lambda n:set(re.findall(r'\{rbmet_[^}]+\}',E.tostring(n,encoding='unicode')))
base=E.parse('Config/_Metabolism/windows.xml').find('.//*[@name="rebirthCharacterMetabolism"]')
assert bindings(met)==bindings(base)
print('PASS: Metabolism bounds and full original live-binding set preserved.')
for kind in ['backpack','belt','support']:
 f=Path('UIAtlases/RebirthUiIcons/rb_slot_'+kind+'.png');im=Image.open(f)
 assert im.mode=='RGBA' and im.getchannel('A').getextrema()==(0,255),(f,im.mode)
 print('PASS: Transparent generated asset',f,im.size)
known=set(re.findall('ui_game_symbol_[A-Za-z0-9_]+',old))
with zipfile.ZipFile('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7dtd 3.2 XML Config.zip') as z:
 for name in z.namelist():
  if name.lower().endswith('.xml'):
   known.update(re.findall('ui_game_symbol_[A-Za-z0-9_]+',z.read(name).decode('utf-8-sig',errors='replace')))
new=set(re.findall('ui_game_symbol_[A-Za-z0-9_]+',xml))-set(re.findall('ui_game_symbol_[A-Za-z0-9_]+',old))
print('Native icon names requiring replacement:',sorted(new-known))
for n in w.iter('sprite'):
 if n.get('name','').startswith('pc135') and n.get('atlas') not in [None,'UIAtlas']:
  assert Path('UIAtlases',n.get('atlas'),n.get('sprite')+'.png').exists(),n.attrib
print('PASS: New custom graphic references resolve.')
print('Static verification only: live screenshots, text fitting, input, and FPS still require in-game acceptance.')
