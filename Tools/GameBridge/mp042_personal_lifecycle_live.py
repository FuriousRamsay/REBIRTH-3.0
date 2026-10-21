"""AUTHORED_NOT_RUN. Actual personal crafting native input/original lifecycle.
PLAN REPORT --owner-session FILE --server-session FILE [--local].
External death/reconnect/cold reload is performed outside this driver, then resume
with the exact report resume object. Never launches, builds, injects effects or
disables guard. Only executed checkpoint assertions count as runtime coverage.
"""
import argparse
import json
import re
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers, fingerprint


def sources(runner, player):
    value = runner.call('/auditstate', domain='remoteResourceOriginals', player=player)['domains']['remoteResourceOriginals']
    need(value.get('available') is True and not value.get('truncated'), 'actual authoritative original observation required')
    rows = value['originals']
    need(all(not row.get('unknown') for row in rows), 'unknown original cannot establish payment')
    return rows


def inventory(runner):
    value = runner.call('/auditstate', domain='nativeInventory')['domains']['nativeInventory']
    need(value.get('available') is True and value.get('personalQueueReadable') is True,
         'actual owning native actor and personal queue required')
    need(fingerprint(value.get('actorFingerprint')) and fingerprint(value.get('personalQueueFingerprint')),
         'complete rich owner/queue images required')
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    player = plan.get('serverPlayerId')
    need(type(player) is int and player > 0, 'observed authenticated owner entity required')
    need(isinstance(plan.get('checkpoints'), list) and plan['checkpoints'], 'native UI checkpoints required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own, host = topology(owner), topology(server, player)
    require_peers(own, host, args.local)
    need(owner.call('/guard')['enabled'] is True, 'native guard must remain enabled')
    owner.call('/surroundings')
    original = dict(plan['resume']) if plan.get('resume') else None
    resume_kind = plan.get('resumeKind')
    need(resume_kind in (None, 'death', 'reconnect', 'cold-owner', 'cold-both'), 'unknown resume qualification')
    if original:
        need(re.fullmatch(r'[0-9a-f]{32}', original.get('operation', '')) is not None,
             'resume requires exact source original operation')
        need(fingerprint(original.get('binding')), 'resume requires immutable source binding')
        for key in ('worldKey', 'ownerKey', 'creationId'):
            need(original[key] == own[key], 'resume changed stable identity: ' + key)
    if resume_kind:
        need(original and original.get('ownerTopology') and original.get('serverTopology'), 'saved endpoint provenance required')
        previous, previous_host = original['ownerTopology'], original['serverTopology']
        if resume_kind == 'death':
            need(own['deaths'] > previous['deaths'] and host['deaths'] == own['deaths'], 'actual later native life required')
        elif resume_kind == 'reconnect':
            need(own['worldInstance'] != previous['worldInstance'] or own['processIdentity'] != previous['processIdentity'],
                 'actual changed native owner world/process required')
        else:
            need(own['processIdentity'] != previous['processIdentity'], 'actual new native owner process required')
        if resume_kind == 'cold-both':
            need(host['processIdentity'] != previous_host['processIdentity'], 'actual new native authority process required')
    baseline = {row['operation'] for row in sources(server, player)}
    had_ui = False
    for check in plan['checkpoints']:
        need(isinstance(check.get('name'), str) and check['name'], 'named checkpoint required')
        expected_owner, expected_source = check.get('expectOwner', {}), check.get('expectSource', {})
        need(expected_owner and expected_source, 'both native owner and source assertions required')
        if check.get('actions'):
            owner.guard_idle()
        for action in check.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'only bounded native input permitted')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded press required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict native observed drag required')
            had_ui |= endpoint in ('/ui/click', '/ui/drag', '/activate')
            owner.call(endpoint, 'POST', **values)
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded native action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(180, max(1, float(check.get('timeout', 60))))
        while True:
            current_owner, current_host = topology(owner), topology(server, player)
            require_peers(current_owner, current_host, args.local)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current_owner[key] == own[key] and current_host[key] == host[key],
                     'episode changed; resume exact saved original in a new driver run: ' + key)
            rows, native = sources(server, player), inventory(owner)
            if original is None:
                fresh = [row for row in rows if row['operation'] not in baseline]
                need(len(fresh) <= 1, 'one personal native crafting action per episode required')
                if fresh:
                    need(had_ui and fingerprint(fresh[0].get('binding')), 'fresh original requires native UI and exact binding')
                    original = {key: fresh[0][key] for key in ('operation', 'binding', 'originalDeaths')}
                    original.update({key: own[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                    original['ownerTopology'], original['serverTopology'] = dict(own), dict(host)
            matching = [row for row in rows if original and row['operation'] == original['operation']]
            need(len(matching) <= 1, 'ambiguous authoritative original')
            if matching:
                source = matching[0]
                for key in ('binding', 'originalDeaths'):
                    need(source[key] == original[key], 'immutable original changed: ' + key)
                for key in ('removedFingerprint',):
                    if source.get('phase', 0) >= 2:
                        if key in original:
                            need(source[key] == original[key], 'settled paid payload changed')
                        else:
                            original[key] = source[key]
                report['resume'] = original
                report['observations'].append({'name': check['name'], 'owner': native, 'source': source})
                owner.save()
                if all(native.get(k) == v for k, v in expected_owner.items()) and all(source.get(k) == v for k, v in expected_source.items()):
                    if check.get('terminal'):
                        need(source.get('phase') == 4 and fingerprint(source.get('ownerProof')), 'positive saved terminal proof required')
                        disposition = source.get('disposition')
                        need(disposition in ('used', 'refunded', 'unused-no-payment'), 'personal terminal disposition required')
                        if disposition == 'used':
                            need(source.get('ownerActionDomain') == 'native-craft', 'actual personal native queue action required')
                        if disposition == 'refunded':
                            need(source.get('ownerRefundDomain') == 'repairRefund', 'actual current-life saved return child required')
                        need(native.get('fetchHeld') is False, 'terminal must release original owner hold')
                    report['checkpoints'].append({'name': check['name'], 'owner': native, 'source': source})
                    break
            need(time.monotonic() < deadline, 'actual original observation timed out: ' + check['name'])
            time.sleep(.5)
        if check.get('externalPause'):
            report.update(status='AWAITING_EXTERNAL', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE')
            return
    need(plan['checkpoints'][-1].get('terminal') is True, 'PASS requires explicit saved original terminal')
    owner.errors()
    server.errors()
    report.update(status='PASS', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE',
                  dedicated='OBSERVED' if host.get('dedicated') else 'NOT_RUN',
                  scope='Only executed native input/checkpoint assertions; lifecycle coverage is not inferred')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan');parser.add_argument('report')
    parser.add_argument('--owner-session', required=True);parser.add_argument('--server-session', required=True)
    parser.add_argument('--local', action='store_true')
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
