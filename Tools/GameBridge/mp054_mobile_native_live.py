"""Actual camper earned lifecycle driver. AUTHORED_NOT_RUN.

PLAN REPORT --session OWNER [--authority SERVER] [--resume PRIOR].
Only real input actions are sent. Activation, save/reload and network barriers
are external prerequisites. No receipt editing, native setters or fake source.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp015_completion_live import strings
from mp010_pull_lifecycle_live import topology, require_peers


def status(runner, identity=None, historical=False, player=None):
    need(topology(runner, player).get('gameName') == 'CodexTest', 'disposable CodexTest required')
    command = 'rbearnedstatus mobile'
    if historical:
        need(identity is not None, 'prior observed original required for history')
        command += ' history ' + identity[1] + ' ' + identity[0]
    response = runner.call('/console', 'POST', cmd=command)
    found = []
    for text in strings(response):
        try:
            value = json.loads(text)
        except (ValueError, TypeError):
            continue
        if isinstance(value, dict) and value.get('role') == 'mobile-native':
            found.append(value)
    need(len(found) == 1, 'one genuine camper audit response required')
    need(found[0].get('enabled') is True and found[0].get('readOnly') is True,
         'activated production and readonly observer required')
    return found[0]


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    owner = Runner(report, Path(args.report), session_path=args.session)
    host = Runner(report, Path(args.report), session_path=args.authority) if args.authority else owner
    authority_player = plan.get('authorityPlayer')
    owner_scope, host_scope = topology(owner), topology(host, authority_player)
    require_peers(owner_scope, host_scope, args.authority is None)
    need(owner.call('/guard')['enabled'] is True, 'native owner instincts remain enabled')
    need(host.call('/guard')['enabled'] is True, 'native authority instincts remain enabled')
    report.update(topology=owner_scope, authorityTopology=host_scope)
    report['topologyEvidence'] = 'HOST_LOCAL' if args.authority is None else ('DEDICATED_REMOTE' if host_scope['dedicated'] else 'LISTEN_REMOTE')
    identity = None
    operation = plan.get('operation')
    initial = status(host, player=authority_player)
    known = {row['operation'] for row in initial['originals']}
    if args.resume:
        prior = json.loads(Path(args.resume).read_text(encoding='utf-8-sig'))
        need(prior.get('identity') and prior.get('authorityTopology'), 'prior real capture required')
        identity = tuple(prior['identity'])
        operation = identity[0]
        for key in ('worldKey',):
            need(host_scope.get(key) == prior['authorityTopology'].get(key), 'saved world changed')
        need(host_scope.get('processIdentity') != prior['authorityTopology'].get('processIdentity') or
             host_scope.get('worldInstance') != prior['authorityTopology'].get('worldInstance'),
             'actual cold authority load required')
    need(plan.get('checkpoints'), 'actual lifecycle checkpoints required')
    for checkpoint in plan['checkpoints']:
        for action in checkpoint.get('actions', []):
            need(action['endpoint'] in ALLOWED, 'real player input only')
            owner.call(action['endpoint'], 'POST', **action.get('args', {}))
            time.sleep(min(5, max(0, action.get('wait', .2))))
        expected = checkpoint.get('expect')
        no_new = checkpoint.get('noNewOriginal') is True
        need(isinstance(expected, dict) and (expected or no_new and checkpoint.get('nativeReads')),
             'exact phase or native refusal-state assertions required')
        deadline = time.monotonic() + min(120, checkpoint.get('timeout', 30))
        while True:
            for runner, baseline, player in ((owner, owner_scope, None), (host, host_scope, authority_player)):
                current = topology(runner, player)
                for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                    need(current.get(key) == baseline.get(key), 'scope changed within invocation: ' + key)
            native_ok = True
            native_reads = []
            for read in checkpoint.get('nativeReads', []):
                need(read['endpoint'] in ('/camper', '/state'), 'native companion reads must be readonly camper/state')
                need(read.get('peer') in ('owner', 'authority'), 'explicit native observation peer required')
                target = owner if read.get('peer') == 'owner' else host
                native = target.call(read['endpoint'], **read.get('args', {}))
                need(read.get('expect'), 'native read requires exact actual fields')
                for path, expected_value in read['expect'].items():
                    observed_value = native
                    for segment in path.split('.'):
                        if isinstance(observed_value, list):
                            need(segment.isdecimal() and int(segment) < len(observed_value), 'missing native list path: ' + path)
                            observed_value = observed_value[int(segment)]
                        else:
                            need(isinstance(observed_value, dict) and segment in observed_value, 'missing native path: ' + path)
                            observed_value = observed_value[segment]
                    native_ok = native_ok and type(observed_value) is type(expected_value) and observed_value == expected_value
                native_reads.append(native)
            observation = status(host, identity, checkpoint.get('historical', False), authority_player)
            report['observations'].append(observation)
            rows = observation['originals']
            if no_new:
                need(not any(row['operation'] not in known for row in rows), 'unexpected new paid original')
                if native_ok:
                    report['checkpoints'].append({'name': checkpoint['name'], 'noNewOriginal': True, 'nativeReads': native_reads})
                    break
                need(time.monotonic() < deadline, 'native refusal checkpoint timeout: ' + checkpoint['name'])
                time.sleep(.2)
                continue
            if operation is None:
                fresh = [row for row in rows if row['operation'] not in known]
                need(len(fresh) <= 1, 'one owning original must be prepared')
                if fresh:
                    operation = fresh[0]['operation']
            matches = [row for row in rows if row['operation'] == operation]
            need(len(matches) <= 1, 'ambiguous camper original')
            if matches:
                row = matches[0]
                need(not row.get('unknown'), 'unknown observer cannot certify lifecycle')
                observed = tuple(row.get(key) for key in ('operation', 'birth', 'digest', 'vehicle', 'recipient'))
                need(all(value is not None for value in observed), 'complete real source identity required')
                if identity is None:
                    identity = observed
                need(observed == identity, 'original, VIN or immutable earned recipient changed')
                report['identity'] = list(identity)
                if native_ok and all(key in row and row[key] == value and type(row[key]) is type(value) for key, value in expected.items()):
                    report['checkpoints'].append({'name': checkpoint['name'], 'row': row, 'nativeReads': native_reads})
                    break
            need(time.monotonic() < deadline, 'camper checkpoint timeout: ' + checkpoint['name'])
            time.sleep(.2)
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--session', required=True)
    parser.add_argument('--authority')
    parser.add_argument('--resume')
    args = parser.parse_args()
    report = {'status': 'FAIL', 'observations': [], 'checkpoints': []}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
