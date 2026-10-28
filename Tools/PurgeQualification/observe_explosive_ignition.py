"""Observe real secondary activation, then promptly throw; never write item Meta.

Prepare the disposable player holding a pipe bomb on clear ground first. This is
an input diagnostic, not a kill/category acceptance test. No setup or launch.
"""
import json
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
session = json.loads((ROOT / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
records = []

def call(endpoint, post=False):
    request = urllib.request.Request(
        f"http://127.0.0.1:{session['port']}{endpoint}",
        data=b'' if post else None,
        headers={'X-Bridge-Token': session['token']},
        method='POST' if post else 'GET')
    with urllib.request.urlopen(request, timeout=10) as response:
        value = json.load(response)
    records.append({'time': time.monotonic(), 'endpoint': endpoint, 'response': value})
    if value.get('ok') is not True:
        raise RuntimeError(f'Bridge failed: {endpoint}')
    return value

output = Path('E:/_Haven/shared/REBIRTHTemp') / ('explosive_ignition_' + time.strftime('%Y%m%d-%H%M%S') + '.json')
try:
    if call('/ping').get('state') != 'ingame':
        raise RuntimeError('Disposable game must already be loaded')
    before = call('/state?sections=player,ui')
    player = before.get('player', {})
    if player.get('dead') is not False:
        raise RuntimeError('Player must be alive; no inputs sent')
    blockers = {'crafting', 'rebirthSurvivorCharacter', 'looting', 'console'}
    if blockers.intersection(before.get('openWindows', [])):
        raise RuntimeError('Close gameplay-blocking windows first; no inputs sent')
    held = player.get('holding') or {}
    if held.get('name') not in ('thrownAmmoPipeBomb', 'thrownGrenade'):
        raise RuntimeError('Prepare a supported explosive first; no inputs sent')
    if 'meta' not in held:
        raise RuntimeError('Rebuild the read-only Meta diagnostic first')
    time.sleep(1.1)
    try:
        call('/press?action=Secondary&seconds=0.12', True)
        call('/state?sections=player,inventory')
    finally:
        # A live fuse must not be held while waiting for another tool round trip.
        call('/press?action=Primary&seconds=0.30', True)
    time.sleep(5)
    call('/state?sections=player,inventory')
finally:
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(records, indent=2), encoding='utf-8')
    print(output)
