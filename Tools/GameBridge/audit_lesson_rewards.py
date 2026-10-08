"""Current-source lesson reward audit; not runtime redemption validation."""
from pathlib import Path
from lxml import etree as E
import csv, json, re

root = Path(__file__).resolve().parents[2]
lessons = E.parse(str(root/'Config/_Rebirth/challenges.xml'))
events = E.parse(str(root/'Config/gameevents.xml'))
localization = dict(row for row in csv.reader((root/'Config/Localization.csv').open(encoding='utf-8-sig')) if len(row)==2)
# These introductory grants must remain resolvable against the installed base
# item catalog; this deliberately does not pretend to apply the mod patch graph.
base_items_path = root.parents[1]/'Data/Config/items.xml'
base_items = set(E.parse(str(base_items_path)).xpath('/items/item/@name'))
report = []
legacy = []
for lesson in lessons.xpath('//challenge | //challenge_group'):
    name = lesson.get('name')
    event = lesson.get('reward_event')
    if name=='rebirthLearningLegacy' and lesson.tag=='challenge_group':
        assert not event
        legacy.append(name)
        continue
    assert event, (name, 'missing reward event')
    matches = events.xpath('//action_sequence[@name=$name]', name=event)
    assert len(matches)==1, (name, event, 'missing or ambiguous event')
    actions = matches[0].findall('action')
    if event=='rebirthLessonRewardLegacy':
        assert not actions, 'Legacy compatibility lessons must not grant duplicate rewards'
        legacy.append(name)
        continue
    assert actions and all(a.get('class')=='AddItems' for a in actions), (name, 'non-item reward action')
    assert lesson.get('reward_text_key') in localization, (name, 'missing reward text')
    items = []
    for action in actions:
        names = action.xpath('./property[@name="added_items"]/@value')
        counts = action.xpath('./property[@name="added_item_counts"]/@value')
        assert len(names)==len(counts)==1, (name, 'invalid item/count properties')
        item_names, item_counts = names[0].split(','), counts[0].split(',')
        assert len(item_names)==len(item_counts), (name, 'count mismatch')
        for item, count in zip(item_names, item_counts):
            assert item.strip() and int(count)>0, (name, 'empty item/nonpositive count')
            assert item.strip() in base_items, (name, item, 'reward missing from installed base item catalog')
            items.append({'item':item.strip(),'count':int(count)})
    reward_text = localization[lesson.get('reward_text_key')]
    # Current lessons grant one item kind. Fail explicitly if the design expands
    # so a multi-item reward cannot silently pass a single-quantity label check.
    assert len(items)==1, (name, 'multi-item label audit required')
    displayed_count = re.fullmatch(r'(?:Section bonus: )?(\d+) .+', reward_text)
    assert displayed_count and int(displayed_count[1])==items[0]['count'], (name, reward_text, 'displayed quantity differs from grant')
    report.append({'lessonOrGroup':name,'event':event,'rewardText':reward_text,'items':items})
assert report
out=root/'_Documentation/Progression_Loot/lesson_reward_source_audit.json'
out.write_text(json.dumps({'scope':'Authored source only; no in-game redemption or full-inventory proof.', 'legacyWithoutRewards':legacy, 'rewards':report},indent=2)+'\n')
print(f'PASS: {len(report)} lesson/group reward references resolve to existing base items with matching displayed quantities; {len(legacy)} legacy entries grant nothing; no player XP actions.')
