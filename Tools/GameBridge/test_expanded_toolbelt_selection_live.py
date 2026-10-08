"""Physical expanded-toolbelt selection; requires equipped >10-slot fixture in disposable CodexTest.
Does not change the background or gear. Keeps guard on; restores the original selection.
"""
import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'TraderVoices'))
from test_live import call, root

assert call('/ping')['state'] == 'ingame'
call('/surroundings')
call('/cleararea', 'POST', radius=40)
call('/restore', 'POST')
assert not any(w in call('/ui')['openWindows'] for w in ('crafting', 'rebirthSurvivorCharacter', 'looting')), 'Close inventory windows first'
before = call('/state', sections='player,inventory')
capacity = before['toolbeltCapacity']
assert capacity > 10, 'Equip an expanded belt before this test; no fixture mutation is performed'
original = before['player']['holdingSlot'] + 1
seq = call('/ping')['lastLogSeq']
results = []
passed = False
try:
    for slot in range(11, capacity + 1):
        for target in (slot, 1):
            result = call('/select', 'POST', slot=target)
            assert result['holdingSlot'] == target, result
            actual = call('/state', sections='player,inventory')
            assert actual['player']['holdingSlot'] == target - 1, actual['player']
            assert actual['toolbelt'] == before['toolbelt'], 'Selection changed toolbelt items'
            assert actual['backpack'] == before['backpack'], 'Selection changed backpack items'
            results.append({'target': target, 'actual': actual['player']['holdingSlot'] + 1})
    errors = call('/log', since=seq, level='error')
    assert errors['count'] == 0, errors
    passed = True
finally:
    if 1 <= original <= capacity:
        call('/select', 'POST', slot=original)
    (root / '_Documentation/ReleaseFollowup/expanded_toolbelt_selection.json').write_text(
        json.dumps({'capacity': capacity, 'steps': results,
                    'complete': passed}, indent=2))
print('PASS expanded native shortcuts, lower-slot return, inventory conservation and error checks')
