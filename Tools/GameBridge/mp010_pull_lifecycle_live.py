"""AUTHORED_NOT_RUN. Cooking pull, native input and original saved evidence.

PLAN REPORT --owner-session FILE --server-session FILE. Sessions must be distinct
actual processes in CodexTest. Plans supply observed selectors and nonempty phase
assertions, never setter/callback/source-withdrawal commands. External reconnect or
cold reload is parent-owned: end with externalPause and resume with report evidence.
No launch/build/restart, fault injection, simulated receipt, guard disable or refund.
With --local, both session files must resolve to the same actual host-local
primary/source process. That run is HOST_LOCAL and cannot certify remote topology.
"""
import argparse
import json
import re
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED


def fingerprint(value):
    return isinstance(value, str) and re.fullmatch(r'[0-9a-f]{64}', value) is not None


def topology(runner, player=None):
    args = {'domain': 'topology'}
    if player is not None:
        args['player'] = player
    result = runner.call('/auditstate', **args)['domains']['topology']
    need(result.get('schema') == 1 and result.get('gameName') == 'CodexTest', 'actual disposable CodexTest required')
    need(result.get('worldKey') and result.get('ownerKey') and result.get('creationId'), 'actual stable owner/world/creation required')
    return result


def require_peers(owner, server, local=False):
    if local:
        need(owner['remote'] is False and owner['server'] is True and owner['localActor'] is True,
             'actual host-local owning primary required')
        need(server['remote'] is False and server['server'] is True and server['localActor'] is True,
             'same actual authoritative local owner required')
        for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId'):
            need(owner[key] == server[key], 'local source/owner scope differs: ' + key)
        return
    need(owner['remote'] is True and owner['server'] is False and owner['localActor'] is True and owner['connected'] is True,
         'actual connected owning remote client required')
    need(server['server'] is True and server['remote'] is False and server['authoritativePeer'] is True,
         'actual authenticated server-side remote actor required')
    need(owner['processIdentity'] != server['processIdentity'], 'listen local control cannot certify genuine remote')
    for key in ('worldKey', 'ownerKey', 'creationId', 'entityId'):
        need(owner[key] == server[key], 'current owner/server scope differs: ' + key)


def observe_owner(runner, operation=None):
    args = {'domain': 'cookingPull'}
    if operation:
        args['operation'] = operation
    result = runner.call('/auditstate', **args)['domains']['cookingPull']
    need(result.get('available') is True and result.get('schema') == 1 and not result.get('truncated'),
         'bounded actual cooking pull evidence unavailable')
    return result['records']


def observe_source(runner, player, operation):
    result = runner.call('/auditstate', domain='remoteResourceOriginals', player=player, operation=operation)['domains']['remoteResourceOriginals']
    need(result.get('available') is True, 'actual source-original observer unavailable')
    rows = [row for row in result['originals'] if row['operation'] == operation]
    need(len(rows) <= 1, 'ambiguous authoritative source original')
    if not rows:
        return None
    need(not rows[0].get('unknown'), 'unreadable original is not a settled source')
    return rows[0]


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0, 'observed authenticated owner entity required')
    need(isinstance(plan.get('checkpoints'), list) and plan['checkpoints'], 'real native UI checkpoints required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own_top, host_top = topology(owner), topology(server, plan['serverPlayerId'])
    require_peers(own_top, host_top, args.local)
    need(owner.call('/guard')['enabled'] is True, 'owner guard must remain enabled')
    owner.call('/surroundings')
    prior = plan.get('resume')
    if prior:
        need(isinstance(prior, dict) and re.fullmatch(r'[0-9a-f]{32}', prior.get('operation', '')), 'resume requires exact original operation')
        for field in ('requestFingerprint', 'needsFingerprint'):
            need(fingerprint(prior.get(field)), 'resume requires original ' + field)
        need(type(prior.get('request')) is int and prior['request'] > 0, 'resume requires original request identity')
        for field in ('worldKey', 'ownerKey', 'creationId'):
            need(prior.get(field) == own_top[field], 'resume cannot change stable original ' + field)
    original = dict(prior) if prior else None
    resume_kind = plan.get('resumeKind')
    need(resume_kind in (None, 'death', 'reconnect', 'cold-owner', 'cold-both'), 'unknown native cooking resume qualification')
    if resume_kind:
        need(original is not None and original.get('ownerTopology') and original.get('serverTopology'),
             'resume qualification requires saved actual endpoint topology')
        previous_owner, previous_server = original['ownerTopology'], original['serverTopology']
        if resume_kind == 'death':
            need(own_top['deaths'] > previous_owner['deaths'] and host_top['deaths'] == own_top['deaths'],
                 'historical refund requires an actual later native owner life')
        elif resume_kind == 'reconnect':
            need(own_top['worldInstance'] != previous_owner['worldInstance'] or own_top['processIdentity'] != previous_owner['processIdentity'],
                 'reconnect requires an actual changed native owner world or process')
        else:
            need(own_top['processIdentity'] != previous_owner['processIdentity'], 'cold owner requires a new actual native process')
        if resume_kind == 'cold-both':
            need(host_top['processIdentity'] != previous_server['processIdentity'], 'cold authority requires a new actual native process')
    baseline = {row['operation'] for row in observe_owner(owner)}
    had_ui_input = False
    for checkpoint in plan['checkpoints']:
        need(isinstance(checkpoint.get('name'), str) and checkpoint['name'], 'named checkpoint required')
        expected = checkpoint.get('expectOwner', {})
        source_expected = checkpoint.get('expectSource', {})
        need(expected or source_expected, 'nonempty actual state assertions required')
        actions = checkpoint.get('actions', [])
        if actions:
            owner.guard_idle()
        for action in actions:
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED, 'only native input actions permitted')
            need(not values.get('hold'), 'unbounded held input refused')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native action required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native drag required')
            had_ui_input |= endpoint in ('/ui/click', '/ui/drag', '/activate')
            owner.call(endpoint, 'POST', **values)
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded native action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(180, max(1, float(checkpoint.get('timeout', 60))))
        while True:
            current_owner, current_server = topology(owner), topology(server, plan['serverPlayerId'])
            require_peers(current_owner, current_server, args.local)
            for key in ('processIdentity', 'worldKey', 'ownerKey', 'creationId', 'entityId'):
                need(current_owner[key] == own_top[key] and current_server[key] == host_top[key],
                     'episode changed; resume the saved original in a new driver run: ' + key)
            records = observe_owner(owner, original['operation'] if original else None)
            if original is None:
                fresh = [row for row in records if row['operation'] not in baseline]
                need(len(fresh) <= 1, 'exactly one new native cooking pull must be prepared')
                if fresh:
                    need(had_ui_input, 'fresh original must come from real native cooking UI')
                    original = {key: fresh[0][key] for key in ('operation', 'request', 'requestFingerprint', 'needsFingerprint')}
                    original.update({key: own_top[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                    original['ownerTopology'], original['serverTopology'] = dict(own_top), dict(host_top)
            matches = [row for row in records if original and row['operation'] == original['operation']]
            need(len(matches) <= 1, 'ambiguous local original')
            if matches:
                row = matches[0]
                need(row.get('readable') is True, 'local journal unavailable')
                for key in ('request', 'requestFingerprint', 'needsFingerprint'):
                    need(row[key] == original[key], 'immutable local original changed: ' + key)
                for key in ('resultFingerprint', 'sourceOperation', 'sourceBinding', 'sourceWorld', 'itemsFingerprint'):
                    if row.get(key):
                        if key in original:
                            need(row[key] == original[key], 'immutable result/source changed: ' + key)
                        else:
                            original[key] = row[key]
                source = observe_source(server, plan['serverPlayerId'], row['sourceOperation']) if row.get('sourceOperation') else None
                if source:
                    need(source['binding'] == row['sourceBinding'], 'owner result uses another source binding')
                    if not row.get('noPayment'):
                        need(source['removedFingerprint'] == row['itemsFingerprint'], 'canonical removed/result payload differs')
                report['observations'].append({'name': checkpoint['name'], 'owner': row, 'source': source,
                                               'ownerTopology': current_owner, 'serverTopology': current_server})
                report['resume'] = original
                owner.save()
                owner_ok = all(row.get(field) == value for field, value in expected.items())
                source_ok = not source_expected or source is not None and all(source.get(field) == value for field, value in source_expected.items())
                if owner_ok and source_ok:
                    if checkpoint.get('terminal'):
                        need(row.get('localApplied') is True and row.get('waiting') is False and source is not None and source.get('phase') == 4,
                             'terminal requires both local original acknowledgements and actual authoritative source terminal')
                        need(fingerprint(source.get('ownerProof')) and source.get('disposition') in ('grid-owned', 'refunded', 'unused-no-payment'),
                             'terminal requires positive original owner or no-payment proof')
                    report['checkpoints'].append({'name': checkpoint['name'], 'owner': row, 'source': source})
                    break
            need(time.monotonic() < deadline, 'original phase observation timed out: ' + checkpoint['name'])
            time.sleep(.5)
        if checkpoint.get('externalPause'):
            report.update(status='AWAITING_EXTERNAL', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE', dedicated='OBSERVED' if host_top.get('dedicated') else 'NOT_RUN')
            return
    need(plan['checkpoints'][-1].get('terminal') is True, 'PASS requires an explicitly asserted final original terminal')
    owner.errors()
    server.errors()
    report.update(status='PASS', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE', dedicated='OBSERVED' if host_top.get('dedicated') else 'NOT_RUN',
                  scope='Only executed checkpoint assertions; fault/death/cold coverage is not inferred')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan');parser.add_argument('report')
    parser.add_argument('--owner-session', required=True);parser.add_argument('--server-session', required=True)
    parser.add_argument('--local', action='store_true', help='Qualify actual host-local primary/source through the same native-input/original evidence; never counts as genuine remote')
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
