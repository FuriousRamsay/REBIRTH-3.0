"""Actual hover regression for the consumable binding fast path. CodexTest only."""
from test_inventory_scroll_live import close, open_window
from test_capacity_live import call, root
import json, time

assert call('/ping')['state'] == 'ingame'
call('/surroundings')
call('/cleararea', 'POST', radius=40)
start = call('/ping')['lastLogSeq']
results = []
for window in ('crafting', 'rebirthSurvivorCharacter'):
    open_window(window)
    for item, expected in (('drinkJarBoiledWater', 'Remaining'),
                           ('foodBakedPotato', 'Nutrition')):
        call('/ui/hover', 'POST', window=window, item=item)
        time.sleep(.9)
        tree = call('/ui/tree', window=window)
        labels = [n.get('text', '') for n in tree['nodes']]
        assert any(expected in text for text in labels), (window, item, labels)
        shot = call('/screenshot', 'POST', name='binding_' + window + '_' + item)['path']
        results.append({'window': window, 'item': item, 'labels': labels, 'screenshot': shot})
        print('PASS', window, item, expected, flush=True)
close()
errors = call('/log', since=start, level='error')
assert errors['count'] == 0, errors
(root/'_Documentation/ReleaseFollowup/consumable_bindings.json').write_text(json.dumps(results, indent=2))
