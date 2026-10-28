"""Explicit-slot, single-launch supervisor. Never builds, resets, restarts or kills a game."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import urllib.parse
import urllib.request
import uuid

from run_camper_qualification import Suite, SESSION, MOD, STUDIO, same_session, prepare_safe_arrival
from watch_game_health import FATAL, heartbeat_stall_ms


def validate_launch(config, saves):
    if config.get('save') != 'CodexTest' or not isinstance(config.get('world'), str):
        raise RuntimeError('Existing disposable CodexTest configuration required; no save override permitted.')
    target = saves / config['world'] / config['save']
    if not target.is_dir():
        raise RuntimeError('Existing test save required; launcher must not initialize or copy a baseline.')
    return target


def processes():
    command = "@(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue | Select-Object Id,Path) | ConvertTo-Json -Compress"
    result = subprocess.run(['powershell', '-NoProfile', '-Command', command], capture_output=True, text=True, check=True)
    value = json.loads(result.stdout.strip() or '[]')
    return [value] if isinstance(value, dict) else value


def require_owned(rows, pid, executable):
    if len(rows) != 1 or rows[0].get('Id') != pid or Path(rows[0].get('Path') or '').resolve() != executable.resolve():
        raise RuntimeError('Exact sole owned game process changed; no further input permitted.')


def observe_session(current, expected, pid):
    if expected is None:
        # The native Bridge overwrites the prior launch's receipt asynchronously.
        # Waiting is read-only; process ownership is checked separately every poll.
        if current.get('pid') != pid:
            return None
        if not same_session(current, current):
            raise RuntimeError('New owned Bridge receipt is incomplete.')
        return current
    if not same_session(current, expected):
        raise RuntimeError('Owned Bridge session changed.')
    return expected


def run(authorization, pad, timeout=600, prepare_only=False, auto_pad=False):
    if not authorization.strip():
        raise RuntimeError('Explicit coordinated slot authorization required.')
    candidate = Path(authorization)
    try:
        authorization = candidate.read_text(encoding='utf-8-sig') if candidate.is_file() else authorization
    except OSError:
        pass  # A receipt may be literal text rather than a path.
    if not authorization.strip():
        raise RuntimeError('Empty slot receipt refused.')
    config = json.loads((MOD / 'Tools/GameBridge/gamebridge.config.json').read_text(encoding='utf-8-sig'))
    validate_launch(config, Path(os.environ['APPDATA']) / '7DaysToDie/Saves')
    if processes():
        raise RuntimeError('An existing game process owns the slot; no launch permitted.')
    folder = STUDIO / 'reports/launch_supervision' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    report = {'status': 'FAIL', 'authorization': authorization, 'build': False, 'reset': False, 'restart': False}
    log = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/The Fun Pimps/7 Days To Die/Player.log'
    executable = MOD.parents[1] / '7DaysToDie.exe'
    pid = None
    started = time.time()
    environment = dict(os.environ, REBIRTH_BRIDGE_NOCHORES='1', REBIRTH_BRIDGE_HUMAN='1')
    try:
        # The existing launcher retains its own second-instance check. Child-only
        # startup flags suppress chores/reflexes before an HTTP request is possible.
        with (folder / 'launcher.log').open('w', encoding='utf-8') as output:
            launch = subprocess.Popen(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                str(MOD / 'Tools/GameBridge/gamebridge.ps1'), 'launch', '-NoBuild', '-NoWait'],
                env=environment, stdout=output, stderr=subprocess.STDOUT)
            launch.wait(timeout=30)
            if launch.returncode:
                raise RuntimeError('Single launch failed; inspect receipt. Never automatically retry.')
        rows = processes()
        if len(rows) != 1:
            raise RuntimeError('Launch did not create one unambiguous game process.')
        pid = rows[0]['Id']
        require_owned(rows, pid, executable)
        report['pid'] = pid
        expected = None
        ping_miss_started = None
        deadline = time.monotonic() + timeout
        with (folder / 'loading_health.jsonl').open('w', encoding='utf-8') as health:
            while time.monotonic() < deadline:
                require_owned(processes(), pid, executable)
                if log.exists() and log.stat().st_mtime >= started:
                    text = log.read_text(encoding='utf-8', errors='replace')
                    if any(FATAL.search(line) for line in text.splitlines()):
                        raise RuntimeError('Fresh full Player.log contains a runtime error; no test input sent.')
                if SESSION.exists():
                    current = json.loads(SESSION.read_text(encoding='utf-8-sig'))
                    first = expected is None
                    expected = observe_session(current, expected, pid)
                    if expected is None:
                        time.sleep(.25)
                        continue
                    if first:
                        receipt = folder / 'owned_session.json'
                        receipt.write_text(json.dumps(expected), encoding='utf-8')
                    def call(path, method='GET', **params):
                        require_owned(processes(), pid, executable)
                        if not same_session(json.loads(SESSION.read_text(encoding='utf-8-sig')), expected):
                            raise RuntimeError('Owned session changed before request.')
                        url = f'http://127.0.0.1:{expected["port"]}{path}'
                        if params:
                            url += '?' + urllib.parse.urlencode(params)
                        request = urllib.request.Request(url, method=method, headers={'X-Bridge-Token': expected['token']})
                        with urllib.request.urlopen(request, timeout=5) as response:
                            data = json.load(response)
                        if not data.get('ok') or data.get('hadError'):
                            raise RuntimeError('Bridge request failed: ' + str(data))
                        return data
                    ping_attempt_started = time.monotonic()
                    try:
                        ping = call('/ping')
                    except (TimeoutError, urllib.error.URLError) as connection:
                        transient = isinstance(connection, TimeoutError) or isinstance(getattr(connection, 'reason', None), (TimeoutError, ConnectionRefusedError))
                        if not transient:
                            raise
                        if ping_miss_started is None:
                            ping_miss_started = ping_attempt_started
                        health.write(json.dumps({'pid': pid, 'startupConnectionMiss': str(connection),
                            'missSeconds': time.monotonic() - ping_miss_started}) + '\n'); health.flush()
                        if time.monotonic() - ping_miss_started >= 15:
                            raise RuntimeError('Startup connection did not recover within15seconds; no test input sent.') from connection
                        # Re-enter the loop: sole process, full log and exact
                        # copied session are revalidated before another GET.
                        time.sleep(.25)
                        continue
                    ping_miss_started = None
                    health.write(json.dumps(ping) + '\n'); health.flush()
                    # Actual native XUi parsing blocked the loading heartbeat for
                    # 34seconds, then recovered with clean logs. Keep gameplay's
                    # strict10second bound; allow bounded startup work60seconds.
                    if ping.get('pid') != pid or heartbeat_stall_ms(ping) > (10000 if ping.get('state') == 'ingame' else 60000):
                        raise RuntimeError('Owned main-thread health exceeded its bound.')
                    if ping.get('state') == 'dead':
                        raise RuntimeError('Player died during arrival; never auto-respawn or retry.')
                    if ping.get('state') == 'ingame':
                        if call('/state', sections='world').get('world', {}).get('gameName') != 'CodexTest':
                            raise RuntimeError('Loaded save changed; no setup permitted.')
                        prepare_safe_arrival(lambda path, **params: call(path, 'POST', **params), call)
                        report['safeArrival'] = True
                        if prepare_only:
                            report['ownedSessionReceipt'] = str(receipt)
                            report['status'] = 'READY_FOR_PARKED_OBSERVATION'
                            return 0
                        suite = Suite(pid, receipt)
                        report['suiteResult'] = str(suite.folder / 'result.json')
                        code = suite.run(pad, quit_on_failure=True, auto_pad=auto_pad)
                        report['status'] = suite.report['status']
                        return code
                time.sleep(.25)
        raise RuntimeError('Bounded loading window expired; leave the owned process for coordinated recovery.')
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
    parser.add_argument('--authorized-slot', required=True, help='Explicit fresh coordinated slot receipt text or path.')
    parser.add_argument('--pad', type=int, nargs=5, required=True)
    parser.add_argument('--loading-timeout', type=int, default=600)
    parser.add_argument('--prepare-only', action='store_true', help='Stop after safe arrival; do not place or mount vehicles before parked observation.')
    parser.add_argument('--auto-pad', action='store_true', help='Automatically select nearby entity-free natural ground before the native pad preflight.')
    args = parser.parse_args()
    if args.pad[3:] != [80, 40] or not 30 <= args.loading_timeout <= 900:
        parser.error('Require 80x40 pad and loading bound30..900seconds.')
    raise SystemExit(run(args.authorized_slot, args.pad, args.loading_timeout, args.prepare_only, args.auto_pad))
