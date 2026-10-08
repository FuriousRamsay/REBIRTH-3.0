"""Static production-organizer conformance; not a native tracking/save playtest."""
import copy, tempfile
from pathlib import Path
from lxml import etree as E
import organize_learning_challenges as curriculum
import learning_rewards as rewards
root=curriculum.ROOT
source=E.parse(str(root/'Config/_Rebirth/challenges.xml')).getroot()
def evidence(tree):
    return {n.get('name'): tuple(E.tostring(o) for o in n.findall('objective'))
            for n in tree.xpath('append/challenge')}
before=evidence(source)
with tempfile.TemporaryDirectory(prefix='rebirth-curriculum-') as directory:
    sandbox=Path(directory)
    (sandbox/'Config/_Rebirth').mkdir(parents=True)
    (sandbox/'_Documentation').mkdir()
    for relative in ('Config/_Rebirth/challenges.xml','Config/gameevents.xml','Config/Localization.csv'):
        (sandbox/relative).write_bytes((root/relative).read_bytes())
    curriculum.ROOT=rewards.ROOT=sandbox
    texts={}
    tree=copy.deepcopy(source)
    curriculum.organize(tree,texts)
    assert evidence(tree)==before, 'Stable IDs or objective definitions changed'
    groups=tree.xpath('append/challenge_group')
    for key,title,names in curriculum.SECTIONS:
        group='rebirthLearn'+key
        assert next(g for g in groups if g.get('name')==group).get('link_challenges')=='true'
        actual=[n.get('name') for n in tree.xpath('append/challenge') if n.get('group')==group]
        assert actual==['rebirthLesson'+suffix for suffix in names.split()]
    assert next(g for g in groups if g.get('name')=='rebirthLearningLegacy').get('link_challenges')=='false'
    items={n.get('name') for n in E.parse(str(root/'Config/_Survivor/items.xml')).xpath('//item')}
    generated=E.parse(str(sandbox/'Config/gameevents.xml'))
    for skill,edition in rewards.SKILL_REFERENCES.items():
        item='rebirthTheory'+edition+'Primer'
        assert item in items, item
        name='rebirthLessonSkill_'+skill
        lesson=tree.xpath(f"append/challenge[@name='{name}']")
        assert len(lesson)==1
        event=lesson[0].get('reward_event')
        assert event=='rebirthLessonRewardSkill_'+skill
        sequences=generated.xpath(f"//action_sequence[@name='{event}']")
        assert len(sequences)==1
        assert sequences[0].xpath("action/property[@name='added_items']/@value")==[item]
        assert sequences[0].xpath("action/property[@name='added_item_counts']/@value")==['1']
        assert 'collecting the book does not raise Skill or Theory' in texts[name+'Desc']
    native_items=E.parse(str(Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/items.xml')))
    items.update(n.get('name') for n in native_items.xpath('//item'))
    assert set(rewards.SECTION_REWARDS)==set(rewards.REWARDS)
    for key,(item,count,label) in rewards.SECTION_REWARDS.items():
        assert item in items and count>0
        event='rebirthLessonReward'+key+'Section'
        group=tree.xpath(f"append/challenge_group[@name='rebirthLearn{key}']")[0]
        assert group.get('reward_event')==event
        sequence=generated.xpath(f"//action_sequence[@name='{event}']")
        assert len(sequence)==1
        assert sequence[0].xpath("action/property[@name='added_items']/@value")==[item]
        assert sequence[0].xpath("action/property[@name='added_item_counts']/@value")==[str(count)]
        assert texts[event]=='Chapter reward: '+label
    first=E.tostring(tree)
    events=(sandbox/'Config/gameevents.xml').read_bytes()
    curriculum.organize(tree,texts)
    assert E.tostring(tree)==first, 'Second organization changed curriculum'
    assert (sandbox/'Config/gameevents.xml').read_bytes()==events, 'Second organization changed reward events'
print('PASS: 76 lessons/13 native linked chapters, exact stable objectives, retired aliases unlinked, organizer/rewards idempotent. Native tracker/save/claim not exercised.')
# Maintained generators must preserve the accepted Overview water equipment route.
layout=(root/"Tools/GameBridge/layout_learning_challenges.py").read_text(encoding="utf-8-sig")
assert "btnRebirthMetabolismEquipHeld" not in layout
assert "btnRebirthMetabolismUnequip" not in layout
assert "btnRebirthMetabolismAutoSip" in layout
assert "Overview > Support" in texts["rebirthLessonRouteWaterRoutineDesc"]
