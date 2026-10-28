"""Focused native-seat/camera evidence. Images still require human-visible inspection."""
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
from concurrent.futures import ThreadPoolExecutor
from watch_game_health import FATAL, heartbeat_stall_ms
from run_camper_qualification import require_no_live_zombies, parked_ready, daylight_command

ROOT = Path(__file__).resolve().parent
MOD = ROOT.parents[1]

def require_production_bootstrap(data):
    evidence = data.get('productionBootstrap')
    if not isinstance(evidence, dict) or evidence.get('state') != 'Complete' or \
            evidence.get('lastFailure') != '' or evidence.get('moduleInstalled') is not True:
        raise RuntimeError('Actual production bootstrap must be Complete with no failure and camper modules installed before seating.')
    return dict(evidence)

def require_safe_seating(data, player_position):
    speed = data.get('speed')
    wheels = data.get('wheels')
    position = data.get('position')
    if isinstance(speed, bool) or not isinstance(speed, (int, float)) or not math.isfinite(speed) or speed > .1 or speed < 0:
        raise RuntimeError('Stopped vehicle evidence required before seating.')
    if not isinstance(wheels, list) or len(wheels) != 4 or sum(w.get('grounded') is True for w in wheels) < 3:
        raise RuntimeError('Grounded vehicle evidence required before seating; airborne captures are invalid.')
    if not isinstance(position, dict) or not isinstance(player_position, list) or len(player_position) != 3:
        raise RuntimeError('Actual nearby player and vehicle positions required before seating.')
    values = [position.get(axis) for axis in ('x', 'y', 'z')] + player_position
    if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in values):
        raise RuntimeError('Finite nearby positions required before seating.')
    if sum((position[axis] - player_position[i]) ** 2 for i, axis in enumerate(('x', 'y', 'z'))) > 144:
        raise RuntimeError('Approach the grounded camper first; remote seating is refused.')


def verify_native_steering(reference, state, held=False):
    """Native VPSteering rotates baseRotation around local Y; road steering alone is insufficient."""
    def quaternion(row):
        values = [row['localQuaternion'][axis] for axis in ('x', 'y', 'z', 'w')]
        if any(not math.isfinite(v) for v in values) or abs(sum(v*v for v in values) - 1) > .001:
            raise RuntimeError('Finite unit steering quaternion evidence required.')
        return values
    a, b = quaternion(reference['steering']), quaternion(state['steering'])
    # inverse(a) * b. Native shaft-local Y leaves relative X/Z zero.
    x = a[3]*b[0] - a[0]*b[3] - a[1]*b[2] + a[2]*b[1]
    y = a[3]*b[1] + a[0]*b[2] - a[1]*b[3] - a[2]*b[0]
    z = a[3]*b[2] - a[0]*b[1] + a[1]*b[0] - a[2]*b[3]
    if abs(x) > .001 or abs(z) > .001:
        raise RuntimeError('Steering rim rotated away from its native local-Y shaft.')
    if held and abs(y) < .00001:
        raise RuntimeError('Road wheels steered but the steering rim did not rotate.')
    for field in ('pivotVehicleLocal', 'meshCenterVehicleLocal'):
        before, after = reference['steering'][field], state['steering'][field]
        if before is None or after is None or math.sqrt(sum((before[k]-after[k])**2 for k in ('x', 'y', 'z'))) > .001:
            raise RuntimeError('Steering rim centre/pivot moved during native steering.')
    driver = next(s for s in state['seats'] if s['slot'] == 0 and s['isLocalPlayer'])
    bindings = [t for frame in driver['ikFrames'] for t in frame['targets']]
    for goal, hand in (('LeftHand', 'L'), ('RightHand', 'R')):
        if not any(t['goal'] == goal and t['hasTransform'] and
                   t.get('targetPath') == 'M/SteeringWheel/IKHand' + hand for t in bindings):
            raise RuntimeError('Native hand rig is not bound to the actual steering target.')

def configured_slots(data):
    count = data.get('attachMaxCount')
    rows = data.get('seats')
    if type(count) is not int or count < 2 or not isinstance(rows, list) or len(rows) != count:
        raise ValueError('Complete native seat manifest required.')
    if any(type(row.get('slot')) is not int for row in rows) or sorted(row['slot'] for row in rows) != list(range(count)):
        raise ValueError('Native seat slots must be unique and contiguous.')
    for row in rows:
        if row.get('configuredPose') != (40 if row['slot'] == 0 else 41):
            raise ValueError('Native driver/passenger pose configuration is incorrect.')
    return list(range(count))

def require_bed_pose(row, camper_id):
    pose = row.get('bedPose')
    if row.get('slot') != 4 or not isinstance(pose, dict) or pose.get('status') != 'APPLIED_UNQUALIFIED' \
            or pose.get('occupantId') != row.get('occupantId') or pose.get('nativeSlot') != 4 \
            or pose.get('camperId') != camper_id \
            or pose.get('clip') != 'rebirthCamperBedLying' \
            or any(pose.get(field) is not True for field in
                   ('nativeMembership', 'clipGenericMotion', 'nativeRigCompatible', 'controllerPreserved', 'nativeBedIKTargetsAbsent')) \
            or pose.get('clipHumanMotion') is not False \
            or type(pose.get('bodyAnimatorCount')) is not int or pose['bodyAnimatorCount'] < 1 \
            or type(pose.get('activeGraphs')) is not int or pose['activeGraphs'] != pose['bodyAnimatorCount']:
        raise RuntimeError('Actual bed occupant must have the native-compatible generic lying clip applied to all bodies.')
    return pose

def run(pid, entity, layouts=None, grips=None):
    folder = ROOT / 'reports/seating' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    session = json.loads((MOD / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    if session['pid'] != pid:
        raise RuntimeError('Explicit owned PID mismatch; no input sent.')
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    report = {'status': 'RUNNING', 'pid': pid, 'entity': entity, 'steps': [], 'visualAcceptance': False}
    layout_attempted = False
    attached = False
    original_grips = None
    def request(path, method='GET', **params):
        if method == 'POST' and request('/ping').get('pid') != pid:
            raise RuntimeError('Owned PID changed; no input or cleanup sent.')
        if method == 'POST' and request('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
            raise RuntimeError('Save changed; no input or cleanup sent.')
        url = f'http://127.0.0.1:{session["port"]}{path}'
        if params:
            url += '?' + urllib.parse.urlencode(params)
        req = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': session['token']})
        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.load(response)
        if not data.get('ok') or data.get('hadError'):
            raise RuntimeError(data)
        return data
    def health():
        ping = request('/ping')
        if ping.get('pid') != pid or ping.get('state') != 'ingame' or heartbeat_stall_ms(ping) > 10000:
            raise RuntimeError('Owned in-game main thread is not ready.')
        if any(FATAL.search(line) for line in log.read_text(encoding='utf-8', errors='replace').splitlines()):
            raise RuntimeError('Game log contains errors; stop and diagnose.')
    def act(path, **params):
        health()
        value = request(path, 'POST', **params)
        output = [line for entry in value.get('results', []) for line in entry.get('output', [])]
        if any('*** ERROR' in line or '***ERROR' in line for line in output):
            raise RuntimeError('Native fixture refused: ' + str(output))
        report['steps'].append({'path': path, 'parameters': params, 'response': value})
        for entry in value.get('results', []):
            for line in entry.get('output', []):
                if '] PASS_CAPTURE ' in line:
                    source = Path(line.split('] PASS_CAPTURE ', 1)[1])
                    shutil.copyfile(source, folder / source.name)
        return value
    def capture(label):
        value = act('/screenshot', name=label)
        path = Path(value['path'])
        shutil.copyfile(path, folder / path.name)
        print(str(path), flush=True)
    def set_grips(values):
        for hand in ('L', 'R'):
            numbers = values[hand]['position'] + values[hand]['rotation']
            if len(numbers) != 6 or any(isinstance(n, bool) or not isinstance(n, (int, float)) or not math.isfinite(n) for n in numbers):
                raise RuntimeError('Six measured native target values required per hand.')
            act('/console', cmd=f'camperik {entity} {hand} ' + ' '.join(str(n) for n in numbers))
        reply = act('/console', cmd=f'camperdiag {entity}')
        paths = [Path(line.split('] PASS_CAPTURE ', 1)[1]) for entry in reply.get('results', [])
                 for line in entry.get('output', []) if '] PASS_CAPTURE ' in line]
        if len(paths) != 1:
            raise RuntimeError('Cannot verify actual native grip application/restoration.')
        data = json.loads(paths[0].read_text(encoding='utf-8-sig'))
        targets = {t['name']: t for t in data['handTargets'] if t['path'].startswith('M/SteeringWheel/')}
        for hand in ('L', 'R'):
            target = targets['IKHand' + hand]
            for index, axis in enumerate(('x', 'y', 'z')):
                if abs(target['localPosition'][axis] - values[hand]['position'][index]) > .0001:
                    raise RuntimeError('Native target position did not match applied/restored grip.')
                angle = (target['localRotation'][axis] - values[hand]['rotation'][index] + 180) % 360 - 180
                if abs(angle) > .01:
                    raise RuntimeError('Native wrist rotation did not match applied/restored grip.')
    def grip_state(reference, held=False):
        reply = act('/console', cmd=f'camperdiag {entity}')
        paths = [Path(line.split('] PASS_CAPTURE ', 1)[1]) for entry in reply.get('results', [])
                 for line in entry.get('output', []) if '] PASS_CAPTURE ' in line]
        if len(paths) != 1:
            raise RuntimeError('Unique grip-state capture required.')
        state = json.loads(paths[0].read_text(encoding='utf-8-sig'))
        verify_native_steering(reference, state, held)
        def distance(a, b):
            return math.sqrt(sum((a[axis] - b[axis])**2 for axis in ('x', 'y', 'z')))
        for field in ('localPosition', 'shaftVehicleLocal'):
            if distance(state['steering'][field], reference['steering'][field]) > .001:
                raise RuntimeError('Steering centre/shaft moved instead of rotating in place.')
        driver = next(s for s in state['seats'] if s['slot'] == 0 and s['isLocalPlayer'])
        bones = {b['name']: b for b in driver['bones']}
        targets = {t['name']: t for t in state['handTargets'] if t['path'].startswith('M/SteeringWheel/')}
        for bone, hand in (('LeftHand', 'L'), ('RightHand', 'R')):
            if distance(bones[bone]['vehicleLocal'], targets['IKHand' + hand]['vehicleLocal']) > .025:
                raise RuntimeError('Native wrist did not follow its steering target.')
        if held and max(abs(w['steerAngle']) for w in state['wheels']) < 5:
            raise RuntimeError('Held steering did not produce an actual wheel steering angle.')
        return state
    try:
        if grips and layouts:
            raise RuntimeError('Run driver grip and rear-layout scenarios separately.')
        candidate_grips = json.loads(Path(grips).read_text(encoding='utf-8-sig'))['hands'] if grips else None
        if grips:
            shutil.copyfile(grips, folder / 'grips.json')
        health()
        world = request('/state', sections='world').get('world', {})
        if world.get('gameName') != 'CodexTest':
            raise RuntimeError('Disposable CodexTest required.')
        act('/guard', enabled=0, needs=0, supply=0, agenda=0, explore=0, glance=0, heal=0, reload=0, bleed=0)
        act('/enemyspawns', on=0)
        require_no_live_zombies(request('/entities', contains='zombie', radius=80, limit=1000))
        daylight = daylight_command(world)
        report['clockBefore'] = world
        if daylight:
            act('/console', cmd=daylight)
        manifest_reply = act('/console', cmd=f'camperdiag {entity}')
        manifest_paths = [Path(line.split('] PASS_CAPTURE ', 1)[1]) for entry in manifest_reply.get('results', [])
                          for line in entry.get('output', []) if '] PASS_CAPTURE ' in line]
        if len(manifest_paths) != 1:
            raise RuntimeError('Unique complete native seat manifest required before seating.')
        manifest = json.loads(manifest_paths[0].read_text(encoding='utf-8-sig'))
        report['productionBootstrap'] = dict(manifest.get('productionBootstrap') or {})
        require_production_bootstrap(manifest)
        if not parked_ready(manifest, entity):
            raise RuntimeError('Camper is moving or spinning; no seat input permitted before parked confirmation.')
        slots = configured_slots(manifest)
        report['configuredSlots'] = slots
        report['simultaneousOccupantFitAccepted'] = False
        if grips:
            slots = [0]  # Grip calibration only needs the driver.
        candidates = [None] * len(slots)
        if layouts:
            candidates = json.loads(Path(layouts).read_text(encoding='utf-8-sig'))['candidates']
            if not candidates or len(candidates) > 12:
                raise RuntimeError('One to twelve explicitly configured cabin candidates required.')
            shutil.copyfile(layouts, folder / 'candidates.json')
        for index, candidate in enumerate(candidates):
            slot = 1 if candidate else slots[index]
            label = f'camper_seat{slot}' if candidate is None else f'camper_cabin_candidate{index}'
            if candidate:
                parameters = [candidate['pose'], *candidate['attachment'], candidate['yaw']]
                if 'feet' in candidate:
                    parameters += candidate['feet']['leftPosition'] + candidate['feet']['rightPosition']
                    parameters += candidate['feet']['leftRotation'] + candidate['feet']['rightRotation']
                if len(parameters) not in (5, 17) or any(isinstance(n, bool) or not isinstance(n, (float, int)) or not math.isfinite(n) for n in parameters):
                    raise RuntimeError('Invalid candidate coordinates.')
                act('/dismount')
                attached = False
                layout_attempted = True
                reply = act('/console', cmd=f'camperseatlayout {entity} ' + ' '.join(str(n) for n in parameters))
                if not any('[CamperSeatLayout] APPLIED ' in line for entry in reply.get('results', []) for line in entry.get('output', [])):
                    raise RuntimeError('Missing confirmed temporary layout receipt; do not retry uncertain changes.')
            safe_reply = act('/console', cmd=f'camperdiag {entity}')
            safe_paths = [Path(line.split('] PASS_CAPTURE ', 1)[1]) for entry in safe_reply.get('results', [])
                          for line in entry.get('output', []) if '] PASS_CAPTURE ' in line]
            if len(safe_paths) != 1:
                raise RuntimeError('Unique current grounded vehicle evidence required before seating.')
            safe_state = json.loads(safe_paths[0].read_text(encoding='utf-8-sig'))
            require_safe_seating(safe_state, request('/state', sections='player').get('player', {}).get('position'))
            attached = True  # Also clean up a possible attachment after an uncertain response.
            act('/console', cmd=f'camperseat {entity} {slot}')
            time.sleep(.6)
            diagnostic = act('/console', cmd=f'camperdiag {entity}')
            captures = [Path(line.split('] PASS_CAPTURE ', 1)[1])
                        for entry in diagnostic.get('results', []) for line in entry.get('output', [])
                        if '] PASS_CAPTURE ' in line]
            if len(captures) != 1:
                raise RuntimeError('Exactly one native diagnostic capture required.')
            data = json.loads(captures[0].read_text(encoding='utf-8-sig'))
            seated = [s for s in data['seats'] if s['slot'] == slot and s['isLocalPlayer'] and s['attachedToCamper']]
            expected_pose = candidate['pose'] if candidate else (40 if slot == 0 else 41)
            if len(seated) != 1 or seated[0]['runtimePose'] != expected_pose:
                raise RuntimeError('Actual native occupant/pose did not match requested seat.')
            if candidate is None and slot == 4:
                report['steps'].append({'appliedBedPose': require_bed_pose(seated[0], entity)})
            if slot == 0 and data.get('turnTowardsLook'):
                act('/press', action='vehicle.ToggleTurnMode', seconds=.1)
                time.sleep(.4)
                diagnostic = act('/console', cmd=f'camperdiag {entity}')
                mode_capture = [Path(line.split('] PASS_CAPTURE ', 1)[1])
                                for entry in diagnostic.get('results', []) for line in entry.get('output', [])
                                if '] PASS_CAPTURE ' in line]
                if len(mode_capture) != 1 or json.loads(mode_capture[0].read_text()).get('turnTowardsLook'):
                    raise RuntimeError('Native keyboard steering mode was not confirmed.')
                data = json.loads(mode_capture[0].read_text(encoding='utf-8-sig'))
            if slot == 0 and candidate_grips:
                targets = {t['name']: t for t in data['handTargets'] if t['path'].startswith('M/SteeringWheel/')}
                original_grips = {hand: {
                    'position': [targets['IKHand' + hand]['localPosition'][axis] for axis in ('x', 'y', 'z')],
                    'rotation': [targets['IKHand' + hand]['localRotation'][axis] for axis in ('x', 'y', 'z')]
                } for hand in ('L', 'R')}
                set_grips(candidate_grips)
                time.sleep(.2)
            if slot == 0:
                grip_state(data)
            capture(label + '_native')
            act('/press', action='vehicle.LookRight', seconds=.8)
            act('/press', action='vehicle.LookDown', seconds=.3)
            act('/press', action='vehicle.ScrollUp', seconds=.15)
            capture(label + '_side')
            act('/press', action='vehicle.LookLeft', seconds=1.6)
            capture(label + '_other_side')
            if slot == 0:
                # Baseline fit needs the same actual native hand/shaft proof as
                # optional calibration. A candidate is not required to observe it.
                for direction in ('Right', 'Left'):
                    # Native timed holds expire even if this collector stops;
                    # overlap the read-only evidence while the real input runs.
                    with ThreadPoolExecutor(max_workers=1) as held_input:
                        held = held_input.submit(act, '/press', action='vehicle.Move' + direction, seconds=4)
                        time.sleep(.5)
                        grip_state(data, held=True)
                        capture(label + '_grip_held_' + direction.lower())
                        held.result(timeout=6)
                    time.sleep(.5)
                    grip_state(data)
                    capture(label + '_grip_returned_neutral_after_' + direction.lower())
                if original_grips:
                    set_grips(original_grips)
                    original_grips = None
            act('/dismount')
            attached = False
        report['status'] = 'CAPTURED_NOT_VISUALLY_ACCEPTED'
    except Exception as error:
        report.update(status='FAIL', error=str(error))
    finally:
        try:
            if request('/ping').get('pid') != pid:
                if attached or layout_attempted or original_grips:
                    raise RuntimeError('Owned session ended/changed before cleanup confirmation; other session left untouched.')
            else:
                request('/release', 'POST')
                health()
                if request('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
                    raise RuntimeError('Save changed; no seating cleanup sent.')
                if original_grips:
                    set_grips(original_grips)
                    original_grips = None
                if attached:
                    act('/dismount')
                if layout_attempted:
                    act('/console', cmd=f'camperseatlayout {entity} clear')
        except Exception as error:
            report['releaseError'] = str(error)
            report['status'] = 'FAIL_CLEANUP_REQUIRES_INSPECTION'
        if log.exists():
            shutil.copyfile(log, folder / 'Player.log')
        (folder / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(str(folder / 'result.json'), flush=True)
    return int(report['status'].startswith('FAIL'))

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--owned-pid', type=int, required=True)
    parser.add_argument('--entity', type=int, required=True)
    parser.add_argument('--layouts', help='Explicit measured cabin candidate JSON; skips front-seat baseline and tests slot 1 temporarily.')
    parser.add_argument('--grips', help='Measured native steering-target JSON; includes stationary held-left/right captures and restores targets.')
    args = parser.parse_args()
    raise SystemExit(run(args.owned_pid, args.entity, args.layouts, args.grips))
