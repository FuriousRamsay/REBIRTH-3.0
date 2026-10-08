from pathlib import Path
from lxml import etree as E
import csv,json,hashlib
from organize_learning_challenges import SECTIONS,ALIASES
ROOT=Path(__file__).resolve().parents[2]
doc=E.parse(str(ROOT/'Config/_Rebirth/challenges.xml'))
lessons=doc.xpath('//challenge');names=[n.get('name') for n in lessons]
assert len(names)==74 and len(set(names))==74
loc={r[0]:r[1] for r in csv.reader((ROOT/'Config/Localization.csv').open(encoding='utf-8-sig')) if len(r)>1}
events=E.parse(str(ROOT/'Config/gameevents.xml'))
eventmap={e.get('name'):e for e in events.xpath('//action_sequence')}
assert not events.xpath('//action[@class="AddXP"]')
icons=[]
for lesson in lessons:
    for attr in ('title_key','description_key','reward_text_key'):assert lesson.get(attr) in loc
    assert lesson.get('reward_event') in eventmap
    icon=ROOT/'UIAtlases/UIAtlas'/(lesson.get('icon')+'.png');assert icon.exists()
    icons.append(hashlib.sha256(icon.read_bytes()).hexdigest())
assert len(set(icons))==74
visible=[l for l in lessons if l.get('group')!='rebirthLearningLegacy'];assert len(visible)==70
signatures=[tuple(sorted(tuple(sorted((k,v) for k,v in o.attrib.items() if k!='stat_text_key')) for o in l.findall('objective'))) for l in visible]
assert len(signatures)==len(set(signatures)), 'Repeated objective set'
categories={n.get('name') for n in doc.xpath('//challenge_category')}
assert all(g.get('category') in categories for g in doc.xpath('//challenge_group')), 'Unregistered challenge category'
groups=doc.xpath('//challenge_group[@category="RebirthLearning" and @name!="rebirthLearningLegacy"]')
assert [g.get('name') for g in groups]==['rebirthLearn'+key for key,_,_ in SECTIONS]
items={e.get('name') for e in E.parse(str(ROOT.parent.parent/'Data/Config/items.xml')).xpath('//item')}
for prop in events.xpath('//property[@name="added_items"]'):assert prop.get('value') in items
for file in ('Config/XUi_InGame/templates.xml','Config/XUi_InGame/windows.xml','Config/Localization.csv'):
    if file.endswith('.xml'):E.parse(str(ROOT/file))
report={'passed':True,'visibleLessons':70,'savedLegacyDefinitions':4,'orderedSections':13,'uniqueIcons':74,'uniqueVisibleObjectiveSets':70,'playerXpRewardActions':0,'validation':'Static XML/localization/item references; not runtime redemption proof'}
(ROOT/'_Documentation/ChallengeLearning/offline_audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
