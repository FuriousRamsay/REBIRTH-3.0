"""Read the game's five-second FPS counter, without an injected profiler.

Sequential samples are observations, not a controlled before/after benchmark.
"""
from test_inventory_scroll_live import close, open_window
from test_capacity_live import call, root
import json, time

results = []
out = root / '_Documentation/ReleaseFollowup/native_fps.json'

def measure(label):
    samples = []
    for _ in range(2):
        time.sleep(5.5)
        samples.append(call('/state', sections='performance')['performance'])
    results.append({'label': label, 'samples': samples,
                    'windows': call('/ui')['openWindows']})
    out.write_text(json.dumps(results, indent=2))
    print(label, [round(s['fps'], 1) for s in samples], flush=True)

if __name__ == '__main__':
    assert call('/ping')['state'] == 'ingame'
    call('/surroundings')
    call('/cleararea', 'POST', radius=40)
    call('/restore', 'POST')
    close()
    measure('idle_169_before')
    for window in ('crafting', 'rebirthSurvivorCharacter', 'map', 'quests',
                   'challenges', 'rebirthJournal', 'players'):
        open_window(window)
        measure(window + '_169')
    close()
    measure('idle_169_after')
    for z, label in ((997, 'campfire_169'), (995, 'forge_169')):
        call('/activate', 'POST', x=12, y=45, z=z, block=1)
        time.sleep(.5)
        assert any(w.startswith('workstation_') for w in call('/ui')['openWindows'])
        measure(label)
        close()
