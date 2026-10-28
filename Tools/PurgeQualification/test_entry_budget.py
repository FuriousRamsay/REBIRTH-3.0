"""Real-input entry timeout probe. Run only in an already-loaded disposable CodexTest.
No build, launch, teleport, fabricated deaths or global input cancellation.
Requires clear, settled ground outside the supplied distant POI.
"""
import argparse, json, math, time, urllib.request, urllib.error
from pathlib import Path

root = Path(__file__).resolve().parents[2]
args = argparse.ArgumentParser()
args.add_argument('--poi', type=int, required=True)
args = args.parse_args()
session = json.loads((root/'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))

def call(path, post=False):
    request = urllib.request.Request(f"http://127.0.0.1:{session['port']}{path}",
        data=b'' if post else None, headers={'X-Bridge-Token':session['token']})
    try:
        reply = urllib.request.urlopen(request, timeout=15)
    except urllib.error.HTTPError as error:
        reply = error
    with reply:
        return json.load(reply)

folder = root/'Tools/GameBridge/out/purge_runs'/time.strftime('%Y%m%d-%H%M%S-entry-budget')
folder.mkdir(parents=True)
report = {}
try:
    report['before'] = call('/state?sections=player')
    assert report['before']['state'] == 'ingame'
    assert report['before']['guard']['enabled'] and not report['before']['guard']['busy']
    report['surroundings'] = call('/surroundings')
    assert report['surroundings']['count'] == 0, 'Clear nearby threats before this probe'
    started = time.monotonic()
    report['entry'] = call(f'/enterpoi?id={args.poi}&maxSeconds=2', True)
    report['wallSeconds'] = time.monotonic()-started
    assert report['entry']['result'] == 'timeout', report['entry']
    assert report['entry']['seconds'] < 3.5 and report['wallSeconds'] < 6
    # Let existing physical momentum settle, then detect continued walking.
    time.sleep(0.6)
    report['settled'] = call('/state?sections=player')
    time.sleep(2)
    report['later'] = call('/state?sections=player')
    first = report['settled']['player']['position']
    last = report['later']['player']['position']
    report['horizontalDrift'] = math.hypot(last[0]-first[0], last[2]-first[2])
    report['entryTravel'] = math.hypot(first[0]-report['before']['player']['position'][0],
                                     first[2]-report['before']['player']['position'][2])
    assert report['entryTravel'] > 0.25, 'No walking occurred: cancellation-under-motion unproven'
    assert report['horizontalDrift'] < 0.2, 'Entry still walking after timeout'
    assert report['later']['guard']['enabled'] and not report['later']['guard']['busy']
    report['passed'] = True
finally:
    (folder/'entry_budget.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f'Evidence: {folder}; passed={report.get("passed", False)}', flush=True)
