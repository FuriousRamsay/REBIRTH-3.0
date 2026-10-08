"""Real menu input regression for the cached Character portrait; disposable bridge save only.
Screenshots require visual inspection; this script does not certify model appearance.
"""
import sys, time, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'TraderVoices'))
from test_live import call, console, root

assert call('/ping')['state'] == 'ingame'
call('/surroundings')
call('/cleararea', 'POST', radius=40)
call('/restore', 'POST')
for stat in ('hydration', 'nutrition', 'energy'):
    console('rbmet set ' + stat + ' 95')
seq = call('/ping')['lastLogSeq']
results = []
for index in range(3):
    for attempt in range(5):
        windows = call('/ui')['openWindows']
        if not any(w in windows for w in ('crafting', 'rebirthSurvivorCharacter', 'ingameMenu')):
            break
        call('/key', 'POST', name='Escape')
        time.sleep(.5)
    call('/key', 'POST', name='Tab')
    time.sleep(1)
    assert 'crafting' in call('/ui')['openWindows']
    call('/ui/click', 'POST', text='Character')
    time.sleep(1)
    assert 'rebirthSurvivorCharacter' in call('/ui')['openWindows']
    row = {'iteration': index, 'state': call('/state', sections='rendering,performance'),
           'screenshot': call('/screenshot', 'POST', name='character_cached_reopen_' + str(index))['path']}
    call('/key', 'POST', name='Escape')
    time.sleep(.5)
    row['closed'] = call('/state', sections='rendering')
    results.append(row)
errors = call('/log', since=seq, level='error')
(root / '_Documentation/ReleaseFollowup/character_reopen.json').write_text(
    json.dumps({'results': results, 'errors': errors}, indent=2))
assert errors['count'] == 0, errors
print('PASS three real Character open/close cycles, no errors; inspect saved screenshots')
