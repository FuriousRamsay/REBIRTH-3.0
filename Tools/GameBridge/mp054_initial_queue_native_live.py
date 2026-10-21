"""AUTHORED_NOT_RUN. Actual ordinary queue insertion -> native save -> cold native load."""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers
from mp015_completion_live import strings


def observe(peer):
    values = []
    for text in strings(peer.call('/console', 'POST', cmd='rbearnedstatus initial')):
        try:
            value = json.loads(text)
        except (ValueError, TypeError):
            continue
        if isinstance(value, dict) and value.get('role') == 'initial-queue':
            values.append(value)
    need(len(values) == 1 and values[0].get('readOnly') is True and values[0].get('enabled') is True, 'actual installed independent initial source observer required')
    return values[0]['originals']


def at(value, path):
    for key in path:
        value = value[key]
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('topology') in ('local', 'listen-remote', 'dedicated-remote'), 'explicit actual topology required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    host = owner if plan['topology'] == 'local' else Runner(report, Path(args.report), session_path=args.server_session)
    actor = plan.get('serverPlayerId')
    if plan['topology'] != 'local':
        need(type(actor) is int and actor > 0, 'actual observed authority actor required')
    scopes = [topology(owner), topology(host, actor)]
    if plan['topology'] == 'local':
        need(scopes[0]['localActor'] and scopes[0]['server'] and not scopes[0]['remote'], 'actual local primary required')
    else:
        require_peers(*scopes)
        need(bool(scopes[1]['dedicated']) == (plan['topology'] == 'dedicated-remote'), 'actual server topology differs')
    report['topology'] = scopes
    prior = json.loads(Path(args.resume).read_text(encoding='utf-8-sig')) if args.resume else None
    original = prior.get('original') if prior else None
    if prior:
        for current, previous in zip(scopes, prior['topology']):
            for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
                need(current[key] == previous[key], 'same genuine original scope required')
        need(scopes[1]['processIdentity'] != prior['topology'][1]['processIdentity'], 'genuinely new authority process required for native load')
    known = {row['operation'] for row in observe(host)}
    owner.call('/surroundings')
    need(owner.call('/guard')['enabled'] is True, 'guard remains enabled')
    for action in plan.get('actions', []):
        for current, initial in zip([topology(owner), topology(host, actor)], scopes):
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == initial[key], 'actual scope changed before native action')
        need(action['endpoint'] in ALLOWED, 'real owning UI/game input required')
        owner.call(action['endpoint'], 'POST', **action.get('args', {}))
    deadline = time.monotonic() + min(180, plan.get('timeout', 60))
    while True:
        for current, initial in zip([topology(owner), topology(host, actor)], scopes):
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == initial[key], 'actual original scope changed within invocation')
        rows = observe(host)
        candidates = [row for row in rows if row['operation'] == original['operation']] if original else [row for row in rows if row['operation'] not in known]
        need(len(candidates) <= 1, 'prepare one actual queue insertion at a time')
        if candidates:
            sample = candidates[0]
            need(not sample.get('unknown'), 'unknown native origin cannot qualify')
            if original:
                for key in ('operation', 'birth', 'owner', 'creation', 'deaths', 'rowBinding', 'originalDigest'):
                    need(sample[key] == original[key], 'immutable initial insertion source changed')
            if sample.get('saved') is True and sample.get('nativeHeld') is False and (not prior or sample.get('loaded') is True):
                report['original'] = sample
                break
        need(time.monotonic() < deadline, 'actual original saved/native-loaded source timeout')
        time.sleep(.2)
    checks = plan.get('nativeChecks')
    need(isinstance(checks, list) and checks, 'actual queue/inventory/owner native assertions required')
    for check in checks:
        need(check['endpoint'] in ('/state', '/uitree', '/target', '/auditstate'), 'read-only native observation required')
        peer = host if check.get('peer') == 'server' else owner
        need(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']) == check['value'], 'actual initial queue native result differs')
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session')
    parser.add_argument('--resume')
    args = parser.parse_args()
    report = {'status': 'FAIL'}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
