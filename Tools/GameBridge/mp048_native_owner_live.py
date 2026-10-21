"""AUTHORED_NOT_RUN. Read-only actual native trade checkpoint observation.
Does not generate a trade, interruption, connection, callback, receipt or save.
An external authorized real trade/rejoin must establish each requested checkpoint.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from mp010_pull_lifecycle_live import topology


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    operation, digest = plan['operation'], plan['original']
    need(len(operation) == 32 and len(digest) == 64, 'explicit immutable operation/digest required')
    need(plan.get('checkpoints'), 'named actual native checkpoints required')
    owner = Runner(report, Path(args.report), session_path=args.session)
    initial = topology(owner)
    if args.local:
        need(initial.get('remote') is False and initial.get('server') is True and initial.get('localActor') is True, 'actual authoritative local primary required')
    else:
        need(initial.get('remote') is True and initial.get('localActor') is True and initial.get('connected') is True, 'actual connected remote owner required')
    resume = plan.get('resumeKind')
    need(resume in (None, 'reconnect', 'cold-owner'), 'unsupported native resume qualification')
    if resume:
        previous = plan.get('previousTopology')
        need(isinstance(previous, dict) and previous.get('processIdentity') and previous.get('worldInstance'), 'actual prior endpoint evidence required')
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(previous[key] == initial[key], 'native resume changed immutable original: ' + key)
        if resume == 'cold-owner':
            need(previous['processIdentity'] != initial['processIdentity'], 'cold owner requires actual changed native process')
        else:
            need(previous['processIdentity'] != initial['processIdentity'] or previous['worldInstance'] != initial['worldInstance'], 'reconnect requires actual replaced native world/process')
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    report['scope'] = initial
    report['topologyEvidence'] = 'HOST_LOCAL' if args.local else 'REMOTE_OWNER_ONLY'
    for checkpoint in plan['checkpoints']:
        expected = checkpoint.get('expect', {})
        need(checkpoint.get('name') and expected, 'explicit named saved owner checkpoint required')
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current = topology(owner)
            for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
                need(current[key] == initial[key], 'immutable owner scope changed: ' + key)
            state = owner.call('/auditstate', domain='npcTradeOriginals')['domains']['npcTradeOriginals']
            need(state.get('schema') == 1 and state.get('available') is True, 'actual native trade audit required')
            if args.local:
                need(state.get('topology') == 'local', 'actual local original audit required')
            holds = state['owner']['holds']
            rows = [row for row in holds if row['operation'] == operation]
            need(len(rows) <= 1, 'one original owner hold required')
            matched = False
            if rows:
                row = rows[0]
                need(row['original'] == digest, 'immutable trade original changed')
                matched = all(row.get(key) == value for key, value in expected.items())
                if checkpoint.get('saved'):
                    need(expected.get('saved') is True and 'step' in expected and 'phase' in expected, 'explicit exact independently saved phase/step required')
                    matched = matched and row.get('saved') is True and row.get('prefix') is True
            if checkpoint.get('released'):
                need(expected == {'held': False}, 'release observation requires explicit hold absence')
                matched = not rows and not state['owner']['held'] and not any(row['operation'] == operation for row in state['attempts'])
            if checkpoint.get('nativeLoad'):
                native = state['nativeJoin']
                matched = matched and native.get('current') is True and native.get('operation') == operation
                if args.local:
                    matched = matched and native.get('kind') == 'local-native-load' and native.get('hydrated') is True and native.get('stage') is True
            if matched:
                report['checkpoints'].append(dict(name=checkpoint['name'], observation=state, topology=current))
                owner.save()
                break
            need(time.monotonic() < deadline, 'native trade checkpoint pending: ' + checkpoint['name'])
            time.sleep(.2)
    report['status'] = 'PASS_OBSERVATIONS_ONLY'
    report['limitations'] = 'Checkpoint snapshots do not prove interruption provenance, host stock conservation, packet authority or a complete remote trade run.'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', required=True)
    parser.add_argument('--session', required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--local', action='store_true', help='Observe actual host-local primary; never remote evidence')
    args = parser.parse_args()
    report = {'finding': 'MP048', 'status': 'NOT_RUN', 'checkpoints': []}
    try:
        run(args, report)
    except Exception as error:
        report['status'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
