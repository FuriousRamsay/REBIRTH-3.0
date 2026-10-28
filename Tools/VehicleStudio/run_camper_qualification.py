"""One bounded camper evidence flow in an explicitly authorized, already running owned session.

Never launches, rebuilds or force-terminates the game. Never substitutes raw entity
spawn for normal placeable use. Visual/audio/world-persistence acceptance stays pending.
"""
import argparse
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import threading
import time
import urllib.parse
import urllib.request
import uuid
from watch_game_health import FATAL, WARNING, heartbeat_stall_ms, running_game_pids

STUDIO = Path(__file__).resolve().parent
MOD = STUDIO.parents[1]
SESSION = MOD / 'Tools/GameBridge/out/session.json'


def daylight_command(world):
    """Preserve earned save history and advance only when daylight is needed."""
    if (world.get('gameName') != 'CodexTest' or type(world.get('day')) is not int
            or world['day'] < 1 or type(world.get('hour')) is not int
            or not 0 <= world['hour'] <= 23 or type(world.get('isDaytime')) is not bool):
        raise RuntimeError('Current disposable-save clock evidence required.')
    if world['isDaytime']:
        return None
    day = world['day'] + (1 if world['hour'] >= 9 else 0)
    return f'settime {day} 9 0'


def repairable_surface_holes(evidence):
    """Identify only exact native loaded-air holes; native repair revalidates."""
    rows = evidence.get('missingBlocks')
    if not isinstance(rows, list) or not 1 <= len(rows) <= 32:
        return None
    result = []
    for row in rows:
        point = row.get('position', {})
        if (row.get('loaded') is not True or type(row.get('actualType')) is not int or row['actualType'] != 0
                or row.get('actualBlock') != 'air' or type(row.get('expectedType')) is not int
                or row['expectedType'] <= 0 or any(type(point.get(k)) is not int for k in ('x', 'y', 'z'))):
            return None
        if (not evidence['xMin'] <= point['x'] < evidence['xMin'] + evidence['width']
                or not evidence['zMin'] <= point['z'] < evidence['zMin'] + evidence['length']
                or point['y'] != evidence['floorBlockY']):
            return None
        result.append((point['x'], point['y'], point['z']))
    if len(set(result)) != len(result):
        return None
    return tuple(sorted(result))


def repair_area_empty(track, state, entities):
    rows = entities.get('entities')
    if not isinstance(rows, list) or type(entities.get('count')) is not int or entities['count'] != len(rows):
        raise RuntimeError('Complete current pad repair occupancy evidence required.')
    positions = [state.get('player', {}).get('position')] + [row.get('position') for row in rows]
    for point in positions:
        if not isinstance(point, list) or len(point) != 3 or any(type(v) not in (int, float) or not math.isfinite(v) for v in point):
            raise RuntimeError('Finite current pad repair entity position required.')
        if (track['xMin'] - 4 <= point[0] <= track['xMin'] + track['width'] + 4
                and track['zMin'] - 4 <= point[2] <= track['zMin'] + track['length'] + 4):
            return False
    return True


def choose_empty_pad(open_ground, state, entities, allow_player_move=False):
    """Choose near the observed 80m natural-ground square, before any mutation.

    The native pad command still validates every block, chunk and prefab. Include
    the local player explicitly: /entities intentionally omits that player.
    Dead entities also occupy the native fixture's protected footprint.
    """
    def position(value):
        if not isinstance(value, list) or len(value) != 3 or any(
                isinstance(n, bool) or not isinstance(n, (int, float)) or not math.isfinite(n) for n in value):
            raise RuntimeError('Complete finite pad occupancy positions required.')
        return value
    center = position(open_ground.get('position'))
    player = position(state.get('player', {}).get('position'))
    rows = entities.get('entities')
    if not isinstance(rows, list) or type(entities.get('count')) is not int or entities['count'] != len(rows):
        raise RuntimeError('Complete pad occupancy scan required.')
    occupied = ([] if allow_player_move else [player]) + [position(row.get('position')) for row in rows]
    # A32m lateral shift plus24m protected half-width remains inside the
    # native search's62m prefab exclusion margin. Terrain beyond its sampled
    # square is never assumed flat: exhaustive native volume/foundation
    # preflight remains mandatory before any change.
    z = round(center[2])
    candidates = sorted(range(math.ceil(center[0] - 32), math.floor(center[0] + 32) + 1),
                        key=lambda x: abs(x - center[0]))
    for x in candidates:
        if (x - player[0]) ** 2 + (z - player[2]) ** 2 > 150 ** 2:
            continue
        if any(x - 24 <= point[0] <= x + 24 and z - 44 <= point[2] <= z + 44 for point in occupied):
            continue
        return [x, z, 0, 80, 40]
    raise RuntimeError('Observed natural-ground square has no entity-free pad; no world changes applied.')


def pad_search_candidates(open_ground, state, entities, parked_only=False):
    """Bounded full-sized candidates; every candidate requires native preflight."""
    # Reuse its strict finite/complete observation validation. Planning excludes
    # the player here only to validate, then the candidate gate includes everyone.
    try:
        choose_empty_pad(open_ground, state, entities, allow_player_move=True)
    except RuntimeError as error:
        if not str(error).startswith('Observed natural-ground square has no entity-free pad'):
            raise
    player = state['player']['position']
    occupied = [player] + [row['position'] for row in entities['entities']]
    length, width = (20, 12) if parked_only else (80, 40)
    half_x, half_z = width / 2 + 4, length / 2 + 4
    x0, _, z0 = open_ground['position']
    offsets = sorted(((x, z) for x in range(-128, 129, 16) for z in range(-128, 129, 16)),
                     key=lambda pair: pair[0] ** 2 + pair[1] ** 2)
    result = []
    if not parked_only:
        try:
            result.append(choose_empty_pad(open_ground, state, entities))
        except RuntimeError:
            pass  # Complete validated occupancy may cover the nearest square.
    for dx, dz in offsets:
        x, z = round(x0 + dx), round(z0 + dz)
        if (x - player[0]) ** 2 + (z - player[2]) ** 2 > 145 ** 2:
            continue
        if any(x - half_x <= p[0] <= x + half_x and z - half_z <= p[2] <= z + half_z for p in occupied):
            continue
        candidate = [x, z, 0, length, width]
        if candidate not in result:
            result.append(candidate)
    return result


def same_session(current, expected):
    if type(expected.get('pid')) is not int or expected['pid'] <= 0 or type(expected.get('port')) is not int \
            or not 1 <= expected['port'] <= 65535 or not isinstance(expected.get('token'), str) or not expected['token']:
        return False
    return all(current.get(key) == expected.get(key) for key in ('pid', 'port', 'token'))


def require_no_live_zombies(reply):
    # /entities count includes corpses and can exceed its returned limit.
    rows = reply.get('entities')
    if not isinstance(rows, list) or type(reply.get('count')) is not int or reply['count'] != len(rows):
        raise RuntimeError('Incomplete nearby zombie evidence; abort.')
    for row in rows:
        if row.get('dead') is True:
            continue
        health = row.get('health')
        if row.get('dead') is not False or isinstance(health, bool) or not isinstance(health, (int, float)):
            raise RuntimeError('Unobservable nearby zombie life state; abort.')
        if health > 0:
            raise RuntimeError('Live zombies remain after clear; abort.')


def prepare_safe_arrival(act, query):
    # Clear the arrival before suppressing the protective routines. The game may
    # have loaded near enemies; native clear leaves dead entities in the world.
    act('/enemyspawns', on=0)
    clear = act('/cleararea', radius=150)
    if clear.get('remaining') != []:
        raise RuntimeError('Native clear did not confirm an empty threat scan; abort.')
    require_no_live_zombies(query('/entities', radius=150, contains='zombie', limit=1000))
    act('/restore')
    guard = act('/guard', enabled=0, needs=0, supply=0, agenda=0, explore=0, glance=0, heal=0, reload=0, bleed=0)
    if guard.get('bleed') is not False:
        raise RuntimeError('Current Bridge must confirm independent bleed reflex suppression.')


def receipt_path(reply, marker, allowed):
    lines = [line for entry in reply.get('results', []) for line in entry.get('output', [])]
    paths = [Path(line.split(marker, 1)[1]).resolve() for line in lines if marker in line]
    if reply.get('hadError') or any('*** ERROR' in line or '***ERROR' in line for line in lines) or len(paths) != 1:
        raise RuntimeError('Missing unique successful receipt: ' + str(lines))
    path = paths[0]
    if not path.is_relative_to(allowed.resolve()) or not path.is_file():
        raise RuntimeError('Unexpected or absent evidence path.')
    return path


def wait_quiescent(status, observe, timeout=15, clock=time.monotonic, pause=time.sleep):
    """Guard busy counts Busy(need/task), fights and bleed/heal routines.

    Needs.Status/Agenda expose no independent busy field. Use the actual common
    activeRoutines counter instead of inventing one; require two idle samples.
    """
    deadline = clock() + timeout
    idle = 0
    while clock() < deadline:
        value = status()
        observe(value)
        if any(value.get(key) is not False for key in ('enabled', 'heal', 'reload', 'bleed')):
            raise RuntimeError('Guard suppression changed or is not explicitly confirmed.')
        needs = value.get('needs')
        if not isinstance(needs, dict) or any(needs.get(key) is not False for key in ('enabled', 'supply')):
            raise RuntimeError('Needs/supply suppression is not explicitly confirmed.')
        if type(value.get('busy')) is not bool:
            raise RuntimeError('Actual shared automation activity counter is unavailable.')
        idle = idle + 1 if not value['busy'] else 0
        if idle >= 2:
            return
        pause(.5)
    raise RuntimeError('Independent automation did not become quiescent within15seconds; no camper setup permitted.')


def parked_ready(data, entity):
    if data.get('entityId') != entity or data.get('entityClass') not in ('rebirthCamper80s', 'vehicleTruck4x4'):
        raise RuntimeError('Exact parked vehicle identity required.')
    seats = data.get('seats')
    if not isinstance(seats, list) or not seats or any(s.get('occupantId') != -1 or s.get('attachedToCamper') is not False for s in seats):
        raise RuntimeError('Mount requires explicitly empty parked vehicle.')
    speed = data.get('speed')
    angular = data.get('rigidbodyState', {}).get('angularVelocity', {})
    if type(speed) not in (int, float) or not math.isfinite(speed) or any(type(angular.get(k)) not in (int, float) or not math.isfinite(angular[k]) for k in ('x', 'y', 'z')):
        raise RuntimeError('Actual finite parked physics evidence required.')
    return speed <= .1 and math.sqrt(sum(angular[k]**2 for k in ('x', 'y', 'z'))) <= .1


class Suite:
    def __init__(self, pid, receipt):
        self.session = json.loads(Path(receipt).read_text(encoding='utf-8-sig'))
        if self.session.get('pid') != pid or not same_session(json.loads(SESSION.read_text(encoding='utf-8-sig')), self.session):
            raise RuntimeError('Explicit authorized session receipt must match the live owned PID/port/token.')
        self.pid = pid
        self.folder = STUDIO / 'reports/qualification' / uuid.uuid4().hex
        self.folder.mkdir(parents=True)
        self.log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
        self.stop = threading.Event()
        self.failure = None
        self.report = {'pid': pid, 'status': 'RUNNING', 'phases': [], 'gameplayAcceptance': False,
            'pendingAcceptance': ['Inspected useful-angle seating, hand contact and steering hub screenshots',
                'Audible native engine and horn', 'Simultaneous comfortable occupants and multiplayer permissions/sync',
                'Native cosmetic red/green/dark/removal UI, pickup and save/load persistence',
                'Native modular storage and mobile station UI, processing and world persistence']}

    def raw(self, path, method='GET', request_timeout=5, **params):
        if not same_session(json.loads(SESSION.read_text(encoding='utf-8-sig')), self.session):
            raise RuntimeError('Session changed; leave the replacement session untouched.')
        url = f'http://127.0.0.1:{self.session["port"]}{path}'
        if params:
            url += '?' + urllib.parse.urlencode(params)
        request = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': self.session['token']})
        with urllib.request.urlopen(request, timeout=request_timeout) as response:
            result = json.load(response)
        if not result.get('ok') or result.get('hadError'):
            raise RuntimeError(str(result))
        return result

    def identity(self):
        if running_game_pids() != {self.pid}:
            raise RuntimeError('The authorized game is no longer the sole instance; stop owned test input.')
        ping = self.raw('/ping')
        if ping.get('pid') != self.pid or ping.get('state') != 'ingame' or heartbeat_stall_ms(ping) > 10000:
            raise RuntimeError('Owned game main thread is not responsive and in-game.')
        if self.raw('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
            raise RuntimeError('Disposable save changed; no further test input allowed.')
        return ping

    def act(self, path, **params):
        if self.failure:
            raise RuntimeError(self.failure)
        self.identity()  # actual main-thread state completion, not process liveness
        request_timeout = min(60, params.get('maxSeconds', 25) + 5) if path == '/walkto' else 5
        result = self.raw(path, 'POST', request_timeout=request_timeout, **params)
        self.report['phases'].append({'path': path, 'parameters': params, 'response': result})
        lines = [line for row in result.get('results', []) for line in row.get('output', [])]
        if any('*** ERROR' in line or '***ERROR' in line for line in lines):
            raise RuntimeError('Native command refused: ' + str(lines))
        return result

    def monitor(self):
        seen = 0
        with (self.folder / 'health.jsonl').open('w', encoding='utf-8') as output:
            while not self.stop.is_set():
                observation = {'time': time.time()}
                try:
                    lines = self.log.read_text(encoding='utf-8', errors='replace').splitlines()
                    if len(lines) < seen:
                        seen = 0
                    fresh = lines[seen:]
                    seen = len(lines)
                    observation.update(logLines=seen, warnings=[line for line in fresh if WARNING.search(line)],
                        errors=[line for line in fresh if FATAL.search(line)])
                    observation['ping'] = self.identity()
                    if observation['errors']:
                        raise RuntimeError('Actual full game log contains runtime errors: ' + '; '.join(observation['errors'][:4]))
                except Exception as error:
                    self.failure = str(error)
                    observation['failure'] = self.failure
                output.write(json.dumps(observation) + '\n')
                output.flush()
                if self.failure:
                    return
                self.stop.wait(2)

    def child(self, script, arguments, timeout):
        if self.failure:
            raise RuntimeError(self.failure)
        output_path = self.folder / (Path(script).stem + '.txt')
        with output_path.open('w', encoding='utf-8') as output:
            process = subprocess.Popen([sys.executable, str(STUDIO / script), *map(str, arguments)], stdout=output, stderr=subprocess.STDOUT)
            deadline = time.monotonic() + timeout
            try:
                while process.poll() is None:
                    if self.failure or time.monotonic() >= deadline:
                        raise RuntimeError(self.failure or script + ' exceeded its bounded phase timeout.')
                    time.sleep(.2)
                if process.returncode:
                    raise RuntimeError(script + ' failed; inspect ' + str(output_path))
            finally:
                if process.poll() is None:
                    process.terminate()  # only this Python child; native sequence lease expires
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill(); process.wait(timeout=5)
        self.report['phases'].append({'collector': script, 'output': str(output_path), 'exitCode': process.returncode})

    def diagnostic(self, command, marker):
        exports = Path(os.environ['APPDATA']) / '7DaysToDie/Mods/REBIRTH_VehicleInspector_Dev/Exports/Camper'
        path = receipt_path(self.act('/console', cmd=command), marker, exports)
        shutil.copyfile(path, self.folder / path.name)
        return json.loads(path.read_text(encoding='utf-8-sig'))

    def find_pad(self, ground, state, entities, parked_only=False):
        inspections = self.report.setdefault('parkedPadInspections' if parked_only else 'padInspections', [])
        protected_tiles = []
        for candidate in pad_search_candidates(ground, state, entities, parked_only):
            x, z, _, length, width = candidate
            if any(x - width / 2 - 4 <= p['x'] <= x + width / 2 + 4 and z - length / 2 - 4 <= p['z'] <= z + length / 2 + 4 for p in protected_tiles):
                continue
            if len(inspections) >= (16 if parked_only else 32):
                break
            data = self.diagnostic('camperpad inspect CodexTest ' + ' '.join(map(str, candidate)), '[CamperTrack] REFUSED_CAPTURE ')
            if (data.get('kind') != 'CAMPER_PAD_READ_ONLY_PREFLIGHT' or data.get('width') != width
                    or data.get('length') != length or data.get('xMin') != x - width // 2
                    or data.get('zMin') != z - length // 2):
                raise RuntimeError('Read-only pad evidence refers to another footprint.')
            inspections.append(dict(candidate=candidate, evidence=data))
            for tile in data.get('preservedTileEntities', []):
                position = tile.get('position', {})
                if any(type(position.get(k)) not in (int, float) or not math.isfinite(position[k]) for k in ('x', 'y', 'z')):
                    raise RuntimeError('Incomplete native preserved-container position.')
                protected_tiles.append(position)
            if data.get('status') == 'READY_NO_WORLD_CHANGES':
                if data.get('blockers') != [] or data.get('mutationLimitExceeded') is not False:
                    raise RuntimeError('Ready pad contains contradictory blocker evidence.')
                return candidate
        purpose = 'parked-only' if parked_only else 'full80x40'
        raise RuntimeError('Bounded ' + purpose + ' read-only search found no admissible footprint; no world changes applied.')

    def wait_parked(self, entity):
        deadline = time.monotonic() + 15
        idle = 0
        while time.monotonic() < deadline:
            state = self.diagnostic(f'camperdiag {entity}', '[CamperDiagnostics] PASS_CAPTURE ')
            idle = idle + 1 if parked_ready(state, entity) else 0
            if idle >= 2:
                self.report['phases'].append({'parkedBeforeMount': entity, 'idleSamples': idle})
                return
            time.sleep(.5)
        raise RuntimeError('Vehicle did not settle parked before mounting; no seat input sent.')

    def place_observed_camper(self, x, z, surface_y):
        receipt = self.folder / 'parked_watch_session.json'
        receipt.write_text(json.dumps(self.session), encoding='utf-8')
        output_path = self.folder / 'parked_placement_watch.txt'
        with output_path.open('w', encoding='utf-8') as output:
            child = subprocess.Popen([sys.executable, str(STUDIO / 'capture_parked_camper.py'),
                '--owned-pid', str(self.pid), '--authorized-session-receipt', str(receipt), '--watch-new'],
                stdout=output, stderr=subprocess.STDOUT)
            try:
                deadline = time.monotonic() + 20
                while time.monotonic() < deadline:
                    self.identity()
                    if self.failure or child.poll() is not None:
                        raise RuntimeError(self.failure or 'Parked placement watcher exited before READY.')
                    if 'READY watching' in output_path.read_text(encoding='utf-8', errors='replace'):
                        break
                    time.sleep(.1)
                else:
                    raise RuntimeError('Parked placement watcher did not confirm bounded readiness; no camper item placement.')
                entity = self.place('rebirthCamper80s', 'rebirthCamper80sPlaceable', x, z, surface_y)
                deadline = time.monotonic() + 35
                while child.poll() is None:
                    self.identity()
                    if self.failure or time.monotonic() >= deadline:
                        raise RuntimeError(self.failure or 'Parked placement capture exceeded bound.')
                    time.sleep(.2)
                if child.returncode:
                    raise RuntimeError('Parked placement physics capture refused; inspect ' + str(output_path))
                self.report['phases'].append({'parkedPlacementWatch': str(output_path), 'exitCode': child.returncode})
                return entity
            finally:
                if child.poll() is None:
                    child.terminate()  # Only the retained read-only Python observer.
                    try:
                        child.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        child.kill(); child.wait(timeout=5)

    def place(self, name, item, x, z, surface_y):
        existing = {e['id'] for e in self.raw('/entities', radius=150, limit=1000).get('entities', [])}
        self.act('/walkto', x=x, z=z - 5, radius=.5, run=1, maxSeconds=35)
        self.act('/give', item=item, count=1, quality=6, toolbelt=1)
        self.act('/select', slot=1)
        self.act('/lookat', x=x, y=surface_y, z=z)
        self.act('/press', action='Secondary', seconds=.3)
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            self.identity()
            rows = self.raw('/entities', radius=150, limit=1000).get('entities', [])
            created = [e for e in rows if e['id'] not in existing and e.get('class') == name]
            if len(created) == 1:
                self.report['phases'].append({'normalItemPlacement': name, 'entity': created[0]})
                return created[0]['id']
            if len(created) > 1:
                raise RuntimeError('Ambiguous new vehicle identity; no retry.')
            time.sleep(.25)
        self.report['placementRefusalEvidence'] = self.raw('/console', 'POST', cmd='camperplacementdiag')
        exports = Path(os.environ['APPDATA']) / '7DaysToDie/Mods/REBIRTH_VehicleInspector_Dev/Exports/Camper'
        placement_path = receipt_path(self.report['placementRefusalEvidence'], '[CamperPlacement] CAPTURED ', exports)
        shutil.copyfile(placement_path, self.folder / placement_path.name)
        self.report['placementRefusalScreenshot'] = self.raw('/screenshot', 'POST', name='camper_native_placement_refusal')
        raise RuntimeError('Normal vehicle item placement not confirmed; native preview evidence captured; do not silently substitute a raw spawn or retry.')

    def run(self, pad, quit_on_failure=False, auto_pad=False):
        monitor = threading.Thread(target=self.monitor, daemon=True)
        monitor.start()
        parked_only = False
        try:
            prepare_safe_arrival(self.act, self.raw)
            def guard_status():
                if self.failure:
                    raise RuntimeError(self.failure)
                self.identity()
                return self.raw('/guard')
            wait_quiescent(guard_status, lambda value: self.report['phases'].append({'quiescence': value}))
            self.act('/restore')
            world = self.raw('/state', sections='world').get('world', {})
            daylight = daylight_command(world)
            self.report['clockBefore'] = world
            # Preserve the shared save's earned history and never rewind its clock.
            if daylight:
                self.act('/console', cmd=daylight)
            if auto_pad:
                observed_ground = self.raw('/openground', radius=145, size=40)
                occupancy_state = self.raw('/state', sections='player,world')
                occupancy_entities = self.raw('/entities', radius=1000, limit=10000)
                self.report['padSelection'] = dict(ground=observed_ground,
                    state=occupancy_state, entities=occupancy_entities, gameplayAcceptance=False)
                try:
                    pad = self.find_pad(observed_ground, occupancy_state, occupancy_entities)
                except RuntimeError as error:
                    if not str(error).startswith('Bounded full80x40 read-only search found no admissible footprint'):
                        raise
                    self.report['handlingPending'] = str(error)
                    # Independent stationary/UI evidence may proceed. This
                    # parking fixture cannot enter the handling comparison.
                    pad = self.find_pad(observed_ground, occupancy_state, occupancy_entities, parked_only=True)
                    parked_only = True
                    self.report['fixturePurpose'] = 'PARKED_SEATING_AND_UI_ONLY_NOT_HANDLING'
                self.report['padSelection']['pad'] = pad
            reply = self.act('/console', cmd='camperpad CodexTest ' + ' '.join(map(str, pad)))
            diagnostic_exports = Path(os.environ['APPDATA']) / '7DaysToDie/Mods/REBIRTH_VehicleInspector_Dev/Exports/Camper'
            track_path = receipt_path(reply, '[CamperTrack] PREPARED ', diagnostic_exports)
            track = json.loads(track_path.read_text(encoding='utf-8-sig'))
            if track.get('kind') != 'FLAT_NATIVE_CONCRETE_TEST_PAD':
                raise RuntimeError('Flat pad receipt required.')
            shutil.copyfile(track_path, self.folder / 'track.json')
            deadline = time.monotonic() + 15
            previous_holes = None
            stable_holes = 0
            repair_attempted = False
            while time.monotonic() < deadline:
                values = [track[k] for k in ('xMin', 'zMin', 'floorBlockY', 'width', 'length')]
                ready = self.diagnostic('camperpadready ' + ' '.join(map(str, values)), '[CamperPadReady] CAPTURED ')
                if ready.get('kind') != 'NATIVE_CONCRETE_PAD_READINESS' or any(ready.get(k) != track[k] for k in ('xMin', 'zMin', 'floorBlockY', 'width', 'length')):
                    raise RuntimeError('Collider readiness evidence refers to another pad.')
                self.report['padColliderReadiness'] = ready
                if ready.get('ready') is True:
                    break
                holes = repairable_surface_holes(ready)
                stable_holes = stable_holes + 1 if holes and holes == previous_holes else 0
                previous_holes = holes
                if holes and stable_holes >= 3 and not repair_attempted:
                    occupancy = dict(state=self.raw('/state', sections='player'),
                        entities=self.raw('/entities', radius=1000, limit=10000))
                    self.report['padRepairOccupancy'] = occupancy
                    if not repair_area_empty(track, occupancy['state'], occupancy['entities']):
                        # Native construction can create entities after preflight.
                        # Wait read-only within the same15second readiness bound.
                        time.sleep(.25)
                        continue
                    repair_attempted = True
                    repair = self.diagnostic('camperpadready repair ' + ' '.join(map(str, values)),
                        '[CamperPadRepair] CAPTURED ')
                    self.report['padAirHoleRepair'] = repair
                    if (repair.get('kind') != 'CAMPER_PAD_AIR_HOLE_REPAIR'
                            or repair.get('readinessAcceptance') is not False
                            or repair.get('sameWorldPreparation') is not True
                            or repair.get('repairBudgetConsumed') is not True
                            or repair.get('repairedCount') != len(holes)
                            or not isinstance(repair.get('repairedPositions'), list)
                            or any(repair.get(k) != track[k] for k in ('xMin', 'zMin', 'floorBlockY', 'width', 'length'))):
                        raise RuntimeError('Native air-hole repair evidence does not match this prepared fixture.')
                    repaired = repair['repairedPositions']
                    if (any(any(type(p.get(k)) is not int for k in ('x', 'y', 'z')) for p in repaired)
                            or tuple(sorted((p['x'], p['y'], p['z']) for p in repaired)) != holes):
                        raise RuntimeError('Native repair changed cells outside the observed air-hole set.')
                time.sleep(.25)
            else:
                raise RuntimeError('Native pad block/collider readiness expired; no vehicles placed.')
            # Both normally placed before seating or driving; leave clearance between them.
            x = track['xMin'] + track['width'] / 2
            if parked_only:
                camper = self.place_observed_camper(x, track['zMin'] + 10, track['surfaceY'])
                self.report['camper'] = camper
                self.wait_parked(camper)
                self.child('validate_camper_seats.py', ['--owned-pid', self.pid, '--entity', camper], 240)
                self.child('run_camper_probes.py', ['--owned-pid', self.pid], 90)
                from camper_ui_qualification import CamperUI
                CamperUI(self, camper).run()
                self.identity()
                if self.failure:
                    raise RuntimeError(self.failure)
                self.report['status'] = 'PARTIAL_PARKED_CAPTURE_FULL_HANDLING_PENDING'
                return 0
            z = track['zMin'] + 14
            native = self.place('vehicleTruck4x4', 'vehicleTruck4x4Placeable', x - 8, z, track['surfaceY'])
            camper = self.place_observed_camper(x + 8, z, track['surfaceY'])
            self.report.update(native=native, camper=camper)
            self.wait_parked(native)
            self.wait_parked(camper)
            self.child('validate_camper_seats.py', ['--owned-pid', self.pid, '--entity', camper], 240)
            self.child('compare_vehicle_handling.py', ['--pid', self.pid, '--native', native, '--camper', camper,
                '--track-receipt', track_path, '--lights'], 180)
            self.child('run_camper_probes.py', ['--owned-pid', self.pid], 90)
            from camper_ui_qualification import CamperUI
            CamperUI(self, camper).run()
            self.identity()
            if self.failure:
                raise RuntimeError(self.failure)
            self.report['status'] = 'CAPTURED_PENDING_ACCEPTANCE'
        except Exception as error:
            self.report.update(status='FAIL', error=str(error))
            # Each request must still identify the exact owned session; a stalled
            # native sequence relinquishes its own input when its lease expires.
            try:
                self.identity()
                self.report['release'] = self.raw('/release', 'POST')
                self.report['failureEntities'] = self.raw('/entities', radius=150, limit=1000)
                self.report['failureScreenshot'] = self.raw('/screenshot', 'POST', name='camper_suite_failure')
            except Exception as cleanup:
                self.report['cleanupError'] = str(cleanup)
                self.report['recoveryRequired'] = 'Verify owned PID and gracefully close; never stop another session.'
            if quit_on_failure:
                try:
                    # Attempt even after a main-thread stall: ping is answered
                    # off-thread, and raw() still requires the exact session token.
                    ping = self.raw('/ping')
                    self.report['failureHeartbeat'] = ping
                    if ping.get('pid') != self.pid:
                        raise RuntimeError('Owned PID changed; no shutdown permitted.')
                    self.report['gracefulShutdown'] = self.raw('/quit', 'POST')
                except Exception as shutdown:
                    self.report['gracefulShutdownError'] = str(shutdown)
        finally:
            self.stop.set()
            monitor.join(timeout=12)
            if self.log.exists():
                shutil.copyfile(self.log, self.folder / 'Player.log')
            (self.folder / 'result.json').write_text(json.dumps(self.report, indent=2), encoding='utf-8')
            print(str(self.folder / 'result.json'), flush=True)
        return int(self.report['status'] == 'FAIL')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-pid', type=int, required=True)
    parser.add_argument('--authorized-session-receipt', required=True, help='Receipt copied when this exact PID/session was authorized.')
    parser.add_argument('--pad', type=int, nargs=5, required=True, metavar=('X', 'Z', 'FLOOR_Y', 'LENGTH', 'WIDTH'),
        help='Nearby loaded unoccupied natural ground; automatic floor=0; require length80 width40 for vehicle clearance.')
    parser.add_argument('--quit-on-failure', action='store_true', help='Attempt graceful shutdown of the exact owned session after failure.')
    parser.add_argument('--auto-pad', action='store_true', help='Select an entity-free pad inside observed nearby natural ground before native preflight.')
    args = parser.parse_args()
    if args.pad[3:] != [80, 40]:
        parser.error('Qualification requires the full80x40 flat pad for matched handling and vehicle clearance.')
    raise SystemExit(Suite(args.owned_pid, args.authorized_session_receipt).run(args.pad, args.quit_on_failure, args.auto_pad))
