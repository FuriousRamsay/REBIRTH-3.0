from pathlib import Path
from lxml import etree as E
from PIL import Image
import csv,json
R=Path(__file__).resolve().parents[2]
entries=E.parse(str(R/'Resources/Journal/entries.xml')).getroot()
loc={}
with (R/'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as source:
    for line,row in enumerate(csv.reader(source),1):
        if not row or row[0].startswith('#'):continue
        assert len(row)==2,('localization column count',line,len(row))
        assert row[0] not in loc,('duplicate localization key',line,row[0])
        loc[row[0]]=row[1]
challenges={e.get('name').lower():e for e in E.parse(str(R/'Config/_Rebirth/challenges.xml')).xpath('//challenge')}
ids=set(); mapped=set()
for e in entries:
    id=e.get('id');assert id and id not in ids,id;ids.add(id)
    assert e.get('trigger') in ['intro','ingestion','activity','completed'],id
    for tag in ['title','body']:assert e.findtext(tag) in loc,(id,tag)
    assert len(loc[e.findtext('body')].split())>=15,id
    if e.findtext('image'):
        f=R/'Resources/Journal/Images'/e.findtext('image');im=Image.open(f);assert im.width>=1280 and im.height>=720,f
    for cid in filter(None,e.get('challenges','').split(',')):
        assert cid.lower() in challenges,cid;mapped.add(cid.lower())
    if id.startswith('Route'):assert e.get('trigger')=='completed',id
assert not any(id.startswith('Skill_') for id in ids),'Weapon practice must share one guide'
assert len(entries.xpath("entry[@trigger='intro']"))==1
practice=entries.xpath("entry[@id='Practice']")[0]
assert len([c for c in practice.get('challenges').split(',') if c.startswith('rebirthLessonSkill_')])==48
assert not entries.xpath("entry[@id='Medicine']/image[text()='metabolism.png']"),'Injury guide must not show digestion'
assert set(challenges)==mapped,sorted(set(challenges)-mapped)
ui=E.parse(str(R/'Config/XUi_InGame/journal_guides.xml'))
for e in ui.xpath('//*[@text_key]'):assert e.get('text_key') in loc,e.get('text_key')
print(json.dumps({'pass':True,'entries':len(entries),'shared_practice_guides':1,'challenge_links':len(mapped),'illustrations':len(set(entries.xpath('entry/image/text()'))),'validation':'Static authoring only; runtime unlock/read/save checks are separate'}))
