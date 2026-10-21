"""One-game, exclusive sequential batch runner. Preflight never contacts/launches game."""
import argparse
import hashlib
from functools import lru_cache
import json
import os
import re
from pathlib import Path
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = HERE / 'out' / 'batches'


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    stage = path.with_suffix('.tmp')
    stage.write_text(json.dumps(value, indent=2), encoding='utf-8')
    stage.replace(path)


def inside(path):
    path = (HERE / path).resolve()
    if HERE.resolve() not in path.parents:
        raise ValueError('Test/driver path must stay within GameBridge')
    if not path.is_file():
        raise ValueError('Missing file: ' + str(path))
    return path



@lru_cache(maxsize=1)
def supported_steps():
    source = (HERE / 'gamebridge.ps1').read_text(encoding='utf-8-sig')
    table = source.split('$KvCommands = @{', 1)[1].split('function ConvertTo-Query', 1)[0]
    commands = {x.lower() for x in re.findall(r"([A-Za-z][A-Za-z0-9_]*)\s*=\s*@\(", table)}
    runner = source.split('function Invoke-TestFile', 1)[1].split('# ------------------------------------------------------------------ dispatch', 1)[0]
    kinds = {x.lower() for x in re.findall(r"'([A-Za-z][A-Za-z0-9_]*)'\s*\{", runner)}
    return commands, kinds | commands


def validate_case(case):
    if not case.get('id') or case.get('kind') not in ('scenario', 'python', 'manual'):
        raise ValueError('Invalid case identity/type')
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,180}', case['id']):
        raise ValueError('Unsafe case id')
    if not 1 <= case.get('timeoutSeconds', 600) <= 3600:
        raise ValueError('Case timeout must be between 1 and 3600 seconds')
    if case['kind'] == 'manual':
        return
    path = inside(case['file'])
    if case['kind'] == 'scenario':
        data = read(path)
        steps = data.get('steps') if isinstance(data, dict) else None
        if not isinstance(data, dict) or not data.get('name') or not isinstance(steps, list) or not steps:
            raise ValueError('Executable scenario requires name and steps: ' + case['file'])
        for step in steps:
            if not isinstance(step, dict) or len(step) != 1:
                raise ValueError('Each step requires exactly one action')
        commands, kinds = supported_steps()
        for step in steps:
            kind, arg = next(iter(step.items()))
            if kind.startswith('_'):
                continue
            if kind.lower() not in kinds:
                raise ValueError('Unsupported bridge step: ' + kind)
            command_name = kind.lower()
            command_args = arg
            if command_name == 'action':
                if not isinstance(arg, dict) or str(arg.get('command', '')).lower() not in commands:
                    raise ValueError('Unsupported nested bridge action')
                command_name, command_args = arg['command'].lower(), arg.get('args', {})
            if command_name == 'guard' and isinstance(command_args, dict) and str(command_args.get('enabled', '1')).lower() in ('0', 'false'):
                raise ValueError('Guard must remain enabled')
        actions = [next(iter(s)) for s in steps if not next(iter(s)).startswith('_')]
        if not actions or not any(a.lower().startswith('expect') and a.lower() != 'expectnoerrors' or a == 'fight' for a in actions):
            raise ValueError('Scenario requires a substantive assertion, not just capture/setup')
        if any(a.lower() in ('launch', 'restart', 'quit', 'reset-save', 'build') for a in actions) or any(
                next(iter(step)) == 'action' and str(step['action'].get('command', '')).lower() in ('launch', 'restart', 'quit', 'reset-save', 'build') for step in steps):
            raise ValueError('Lifecycle operations forbidden inside a session batch')
    else:
        compile(path.read_text(encoding='utf-8-sig'), str(path), 'exec')
        if not isinstance(case.get('args'), list) or not case.get('resultFile'):
            raise ValueError('Python case needs explicit args and resultFile')
        if not all(isinstance(a, str) for a in case['args']):
            raise ValueError('Python args must all be strings')
        if any('TODO' in str(a) for a in case['args']):
            raise ValueError('Unpopulated Python plan')
        # Lease propagation must be implemented, not assumed for arbitrary clients.
        source = path.read_text(encoding='utf-8-sig')
        if 'camper_consolidated_regression_live import' not in source and path.name != 'camper_consolidated_regression_live.py':
            raise ValueError('Driver must use the lease-aware shared Runner')
        if Path(case['resultFile']).name != case['resultFile'] or case.get('passField', 'status') not in ('status', 'passed', 'pass'):
            raise ValueError('Result must be a local filename with an explicit supported pass field')
        for prerequisite in case.get('planFiles', []):
            read(inside(prerequisite))


def prepare(plan, selected=None, groups=None, acknowledgements=()):
    if plan.get('schema') != 1 or not isinstance(plan.get('cases'), list):
        raise ValueError('Expected batch schema 1 and cases array')
    if not isinstance(plan.get('captureScreenshots', True), bool):
        raise ValueError('captureScreenshots must be a boolean')
    seen = set()
    result = []
    for case in plan['cases']:
        if case.get('id') in seen:
            raise ValueError('Duplicate case id')
        seen.add(case.get('id'))
        # Validate every executable entry before any game request, even gated ones.
        validate_case(case)
        enabled = case.get('enabled', False) if not selected and not groups else (
            case['id'] in (selected or ()) or case.get('group') in (groups or ()))
        missing = [p for p in case.get('requires', []) if p not in acknowledgements]
        status = 'READY' if enabled and not missing and case['kind'] != 'manual' else 'NOT_RUN'
        result.append({'case': case, 'status': status,
                       'reason': 'prerequisites: ' + ', '.join(missing) if missing else ('manual evidence required' if case['kind'] == 'manual' else 'not selected')})
    unknown_groups = set(groups or ()) - {c.get('group') for c in plan['cases']}
    if unknown_groups:
        raise ValueError('Unknown group: ' + ', '.join(sorted(unknown_groups)))
    unknown = set(selected or ()) - seen
    if unknown:
        raise ValueError('Unknown selected case: ' + ', '.join(sorted(unknown)))
    return result


class Transport:
    def __init__(self, session_path):
        discovered = read(session_path)
        self.path = Path(discovered['outputDir']) / 'session.json'
        self.session = read(self.path)
        self.identity = {k: self.session[k] for k in ('pid', 'startedUtc', 'processStartUtc', 'port', 'outputDir')}
        if any(discovered.get(k) != self.identity[k] for k in self.identity):
            raise ValueError('Discovery/unique session mismatch')
        self.run_token = None

    def call(self, endpoint, method='GET', **query):
        current = read(self.path)
        if any(current.get(k) != v for k, v in self.identity.items()) or current.get('token') != self.session['token']:
            raise RuntimeError('Pinned bridge session changed')
        headers = {'X-Bridge-Token': self.session['token']}
        if self.run_token:
            headers['X-Bridge-Run'] = self.run_token
        url = 'http://127.0.0.1:' + str(self.session['port']) + endpoint + '?' + urllib.parse.urlencode(query)
        request = urllib.request.Request(url, headers=headers, method=method, data=b'' if method == 'POST' else None)
        with urllib.request.urlopen(request, timeout=95) as response:
            result = json.load(response)
        if result.get('ok') is not True:
            raise RuntimeError('Bridge refused ' + endpoint)
        if endpoint == '/ping' and result.get('pid') != self.session['pid']:
            raise RuntimeError('Bridge PID changed')
        return result


class BatchLock:
    def __enter__(self):
        OUT.mkdir(parents=True, exist_ok=True)
        self.file = (OUT / 'active.lock').open('a+b')
        self.file.seek(0)
        self.file.write(b'1')
        self.file.flush()
        self.file.seek(0)
        try:
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(self.file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(self.file, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except Exception:
            self.file.close()
            raise RuntimeError('Another batch runner owns the local lock')
        return self

    def __exit__(self, *args):
        self.file.close()


def last_json(stdout):
    for line in reversed(stdout.splitlines()):
        try:
            value = json.loads(line)
            if isinstance(value, dict):
                return value
        except ValueError:
            pass
    raise ValueError('No structured result from scenario')


def command(case, directory):
    if case['kind'] == 'scenario':
        return ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(HERE / 'gamebridge.ps1'), 'test', str(inside(case['file']))]
    replacements = {'{report}': str(directory / 'driver-result.json'), '{session}': os.environ['REBIRTH_BRIDGE_SESSION']}
    args = [replacements.get(a, a) for a in case['args']]
    return [sys.executable, str(inside(case['file'])), *args]



def require_settled_custody(bridge):
    if bridge.call('/ping').get('state') != 'ingame':
        raise RuntimeError('Cannot qualify actor custody outside in-game state; lease retained')
    owner=bridge.call('/gearobserve')
    if owner.get('cursor',{}).get('count') != 0:
        raise RuntimeError('Native cursor remains occupied; retain exact source evidence and batch ownership')
    for key in ('gearHeld','libraryHeld','resourcePending','creationHeld','offerPending'):
        if owner.get(key) is not False:
            raise RuntimeError('Original actor custody remains pending: '+key)
    cooking=bridge.call('/auditstate',domain='cookingAdmission')['domains']['cookingAdmission']
    if type(cooking.get('clientHeld')) is not int or cooking.get('ownerDeltaHeld') is not False:
        raise RuntimeError('Cooking custody unavailable/held; batch ownership retained')
    if cooking['clientHeld'] and cooking.get('client',{}).get('accepted') is not True:
        raise RuntimeError('Original cooking admission remains unacknowledged; batch ownership retained')
    if cooking.get('client',{}).get('custody',{}).get('unknown') is True or cooking.get('client',{}).get('cancellation',{}).get('pending') is True:
        raise RuntimeError('Original cooking return/cancellation remains uncertain; batch ownership retained')


def run(plan, prepared, session_path, transport_factory=Transport, executor=subprocess.run):
    run_id = time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]
    directory = OUT / run_id
    report_path = directory / 'report.json'
    report = {'schema': 1, 'run': run_id, 'name': plan['name'], 'status': 'RUNNING', 'cases': [], 'startedUtc': time.time()}
    for item in prepared:
        report['cases'].append({'id': item['case']['id'], 'group': item['case'].get('group'), 'scope': item['case'].get('scope'), 'status': item['status'], 'reason': item['reason']})
    write(report_path, report)
    bridge = None
    report['session'] = None
    report['inputs'] = {str(inside(i['case']['file']).relative_to(ROOT)): hashlib.sha256(inside(i['case']['file']).read_bytes()).hexdigest()
                        for i in prepared if i['case']['kind'] != 'manual'}
    acquired = False
    old_env = {key: os.environ.get(key) for key in ('REBIRTH_BRIDGE_SESSION', 'REBIRTH_BRIDGE_RUN_TOKEN')}
    try:
        bridge = transport_factory(session_path)
        report['session'] = bridge.identity
        ping = bridge.call('/ping')
        if ping.get('version') != '1.1.0' or ping.get('state') != 'ingame':
            raise RuntimeError('Requires bridge 1.1.0, already loaded in the disposable test save')
        lease = bridge.call('/batch', 'POST', action='acquire', run=run_id)
        bridge.run_token = lease['runToken']
        acquired = True
        write(directory / 'control.json', {'session': str(bridge.path), 'runToken': bridge.run_token, 'run': run_id})
        # Verify the installed server actually rejects a competing script before
        # any test setup/input. Ping remains available for external status checks.
        own_token = bridge.run_token
        bridge.run_token = None
        try:
            try:
                bridge.call('/state', sections='world')
            except urllib.error.HTTPError as refused:
                if refused.code != 409:
                    raise
                write(directory / 'isolation.json', {'passed': True, 'foreignCommandStatus': 409})
            else:
                raise RuntimeError('Bridge failed exclusive-control probe')
        finally:
            bridge.run_token = own_token
        os.environ['REBIRTH_BRIDGE_SESSION'] = str(bridge.path)
        os.environ['REBIRTH_BRIDGE_RUN_TOKEN'] = bridge.run_token
        world = bridge.call('/state', sections='world')['world']
        if world.get('gameName') != plan['save'] or world.get('gameWorld') != plan['world']:
            raise RuntimeError('Loaded world/save differs from the approved disposable batch save')
        startup_errors = bridge.call('/log', since=0, level='error', limit=1000)
        write(directory / 'startup-errors.json', startup_errors)
        report['startupErrorCount'] = startup_errors.get('count')
        write(directory / 'startup-warnings.json', bridge.call('/log', since=0, level='warning', limit=1000))
        if startup_errors.get('count') != 0:
            raise RuntimeError('Startup errors present; inspect startup-errors.json before gameplay tests')
        bridge.call('/stop', 'POST')
        guard = bridge.call('/guard')
        write(directory / 'guard.json', guard)
        # Set guard ON, never suppress combat instincts to make assertions pass.
        bridge.call('/guard', 'POST', enabled=1)
        healthy = True
        for item, row in zip(prepared, report['cases']):
            if item['status'] != 'READY':
                continue
            if not healthy:
                row.update(status='BLOCKED', reason='Earlier case left uncertain/unsafe session; no automatic restart')
                write(report_path, report)
                continue
            case = item['case']
            evidence = directory / case['id']
            evidence.mkdir()
            row.update(status='RUNNING', startedUtc=time.time(), evidence=str(evidence))
            write(report_path, report)
            start = time.monotonic()
            try:
                before = bridge.call('/ping')
                if before['state'] != 'ingame' or before.get('batch', {}).get('uncertain'):
                    raise RuntimeError('Session not ready or previous action uncertain')
                snapshot = bridge.call('/state', sections='player,stats,world,ui')
                if snapshot.get('world', {}).get('gameName') != plan['save'] or snapshot.get('world', {}).get('gameWorld') != plan['world']:
                    raise RuntimeError('World/save changed during batch')
                write(evidence / 'before.json', snapshot)
                write(evidence / 'surroundings.json', bridge.call('/surroundings', radius=30))
                timeout = case.get('timeoutSeconds', 600)
                # No shell and no launch/build/quit in this runner. One sequential child at a time.
                result = executor(command(case, evidence), cwd=HERE, env=os.environ.copy(), capture_output=True, text=True, timeout=timeout)
                (evidence / 'stdout.txt').write_text(result.stdout or '', encoding='utf-8')
                (evidence / 'stderr.txt').write_text(result.stderr or '', encoding='utf-8')
                parsed = last_json(result.stdout) if case['kind'] == 'scenario' else read(evidence / case['resultFile'])
                write(evidence / 'result.json', parsed)
                errors = bridge.call('/log', since=before['lastLogSeq'], level='error', limit=1000)
                warnings = bridge.call('/log', since=before['lastLogSeq'], level='warning', limit=1000)
                write(evidence / 'errors.json', errors)
                write(evidence / 'warnings.json', warnings)
                expected = case.get('passValue', 'PASS')
                actual = parsed.get(case.get('passField', 'status'))
                passed = parsed.get('passed') is True if case['kind'] == 'scenario' else type(actual) is type(expected) and actual == expected
                if result.returncode != 0 or not passed or errors.get('count') != 0:
                    raise AssertionError('Scenario failed, result lacks passed=true, or new game errors')
                row.update(status='PASS', reason='All case assertions passed; no new error/exception logs', warningCount=warnings.get('count'))
            except subprocess.TimeoutExpired:
                bridge.call('/batch', 'POST', action='hold')
                row.update(status='FAIL', reason='Case deadline exceeded; batch stopped to avoid overlapping late input')
                healthy = False
            except Exception as error:
                row.update(status='FAIL', reason=str(error))
                healthy = False
            finally:
                row['seconds'] = round(time.monotonic() - start, 3)
                try:
                    bridge.call('/stop', 'POST')
                    write(evidence / 'after.json', bridge.call('/state', sections='player,stats,world,ui'))
                    if plan.get('captureScreenshots', True):
                        shot = bridge.call('/screenshot', 'POST', name=run_id + '-' + case['id'])
                        write(evidence / 'screenshot.json', shot)
                except Exception as error:
                    row['captureError'] = str(error)
                    healthy = False
                write(report_path, report)
                print(json.dumps({'case': row['id'], 'status': row['status'], 'seconds': row['seconds']}), flush=True)
        report['status'] = 'FAIL' if any(r['status'] in ('FAIL', 'BLOCKED') for r in report['cases']) else ('PASS_SELECTED' if any(r['status'] == 'PASS' for r in report['cases']) else 'NOT_RUN')
    except BaseException as error:
        report['status'] = 'INTERRUPTED' if isinstance(error, KeyboardInterrupt) else 'FAIL'
        if acquired and isinstance(error, KeyboardInterrupt):
            try:
                bridge.call('/batch', 'POST', action='hold')
            except Exception:
                pass
        report['error'] = str(error)
        for row in report['cases']:
            if row['status'] in ('READY', 'RUNNING'):
                row.update(status='BLOCKED', reason='Batch interrupted before completion')
    finally:
        if acquired:
            try:
                # /stop cancels bridge-owned inputs; does not close windows/drop cursor items.
                bridge.call('/stop', 'POST')
                require_settled_custody(bridge)
                bridge.call('/batch', 'POST', action='release')
                report['leaseReleased'] = True
                (directory / 'control.json').unlink(missing_ok=True)
            except Exception as error:
                report['leaseReleased'] = False
                report['leaseError'] = str(error)
                report['status'] = 'FAIL'
        for key, value in old_env.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        report['finishedUtc'] = time.time()
        write(report_path, report)
    return report_path, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, default=HERE / 'batches' / 'current.json')
    parser.add_argument('--preflight', action='store_true')
    parser.add_argument('--session', type=Path, default=HERE / 'out/session.json')
    parser.add_argument('--case', action='append')
    parser.add_argument('--group', action='append')
    parser.add_argument('--ack', action='append', default=[])
    args = parser.parse_args()
    plan = read(args.plan)
    prepared = prepare(plan, args.case, args.group, args.ack)
    if args.preflight:
        counts = {state: sum(i['status'] == state for i in prepared) for state in ('READY', 'NOT_RUN')}
        print(json.dumps({'preflight': 'PASS', 'cases': len(prepared), **counts}))
        return 0
    if not any(i['status'] == 'READY' for i in prepared):
        raise ValueError('No selected cases with acknowledged prerequisites')
    with BatchLock():
        path, report = run(plan, prepared, args.session)
    print(json.dumps({'status': report['status'], 'report': str(path)}))
    return 0 if report['status'] == 'PASS_SELECTED' else 1


if __name__ == '__main__':
    sys.exit(main())
