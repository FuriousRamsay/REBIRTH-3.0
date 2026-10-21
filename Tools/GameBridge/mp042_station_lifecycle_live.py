"""AUTHORED_NOT_RUN. Paired real cooking cancellation/hidden-output lifecycle.
PLAN REPORT --owner-session FILE --server-session FILE [--local]. Source originals
come from read-only cookingAdmission.protocolOriginals. No launch, setup cheats,
fault injection, callbacks, setters or receipt creation. An external actor owns
death/reconnect/cold transitions; resume proves their actual endpoint provenance.
"""
import argparse
import json
import re
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers, fingerprint


def sources(runner, player, domain, action=None):
    value = runner.call('/auditstate', domain='cookingAdmission', player=player)['domains']['cookingAdmission']['protocolOriginals']
    need(value.get('available') is True and value.get('schema') == 1 and not value.get('truncated'), 'bounded actual cooking originals required')
    return [row for row in value['originals'] if row['domain'] == domain and (action is None or row.get('action') == action)]


def owning(runner):
    value = runner.call('/auditstate', domain='nativeInventory')['domains']['nativeInventory']
    need(value.get('available') is True and fingerprint(value.get('actorFingerprint')), 'actual owning native inventory required')
    admission = runner.call('/auditstate', domain='cookingAdmission')['domains']['cookingAdmission']
    value['cookingOwnerHeld'] = admission['ownerDeltaHeld']
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    domain, player = plan.get('domain'), plan.get('serverPlayerId')
    need(domain in ('cancellation', 'hidden-output', 'grid-return', 'grid-custody'), 'named actual cooking original domain required')
    action = plan.get('action')
    need(domain != 'grid-custody' or action in ('source-return', 'return'), 'grid-custody cohort requires actual source-return or owner-return child')
    need(type(player) is int and player > 0 and plan.get('checkpoints'), 'authenticated owner and native checkpoints required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own, host = topology(owner), topology(server, player)
    require_peers(own, host, args.local)
    need(owner.call('/guard')['enabled'] is True, 'guard must remain enabled')
    owner.call('/surroundings')
    original = dict(plan['resume']) if plan.get('resume') else None
    resume = plan.get('resumeKind')
    need(resume in (None, 'death', 'reconnect', 'cold-owner', 'cold-both'), 'unknown resume qualification')
    if original:
        need(original['domain'] == domain and re.fullmatch(r'[0-9a-f]{32}', original['operation']) and fingerprint(original['originalFingerprint']), 'exact original resume identity required')
        for key in ('worldKey', 'ownerKey', 'creationId'):
            need(original[key] == own[key], 'stable original scope changed: ' + key)
    if resume:
        need(original and original.get('ownerTopology') and original.get('serverTopology'), 'actual saved topology provenance required')
        prior, prior_host = original['ownerTopology'], original['serverTopology']
        if resume == 'death':
            need(own['deaths'] > prior['deaths'] and own['deaths'] == host['deaths'], 'actual later native life required')
        elif resume == 'reconnect':
            need(own['worldInstance'] != prior['worldInstance'] or own['processIdentity'] != prior['processIdentity'], 'actual owner connection/world replacement required')
        else:
            need(own['processIdentity'] != prior['processIdentity'], 'actual new owner process required')
        if resume == 'cold-both':
            need(host['processIdentity'] != prior_host['processIdentity'], 'actual new authority process required')
    baseline = {row['operation'] for row in sources(server, player, domain, action)}
    had_ui = False
    for check in plan['checkpoints']:
        need(check.get('name') and check.get('expectOwner') and check.get('expectSource'), 'named native owner/source assertions required')
        if check.get('actions'):
            owner.guard_idle()
        for action in check.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'only bounded actual native input permitted')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native action required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native drag required')
            had_ui |= endpoint in ('/ui/click', '/ui/drag', '/activate')
            owner.call(endpoint, 'POST', **values)
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(180, max(1, float(check.get('timeout', 60))))
        while True:
            current, authority = topology(owner), topology(server, player)
            require_peers(current, authority, args.local)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == own[key] and authority[key] == host[key], 'episode changed; resume the exact saved original: ' + key)
            rows, native = sources(server, player, domain, action), owning(owner)
            if original is None:
                fresh = [row for row in rows if row['operation'] not in baseline]
                need(len(fresh) <= 1, 'one actual native cooking original per episode required')
                if fresh:
                    need(had_ui, 'fresh original requires actual native UI action')
                    original = {key: fresh[0][key] for key in ('domain', 'operation', 'originalFingerprint', 'parent', 'location', 'birth', 'token')}
                    original.update({key: own[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                    original['ownerTopology'], original['serverTopology'] = dict(own), dict(host)
                    original['ownerDeltas'] = {}
            matches = [row for row in rows if original and row['operation'] == original['operation']]
            need(len(matches) <= 1, 'ambiguous actual original')
            if matches:
                source = matches[0]
                for key in ('originalFingerprint', 'parent', 'location', 'birth', 'token'):
                    need(source[key] == original[key], 'immutable original changed: ' + key)
                if source.get('delta'):
                    need(fingerprint(source.get('deltaFingerprint')), 'exact owner child fingerprint required')
                    saved = original['ownerDeltas'].setdefault(source['delta'], source['deltaFingerprint'])
                    need(saved == source['deltaFingerprint'], 'owner child original changed')
                report['resume'] = original
                report['observations'].append({'name': check['name'], 'owner': native, 'source': source})
                owner.save()
                if all(native.get(key) == value for key, value in check['expectOwner'].items()) and all(source.get(key) == value for key, value in check['expectSource'].items()):
                    if check.get('terminal'):
                        need(source['phase'] == 'Applied' and native['cookingOwnerHeld'] is False, 'saved original terminal and released owner custody required')
                        if domain in ('cancellation', 'hidden-output'):
                            need(source.get('birthMatches') is True and source.get('sourceMatchesAfter') is True and source.get('deltaPhase') == 'Applied',
                                 'exact original station incarnation, saved source after and applied owner child required')
                            need(source.get('ownerSaved2') is True or source.get('ownerSaved3') is True or own['deaths'] > source['deltaDeaths'],
                                 'same-life saved owner phase or previously Applied historical owner child required')
                        if domain == 'grid-custody':
                            if action == 'source-return':
                                need(source.get('sourceReturn') is True and source.get('depositPublished') is True,
                                     'saved named original source deposit required before grid-child terminal')
                            else:
                                need(source.get('ownerReceiptSaved2') is True and source.get('overflowSaved') is True,
                                     'saved original owner credit and every native overflow publication required before grid-child terminal')
                    report['checkpoints'].append({'name': check['name'], 'owner': native, 'source': source})
                    break
            need(time.monotonic() < deadline, 'actual cooking original checkpoint timed out: ' + check['name'])
            time.sleep(.5)
        if check.get('externalPause'):
            report.update(status='AWAITING_EXTERNAL', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE')
            return
    need(plan['checkpoints'][-1].get('terminal') is True, 'PASS requires explicit original terminal')
    owner.errors()
    server.errors()
    report.update(status='PASS', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE', dedicated='OBSERVED' if host.get('dedicated') else 'NOT_RUN',
                  scope='Only executed native input/checkpoint assertions; fault, lifecycle and topology coverage is not inferred')


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
