"""Real mount/dismount regression; requires an owned motorcycle within 4m in CodexTest."""
import sys, time, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'TraderVoices'))
from test_live import call, root

assert call('/ping')['state'] == 'ingame'
call('/surroundings')
call('/cleararea', 'POST', radius=40)
bikes = call('/entities', radius=4, contains='vehicleMotorcycle')['entities']
assert bikes, 'Place the test motorcycle nearby before running'
bike = min(bikes, key=lambda b: b['distance'])
before = call('/state', sections='player')['player']
start = call('/ping')['lastLogSeq']
results = []
for index in range(3):
    call('/activate', 'POST', entity=bike['id'])
    time.sleep(1)
    mounted = call('/state', sections='player')['player']
    shot = call('/screenshot', 'POST', name=f'motorcycle_mount_{index}')['path']
    assert call('/log', since=start, level='error')['count'] == 0
    call('/key', 'POST', name='E')
    time.sleep(1)
    after = call('/state', sections='player')['player']
    assert after['holdingSlot'] == before['holdingSlot'], (before, after)
    assert after['holding'] == before['holding'], (before, after)
    assert call('/log', since=start, level='error')['count'] == 0
    results.append({'cycle': index + 1, 'mounted': mounted, 'dismounted': after, 'screenshot': shot})
    print('PASS mount/dismount', index + 1, flush=True)
(root/'_Documentation/ReleaseFollowup/motorcycle_mount.json').write_text(json.dumps(results, indent=2))
