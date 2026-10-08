"""Real input regression: natural stone pickup or felling a tree in disposable CodexTest.
Usage: python test_resource_harvest_live.py stone|tree X Y Z
For stone: stand within reach, point coordinates at the visible stone surface; active hand must be empty.
For tree: stand within reach with a stone axe selected; coordinates must hit the trunk.
Fixtures are never created by this test. Wood remains deferred until the tree falls.
"""
import sys, time, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'TraderVoices'))
from test_live import call, root
mode, x, y, z = sys.argv[1:]
assert mode in ('stone', 'tree')
assert call('/ping')['state'] == 'ingame'
call('/surroundings')
call('/cleararea', 'POST', radius=40)
call('/restore', 'POST')
call('/lookat', 'POST', x=float(x), y=float(y), z=float(z))
target = call('/target')['target']
assert 'block' in target, target
before = call('/state', sections='inventory,player')
seq = call('/ping')['lastLogSeq']
item = 'resourceRockSmall' if mode == 'stone' else 'resourceWood'
count = lambda state: sum(s['count'] for s in state['backpack'] if s['name'] == item)
if mode == 'stone':
    assert before['player']['holding'] is None, 'Select an empty active hand'
    assert 'Pick Up' in target['block'].get('activationText', ''), target
    call('/key', 'POST', name='E')
    time.sleep(1)
else:
    assert before['player']['holding']['name'] == 'meleeToolRepairT0StoneAxe'
    assert target['block']['name'].startswith('tree'), target
    call('/press', 'POST', action='Primary', hold=1)
    try:
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            time.sleep(1)
            if count(call('/state', sections='inventory')) > count(before): break
    finally:
        call('/release', 'POST', action='Primary')
after = call('/state', sections='inventory,player')
errors = call('/log', since=seq, level='error')
result = {'target': target, 'before': before, 'after': after,
          'gain': count(after) - count(before), 'errors': errors,
          'screenshot': call('/screenshot', 'POST', name=mode + '_resource_regression')['path']}
(root / ('_Documentation/ReleaseFollowup/' + mode + '_resource_regression.json')).write_text(json.dumps(result, indent=2))
assert result['gain'] > 0, result
assert errors['count'] == 0, errors
if mode == 'stone': assert before['toolbelt'] == after['toolbelt'], 'Stone entered toolbelt'
print('PASS', mode, 'backpack gain', result['gain'])
