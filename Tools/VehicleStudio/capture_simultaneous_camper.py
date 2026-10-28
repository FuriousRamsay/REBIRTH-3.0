"""Capture six real camper occupants together in an authorized disposable session.

Does not create players, attach them, or accept appearance from coordinates.
Run once real participants occupy all six positions. Inspect every screenshot.
"""
import argparse
import json
import math
from pathlib import Path
import shutil
import threading

from run_camper_qualification import Suite, require_no_live_zombies
from validate_camper_seats import configured_slots, require_production_bootstrap, require_bed_pose


def require_six_players(data, expected_ids=None):
    require_production_bootstrap(data)
    if configured_slots(data) != list(range(6)):
        raise RuntimeError('The requested six-place camper configuration is required.')
    if type(data.get('speed')) not in (int, float) or not math.isfinite(data['speed']) or not 0 <= data['speed'] <= .1:
        raise RuntimeError('Stop the camper before simultaneous occupant capture.')
    seats = sorted(data['seats'], key=lambda row: row['slot'])
    ids = []
    local = []
    for row in seats:
        ident = row.get('occupantId')
        if type(ident) is not int or ident < 0 or row.get('isPlayer') is not True \
                or row.get('occupantAlive') is not True or row.get('attachedToCamper') is not True:
            raise RuntimeError('Every slot must contain a living real player attached to this camper.')
        if row.get('runtimePose') != row['configuredPose']:
            raise RuntimeError('Actual occupant pose differs from native seat configuration.')
        if row['slot'] == 4:
            require_bed_pose(row, data.get('entityId'))
        bones = row.get('bones')
        if not isinstance(bones, list):
            raise RuntimeError('Actual occupant bone evidence is missing.')
        for name in ('Hips', 'Head', 'LeftHand', 'RightHand', 'LeftFoot', 'RightFoot'):
            matches = [bone for bone in bones if bone.get('name') == name]
            if len(matches) != 1:
                raise RuntimeError('Unique actual humanoid bone required: ' + name)
            point = matches[0].get('vehicleLocal')
            if not isinstance(point, dict) or any(type(point.get(axis)) not in (int, float)
                    or not math.isfinite(point[axis]) for axis in ('x', 'y', 'z')):
                raise RuntimeError('Finite model-relative humanoid bone evidence required.')
        ids.append(ident)
        if row.get('isLocalPlayer') is True:
            local.append(row['slot'])
    if len(set(ids)) != 6 or len(local) != 1:
        raise RuntimeError('Six distinct players including exactly one local camera occupant required.')
    if expected_ids is not None and ids != expected_ids:
        raise RuntimeError('Occupants changed during simultaneous camera capture.')
    return ids


def run(pid, receipt, entity):
    suite = Suite(pid, receipt)
    suite.report.update(kind='SIX_REAL_PLAYER_OCCUPANT_CAPTURE', camper=entity,
                        visualAcceptance=False, simultaneousOccupantFitAccepted=False)
    monitor = threading.Thread(target=suite.monitor, daemon=True)
    monitor.start()
    try:
        suite.identity()
        require_no_live_zombies(suite.raw('/entities', radius=150, contains='zombie', limit=1000))
        guard = suite.raw('/guard')
        needs = guard.get('needs', {})
        if any(guard.get(k) is not False for k in ('enabled', 'heal', 'reload', 'bleed', 'busy')) \
                or any(needs.get(k) is not False for k in ('enabled', 'supply')):
            raise RuntimeError('Use the focused camper setup first; independent chores must be stopped.')
        ids = None
        for label, actions in (
                ('neutral', ()),
                ('right_inside', (('vehicle.LookRight', .8), ('vehicle.LookDown', .3))),
                ('left_inside', (('vehicle.LookLeft', 1.6),))):
            before = suite.diagnostic(f'camperdiag {entity}', '] PASS_CAPTURE ')
            if before.get('entityId') != entity or before.get('entityClass') != 'rebirthCamper80s':
                raise RuntimeError('Exact camper diagnostic identity required.')
            ids = require_six_players(before, ids)
            for action, seconds in actions:
                suite.act('/press', action=action, seconds=seconds)
            image = suite.act('/screenshot', name='six_occupants_' + label)
            source = Path(image['path'])
            if not source.is_file():
                raise RuntimeError('Native screenshot was not written.')
            shutil.copyfile(source, suite.folder / source.name)
            after = suite.diagnostic(f'camperdiag {entity}', '] PASS_CAPTURE ')
            if after.get('entityId') != entity or after.get('entityClass') != 'rebirthCamper80s':
                raise RuntimeError('Camper identity changed during capture.')
            require_six_players(after, ids)
            suite.report['phases'].append({'view': label, 'occupantIdsBySlot': ids,
                                           'screenshot': str(suite.folder / source.name)})
        if suite.failure:
            raise RuntimeError(suite.failure)
        suite.report['status'] = 'CAPTURED_PENDING_VISUAL_INSPECTION'
    except Exception as error:
        suite.report.update(status='FAIL', error=str(error))
    finally:
        # Timed camera inputs expire by themselves. Do not detach participants or
        # clear another player's input as part of this read-only seating capture.
        suite.stop.set()
        monitor.join(timeout=12)
        if suite.log.exists():
            shutil.copyfile(suite.log, suite.folder / 'Player.log')
        target = suite.folder / 'result.json'
        target.write_text(json.dumps(suite.report, indent=2), encoding='utf-8')
        print(str(target), flush=True)
    return int(suite.report['status'] == 'FAIL')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-pid', type=int, required=True)
    parser.add_argument('--authorized-session-receipt', required=True)
    parser.add_argument('--entity', type=int, required=True)
    args = parser.parse_args()
    raise SystemExit(run(args.owned_pid, args.authorized_session_receipt, args.entity))
