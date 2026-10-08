"""Matched idle/open/closed samples for real station input in disposable CodexTest.
Uses the established disposable station fixture at 12,45,995. No profiler injection.
"""
from test_inventory_scroll_live import close
from test_capacity_live import call, root
import time, json

out = root / '_Documentation/ReleaseFollowup/station_performance_pairs.json'
results = []
assert call('/ping')['state'] == 'ingame'
seq = call('/ping')['lastLogSeq']
close()
call('/walkto', 'POST', x=13.5, z=993.8)
call('/surroundings')
call('/cleararea', 'POST', radius=40)
call('/restore', 'POST')

def sample(expected=None):
    time.sleep(5.5)
    windows = call('/ui')['openWindows']
    assert expected is None or expected in windows, windows
    return {'state': call('/state', sections='performance,rendering,inventory'), 'windows': windows}

for name in ('campfire', 'forge', 'cementMixer', 'workbench', 'chemistryStation',
             'cntWoodBurningStove', 'WorkbenchGasStove001_FR',
             'WorkbenchMortarPestle001_FR', 'WorkbenchIronOven001_FR'):
    close()
    call('/setblock', 'POST', x=12, y=45, z=995, name=name)
    call('/console', 'POST', cmd='settime 1 9 0')
    for stat in ('hydration', 'nutrition', 'energy'):
        call('/console', 'POST', cmd='rbmet set ' + stat + ' 95')
    call('/lookat', 'POST', x=12, y=45, z=995, block=1)
    row = {'station': name, 'idle': sample()}
    for attempt in range(2):
        call('/activate', 'POST', x=12, y=45, z=995, block=1)
        time.sleep(.7)
        windows = [w for w in call('/ui')['openWindows'] if w.startswith('workstation_')]
        if windows: break
    assert windows, name
    row['open'] = sample(windows[0])
    row['screenshot'] = call('/screenshot', 'POST', name='paired_station_' + name)['path']
    close()
    row['closed'] = sample()
    row['errors'] = call('/log', since=seq, level='error')
    assert row['errors']['count'] == 0, row['errors']
    results.append(row)
    out.write_text(json.dumps(results, indent=2))
    print(name, {k: round(row[k]['state']['performance']['fps'], 1)
                 for k in ('idle', 'open', 'closed')}, flush=True)
