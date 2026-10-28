"""Run one in-game native input sequence in an already authorized owned session."""
import argparse
import json
import os
from pathlib import Path
import shutil
import time
import urllib.parse
import urllib.request
import uuid
from watch_game_health import FATAL, WARNING, heartbeat_stall_ms
from run_camper_qualification import require_no_live_zombies

ROOT = Path(__file__).resolve().parent
MOD = ROOT.parents[1]


def run(pid, entity, timeout=90, bounds=None):
    session = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    if session.get('pid') != pid:
        raise RuntimeError('Explicit owned PID does not match; no input sent.')
    folder = ROOT / 'reports/native_sequence_runs' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    report = {'pid': pid, 'entity': entity, 'status': 'RUNNING', 'observations': [],
              'visualAcceptance': False, 'audibleAcceptance': False}
    source = None
    lease_sent = 0

    def request(path, method='GET', **params):
        url = f'http://127.0.0.1:{session["port"]}{path}'
        if params:
            url += '?' + urllib.parse.urlencode(params)
        req = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': session['token']})
        with urllib.request.urlopen(req, timeout=5) as response:
            value = json.load(response)
        if not value.get('ok') or value.get('hadError'):
            raise RuntimeError('Bridge request failed: ' + path)
        return value

    def health():
        ping = request('/ping')
        if ping.get('pid') != pid or ping.get('state') != 'ingame':
            raise RuntimeError('Owned game changed or is no longer in-game.')
        stall = heartbeat_stall_ms(ping)
        if stall > 10000:
            raise RuntimeError('Native main-thread heartbeat is missing or stalled.')
        if request('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
            raise RuntimeError('Disposable save changed; no further input permitted.')
        lines = log.read_text(encoding='utf-8', errors='replace').splitlines()
        errors = [line for line in lines if FATAL.search(line)]
        report['observations'].append({'time': time.time(), 'mainThreadStallMs': stall,
            'logLines': len(lines), 'warnings': [line for line in lines if WARNING.search(line)]})
        if errors:
            raise RuntimeError('Actual game log contains runtime errors: ' + '; '.join(errors[:3]))

    try:
        health()
        require_no_live_zombies(request('/entities', radius=80, contains='zombie', limit=1000))
        # The in-game command separately validates exact mounted driver, all
        # independent automation switches and threat absence before any input.
        command = f'rbvehiclesequence {entity}'
        if bounds is not None:
            import math
            if len(bounds) != 4 or any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in bounds):
                raise ValueError('Four finite safe-interior bounds are required.')
            command += ' ' + ' '.join(format(v, '.9g') for v in bounds)
        # Preparation requests can outlive a session change. Recheck the live
        # process/save and the session credential immediately before input.
        health()
        current_session = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
        if any(current_session.get(key) != session.get(key) for key in ('pid', 'port', 'token')):
            raise RuntimeError('Bridge session changed during preparation; no input sent.')
        response = request('/console', 'POST', cmd=command)
        lines = [line for row in response.get('results', []) for line in row.get('output', [])]
        receipts = [line.split('[VehicleSequence] STARTED ', 1)[1] for line in lines
                    if '[VehicleSequence] STARTED ' in line]
        if len(receipts) != 1 or any('*** ERROR' in line for line in lines):
            raise RuntimeError('Missing unique start receipt; do not replay an uncertain input sequence.')
        source = Path(receipts[0]).resolve()
        allowed = (MOD / 'Tools/GameBridge/out/vehicle_sequences').resolve()
        if not source.is_relative_to(allowed) or source.name != 'result.json':
            raise RuntimeError('Unexpected sequence result path.')
        report['resultPath'] = str(source)
        deadline = time.monotonic() + timeout
        while not source.exists():
            health()
            if time.monotonic() - lease_sent >= 2:
                control = request('/console', 'POST', cmd='rbvehiclesequence heartbeat ' + source.parent.name)
                output = [line for row in control.get('results', []) for line in row.get('output', [])]
                if any('*** ERROR' in line for line in output):
                    # Completion can race the heartbeat; require the same result.
                    if not source.exists():
                        raise RuntimeError('Native sequence supervisor lease was refused.')
                lease_sent = time.monotonic()
            if time.monotonic() >= deadline:
                raise RuntimeError('Sequence observation timed out; inspect the same session, never replay automatically.')
            time.sleep(.5)
        health()
        result = json.loads(source.read_text(encoding='utf-8-sig'))
        shutil.copyfile(source, folder / 'native_result.json')
        if result.get('entity') != entity or result.get('schema') != 3 or result.get('failure') or result.get('status') != 'CAPTURED_NATIVE_INPUT_SEQUENCE_NOT_ACCEPTANCE':
            raise RuntimeError('Native sequence failed or did not match the requested vehicle.')
        if bounds is not None:
            captured_bounds = result.get('safeBounds') or {}
            if any(not isinstance(captured_bounds.get(key), (int, float)) or abs(captured_bounds[key]-expected) > .001
                   for key, expected in zip(('xMin', 'xMax', 'zMin', 'zMax'), bounds)):
                raise RuntimeError('Native result does not acknowledge the requested live pad boundary.')
        if len(result.get('screenshots', [])) != 9:
            raise RuntimeError('All nine phase screenshots are required.')
        for index, screenshot in enumerate(result.get('screenshots', [])):
            image = Path(screenshot).resolve()
            if not image.is_relative_to(source.parent) or not image.is_file():
                raise RuntimeError('Missing or unexpected native screenshot path.')
            with image.open('rb') as stream:
                if stream.read(8) != b'\x89PNG\r\n\x1a\n':
                    raise RuntimeError('Invalid native screenshot signature.')
            shutil.copyfile(image, folder / f'phase_{index}.png')
        report['status'] = 'CAPTURED_REQUIRES_VISUAL_AUDIO_AND_HANDLING_REVIEW'
    except Exception as error:
        report.update(status='FAILED', error=str(error))
        raise
    finally:
        if source is not None and not source.exists():
            try:
                current = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
                if current == session and request('/ping').get('pid') == pid:
                    request('/console', 'POST', cmd='rbvehiclesequence cancel ' + source.parent.name)
                    report['abort'] = 'Exact run cancellation requested; owned lease also expires within 10 seconds.'
            except Exception as error:
                report['abort'] = 'Cancellation unavailable; native owned lease expires: ' + str(error)
        if log.exists():
            shutil.copyfile(log, folder / 'Player.log')
        (folder / 'runner.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(str(folder / 'runner.json'), flush=True)
    return folder


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', type=int, required=True)
    parser.add_argument('--entity', type=int, required=True)
    args = parser.parse_args()
    run(args.pid, args.entity)
