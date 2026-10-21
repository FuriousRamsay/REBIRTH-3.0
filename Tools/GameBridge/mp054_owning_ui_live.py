"""Deferred actual owning-card MP054 phase driver; AUTHORED_NOT_RUN.

PLAN REPORT --session VIEWER [--authority SERVER]. Never activates the feature, launches, builds,
edits receipts or invokes native setters. PLAN checkpoints contain real Bridge
input actions and exact Boolean phase assertions against the retained original.
Use separate genuine save/network barriers to observe intermediate phases.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp015_completion_live import strings
from mp010_pull_lifecycle_live import topology, require_peers


def status(runner):
    need(topology(runner).get('gameName') == 'CodexTest', 'actual disposable CodexTest required')
    response = runner.call('/console', 'POST', cmd='rbearnedstatus ui')
    candidates = []
    for text in strings(response):
        try:
            value = json.loads(text)
        except (ValueError, TypeError):
            continue
        if isinstance(value, dict) and value.get('role') == 'owning-ui':
            candidates.append(value)
    need(len(candidates) == 1, 'one actual owning UI audit response required')
    value = candidates[0]
    need(value['enabled'] is True and value['readOnly'] is True,
         'production activation and actual read-only observer required')
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    runner = Runner(report, Path(args.report), session_path=args.session)
    authority = Runner(report, Path(args.report), session_path=args.authority) if args.authority else runner
    authority_player = plan.get('authorityPlayer')
    owner_scope, host_scope = topology(runner), topology(authority, authority_player)
    require_peers(owner_scope, host_scope, args.authority is None)
    need(runner.call('/guard')['enabled'] is True, 'native owner instincts remain enabled')
    need(authority.call('/guard')['enabled'] is True, 'native authority instincts remain enabled')
    report.update(topology=owner_scope, authorityTopology=host_scope)
    report['topologyEvidence'] = 'HOST_LOCAL' if args.authority is None else ('DEDICATED_REMOTE' if host_scope['dedicated'] else 'LISTEN_REMOTE')
    report['evidenceScope'] = 'actual-owning-ui-phase-observations'
    initial = status(runner)
    known = {row['operation'] for row in initial['originals']}
    operation = plan.get('operation')
    identity = None
    need(plan.get('checkpoints'), 'actual input/phase checkpoints required')
    for checkpoint in plan['checkpoints']:
        for action in checkpoint.get('actions', []):
            need(runner.call('/guard')['enabled'] is True, 'native owner instincts remain enabled before input')
            need(action['endpoint'] in ALLOWED, 'only real native input actions allowed')
            runner.call(action['endpoint'], 'POST', **action.get('args', {}))
            time.sleep(min(5, max(0, action.get('wait', .2))))
        expected = checkpoint['expect']
        need(expected and all(isinstance(value, bool) for value in expected.values()),
             'exact actual Boolean phases required')
        deadline = time.monotonic() + min(120, checkpoint.get('timeout', 30))
        while True:
            for peer, baseline, player in ((runner, owner_scope, None), (authority, host_scope, authority_player)):
                current = topology(peer, player)
                for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                    need(current.get(key) == baseline.get(key), 'native scope changed within invocation: ' + key)
                need(peer.call('/guard')['enabled'] is True, 'native instincts remain enabled')
            observation = status(runner)
            report['observations'].append(observation)
            rows = observation['originals']
            if operation is None:
                fresh = [row for row in rows if row['operation'] not in known]
                need(len(fresh) <= 1, 'prepare exactly one owning application')
                if fresh:
                    operation = fresh[0]['operation']
            matches = [row for row in rows if row['operation'] == operation]
            need(len(matches) <= 1, 'ambiguous actual original')
            if matches:
                row = matches[0]
                need(not row.get('unknown'), 'uncertain observer cannot qualify recovery')
                current_identity = (row['operation'], row['birth'])
                if identity is None:
                    identity = current_identity
                need(identity == current_identity, 'owning original replaced')
                if all(row.get(field) is value for field, value in expected.items()):
                    report['checkpoints'].append({'name': checkpoint['name'], 'row': row})
                    break
            need(time.monotonic() < deadline, 'actual phase checkpoint timed out: ' + checkpoint['name'])
            time.sleep(.2)
    report['operation'] = operation
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--session', required=True)
    parser.add_argument('--authority')
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
