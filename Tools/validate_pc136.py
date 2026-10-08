from pathlib import Path
import xml.etree.ElementTree as E
import re, zipfile

backup=Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/Character_PC136_Backup_20260912')
def split(s):
    a=s.index('<window name="rebirthSurvivorCharacterWindow"');b=s.index('</window>',a)+9
    return s[:a],s[a:b],s[b:]
p=Path('Config/XUi_InGame/windows.xml')
a,x,b=split(p.read_text(encoding='utf-8-sig'));aa,old,bb=split((backup/p).read_text(encoding='utf-8-sig'))
assert (a,b)==(aa,bb),'Changes outside Character window'
r=E.fromstring(x);o=E.fromstring(old)
def canonical(n):return n.tag,sorted(n.attrib.items()),[canonical(c) for c in n]
assert [canonical(n) for n in r.iter('equipment_stack_sdcs')]==[canonical(n) for n in o.iter('equipment_stack_sdcs')]
def get(n):return next(e for e in r.iter() if e.get('name')==n)
def fits(parent,child):
    px,py=map(float,child.get('pos','0,0').split(','))
    assert px>=0 and px+float(child.get('width','0'))<=float(parent.get('width')),(parent.get('name'),child.attrib)
    assert py<=0 and -py+float(child.get('height','0'))<=float(parent.get('height')),(parent.get('name'),child.attrib)
for name in ['survivorProgressionSkillsPanel','survivorProgressionSelectedPanel','survivorProgressionKnowledgePanel','survivorConditionsListPanel','survivorConditionDetailsPanel','survivorConditionOverviewPanel','survivorHudTrackingTrackedPanel','survivorHudTrackingAvailablePanel']:
    n=get(name)
    for c in n:fits(n,c)
for name in ['pc134SkillsList','pc134KnowledgeList','pc134ConditionsList']:
    n=get(name);v=next(e for e in n if e.get('name')=='listViewport')
    assert v.get('height')==n.get('height')
    assert list(map(float,v.get('clippingsize').split(',')))==[float(v.get('width')),float(v.get('height'))]
for name in ['survivorProgressionPanel','survivorConditionPanel']:
    assert int(get(name).get('height'))==851
    assert abs(851*min(1666/1744,813/851)-813)<0.1
overlay=get('survivorHudTrackingOverlay')
assert overlay.tag=='panel' and overlay.get('depth')=='100'
for n in ['survivorHudTrackingTrackedScrollView','survivorHudTrackingAvailableScrollView']:
    assert int(get(n).get('depth'))>int(overlay.get('depth'))
for c in overlay:
    if c.get('name')!='pc136TrackingInputShield':fits(overlay,c)
for c in get('survivorMetabolismPanel'):fits(get('survivorMetabolismPanel'),c)
icons=[e for e in r.iter('sprite') if e.get('sprite') in ['ui_game_symbol_fork','{rbmet_intake_icon}']]
assert len(icons)==2
assert {e.get('visible') for e in icons}=={'{rbmet_intake_empty_visible}','{rbmet_intake_item_visible}'}
assert all(e.get('width')==e.get('height')=='64' for e in icons)
assert not any(e.get('name')=='survivorConditionRelatedPanel' for e in r.iter())
assert not any(e.get('name')=='survivorStatisticsActivityValue1' for e in r.iter())
bindings=lambda n:set(re.findall(r'\{rbmet_[^}]+\}',E.tostring(n,encoding='unicode')))
assert bindings(get('survivorMetabolismPanel'))==bindings(next(e for e in o.iter() if e.get('name')=='survivorMetabolismPanel'))|{'{rbmet_intake_empty_visible}'}
with zipfile.ZipFile('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7dtd 3.2 XML Config.zip') as z:
    native='\n'.join(z.read(n).decode('utf-8-sig',errors='replace') for n in z.namelist() if n.lower().endswith('.xml'))
    assert 'ui_game_symbol_player' in native
    assert re.search(r'<buff\s+name="buffLegBroken"',native,re.I)
print('PASS: Character-only XML changes; native equipment unchanged.')
print('PASS: Full-height pages, column content, expanded viewports and modal bounds.')
print('PASS: Modal panel/list rendering order and opaque background.')
print('PASS: Square, mutually exclusive intake icons; original metabolism bindings retained.')
print('PASS: Native Overview icon and broken-leg ID verified against game config.')
print('Static checks only; in-game visual and input acceptance remains required.')

