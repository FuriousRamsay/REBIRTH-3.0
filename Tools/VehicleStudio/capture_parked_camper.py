"""Read-only parked physics supervision; never places, mounts or moves a vehicle."""
import argparse
import json
import math
import os
from pathlib import Path
import shutil
import time
import urllib.parse
import urllib.request
import uuid

from run_camper_qualification import MOD, STUDIO, SESSION, same_session, receipt_path
from watch_game_health import FATAL, heartbeat_stall_ms
from launch_camper_qualification import processes, require_owned


def check_parked(state, entity, baseline=None):
    if state.get('entityId') != entity or state.get('entityClass') != 'rebirthCamper80s':
        raise RuntimeError('Diagnostic entity identity changed.')
    seats = state.get('seats')
    if not isinstance(seats, list) or not seats or any(s.get('occupantId') != -1 or s.get('attachedToCamper') is not False for s in seats):
        raise RuntimeError('Parked observation requires explicitly empty seats.')
    speed = state.get('speed')
    rigidbody = state.get('rigidbodyState')
    if not isinstance(rigidbody, dict):
        raise RuntimeError('Expanded native rigidbody evidence required.')
    angular = rigidbody.get('angularVelocity')
    position = state.get('position')
    def vector(value):
        if not isinstance(value, dict) or any(type(value.get(k)) not in (int, float) or not math.isfinite(value[k]) for k in ('x', 'y', 'z')):
            raise RuntimeError('Finite native physics vector required.')
        return value
    angular, position = vector(angular), vector(position)
    if type(speed) not in (int, float) or not math.isfinite(speed):
        raise RuntimeError('Finite native speed required.')
    if speed > 1 or math.sqrt(sum(angular[k]**2 for k in ('x', 'y', 'z'))) > 1:
        raise RuntimeError('Unsafe unattended parked speed or angular velocity.')
    if baseline is not None and math.sqrt(sum((position[k]-baseline[k])**2 for k in ('x', 'y', 'z'))) > 1:
        raise RuntimeError('Unsafe unattended parked displacement exceeded1m.')
    return dict(position)


def run(pid, authorized_receipt, entity=None, watch=False, interval=.15, watch_timeout=90):
    expected = json.loads(Path(authorized_receipt).read_text(encoding='utf-8-sig'))
    if expected.get('pid') != pid or not same_session(expected, expected):
        raise RuntimeError('Explicit valid owned PID/session receipt required.')
    folder = STUDIO / 'reports/parked_physics' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    report = {'status': 'FAIL', 'pid': pid, 'entity': entity, 'readOnly': True, 'samples': [], 'gameplayAcceptance': False}
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    exports = Path(os.environ['APPDATA']) / '7DaysToDie/Mods/REBIRTH_VehicleInspector_Dev/Exports/Camper'
    def request(path, method='GET', **parameters):
        if not same_session(json.loads(SESSION.read_text(encoding='utf-8-sig')), expected):
            raise RuntimeError('Owned session changed; no further diagnostic requests.')
        url = f'http://127.0.0.1:{expected["port"]}{path}'
        if parameters:
            url += '?' + urllib.parse.urlencode(parameters)
        req = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': expected['token']})
        with urllib.request.urlopen(req, timeout=5) as response:
            reply = json.load(response)
        if not reply.get('ok') or reply.get('hadError'):
            raise RuntimeError('Read-only diagnostic request failed: ' + str(reply))
        return reply
    next_process_check = 0
    def health():
        nonlocal next_process_check
        if time.monotonic() >= next_process_check:
            require_owned(processes(), pid, MOD.parents[1] / '7DaysToDie.exe')
            next_process_check = time.monotonic() + 2
        ping = request('/ping')
        if ping.get('pid') != pid or ping.get('state') != 'ingame' or heartbeat_stall_ms(ping) > 10000:
            raise RuntimeError('Owned in-game main thread is not healthy.')
        if request('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
            raise RuntimeError('Disposable save changed.')
        if not log.is_file():
            raise RuntimeError('Actual Player.log unavailable.')
        if any(FATAL.search(line) for line in log.read_text(encoding='utf-8', errors='replace').splitlines()):
            raise RuntimeError('Full Player.log contains a runtime error.')
        return ping
    def campers():
        reply = request('/entities', radius=150, contains='rebirthCamper80s', limit=1000)
        rows = reply.get('entities')
        if not isinstance(rows, list) or reply.get('count') != len(rows):
            raise RuntimeError('Complete nearby camper identity set required.')
        return {row['id'] for row in rows if row.get('class') == 'rebirthCamper80s' and row.get('dead') is False}
    try:
        health()
        if watch:
            initial = campers()
            report['initialIds'] = sorted(initial)
            print('READY watching for one normally placed camper; no placement input sent.', flush=True)
            deadline = time.monotonic() + watch_timeout
            while time.monotonic() < deadline:
                health()
                added = campers() - initial
                if len(added) > 1:
                    raise RuntimeError('Ambiguous new camper identities.')
                if added:
                    entity = next(iter(added)); break
                time.sleep(interval)
            else:
                raise RuntimeError('Bounded new-camper watch expired.')
        report['entity'] = entity
        started = time.monotonic()
        deadline = started + 15
        baseline = None
        while time.monotonic() < deadline:
            sample_start = time.monotonic()
            ping = health()
            reply = request('/console', 'POST', cmd=f'camperdiag {entity}')
            path = receipt_path(reply, '[CamperDiagnostics] PASS_CAPTURE ', exports)
            shutil.copyfile(path, folder / path.name)
            state = json.loads(path.read_text(encoding='utf-8-sig'))
            report['samples'].append({'seconds': sample_start-started, 'receipt': path.name, 'heartbeat': ping})
            # Store the failing/runaway snapshot before enforcing the safety gate.
            observed = check_parked(state, entity, baseline)
            if baseline is None:
                baseline = observed
            time.sleep(max(0, interval-(time.monotonic()-sample_start)))
        if len(report['samples']) < 2:
            raise RuntimeError('Insufficient bounded parked observations.')
        report['status'] = 'CAPTURED_PENDING_PHYSICS_REVIEW'
        return 0
    except Exception as error:
        report['error'] = str(error)
        return 1
    finally:
        if log.exists():
            shutil.copyfile(log, folder / 'Player.log')
        (folder / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(str(folder / 'result.json'), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-pid', required=True, type=int)
    parser.add_argument('--authorized-session-receipt', required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--entity', type=int)
    mode.add_argument('--watch-new', action='store_true')
    parser.add_argument('--interval', type=float, default=.15)
    parser.add_argument('--watch-timeout', type=int, default=90)
    args = parser.parse_args()
    if not .1 <= args.interval <= .25 or not 5 <= args.watch_timeout <= 120 or args.entity is not None and args.entity <= 0:
        parser.error('Require interval0.1..0.25s, watch bound5..120s and positive entity ID.')
    raise SystemExit(run(args.owned_pid, args.authorized_session_receipt, args.entity, args.watch_new, args.interval, args.watch_timeout))
