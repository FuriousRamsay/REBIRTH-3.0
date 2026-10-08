from pathlib import Path
import xml.etree.ElementTree as E
import re
root=Path.cwd();backup=Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/Character_PC134_Backup_20260912')
p=root/'Config/XUi_InGame/windows.xml';s=p.read_text();before=(backup/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8-sig')
def split(s):
 a=s.index('<window name="rebirthSurvivorCharacterWindow"');b=s.index('</window>',a)+9;return s[:a],s[a:b],s[b:]
a,xml,b=split(s);a0,old,b0=split(before)
assert a==a0 and b==b0,'Changes outside Character window'
w=E.fromstring(xml);o=E.fromstring(old)
def node(name):
 n=w.find('.//*[@name="'+name+'"]');assert n is not None,name;return n
def canonical(n):return (n.tag,sorted(n.attrib.items()),[canonical(c) for c in n])
assert [canonical(n) for n in w.iter('equipment_stack_sdcs')]==[canonical(n) for n in o.iter('equipment_stack_sdcs')]
print('PASS: Non-Character XML and all native equipment controls unchanged.')
for kind in ['Backpack','Belt','Support']:
 card=node('survivorOverviewGear'+kind+'Card')
 print(kind,[(c.tag,c.attrib) for c in card])
for name in ['pc134ConditionsList','pc134HistoryList','pc134SkillsList','pc134KnowledgeList']:
 n=node(name);view=n.find('panel');content=view.find('rect');rows=list(content)
 assert len(rows)>0
 assert len({c.get('name') for c in rows})==len(rows)
 print(name,'rows=',len(rows),'viewport=',view.get('width'),view.get('height'),'stride=',abs(int(rows[1].get('pos').split(',')[1])-int(rows[0].get('pos').split(',')[1])))
met=node('survivorMetabolismPanel')
bad=[]
for c in met:
 x,y=map(float,c.get('pos','0,0').split(','));width=float(c.get('width','0'));height=float(c.get('height','0'))
 if x<0 or x+width>1666 or -y+height>813:bad.append(c.attrib)
assert not bad,bad
source=E.parse('Config/_Metabolism/windows.xml').find('.//*[@name="rebirthCharacterMetabolism"]')
bindings=lambda n:set(re.findall(r'\{rbmet_[^}]+\}',E.tostring(n,encoding='unicode')))
assert bindings(met)==bindings(source),'Metabolism live binding loss'
print('PASS: Metabolism controls contained; complete Base Game presentation bindings retained.')
print('PASS: XML parses. Static checks do not establish in-game appearance or input behavior.')
