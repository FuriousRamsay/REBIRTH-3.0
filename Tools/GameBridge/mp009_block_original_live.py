"""AUTHORED_NOT_RUN. Actual block repair/upgrade source, owner and host lifecycle.

Observed native input only. Parent/operator owns genuine death/reconnect/restart
between reports. No original, source, owner, block, receipt or native callback is
fabricated. A paused report is not a terminal acceptance result.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(server, player, operation=None):
    query = {'domain': 'remoteResourceOriginals', 'player': player}
    if operation:
        query['operation'] = operation
    value = server.call('/auditstate', **query)['domains']['remoteResourceOriginals']
    need(value.get('available') is True and value.get('installed') is True, 'actual authoritative source audit required')
    rows = value['originals']
    need(all(not row.get('unknown') for row in rows), 'unknown disk history cannot qualify payment')
    return [row for row in rows if row.get('kind') in (3, 4)]


def at(value, path):
    for key in path:
        value = value[key]
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('topology') in ('listen-remote', 'dedicated-remote'), 'explicit remote topology required')
    need(plan.get('operationKind') in (3, 4), 'actual repair or upgrade original required')
    need(plan.get('transition', 'fresh') in ('fresh', 'death', 'reconnect', 'cold-server', 'cold-both'), 'explicit lifecycle required')
    player = plan.get('serverPlayerId')
    need(type(player) is int and player > 0, 'actual authoritative peer entity required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own, host = topology(owner), topology(server, player)
    require_peers(own, host)
    need(bool(host.get('dedicated')) == (plan['topology'] == 'dedicated-remote'), 'actual dedicated mode differs')
    need(owner.call('/guard')['enabled'] is True, 'guard stays enabled')
    owner.call('/surroundings')
    resume = plan.get('resume')
    if resume:
        original = resume['original']
        for name in ('worldKey', 'ownerKey', 'creationId'):
            need(own[name] == resume['owner'][name] and host[name] == resume['server'][name], 'same original world/owner/creation required')
        transition = plan['transition']
        if transition == 'death':
            need(own['deaths'] > resume['owner']['deaths'], 'actual later native life required')
        elif transition == 'reconnect':
            need(own['processIdentity'] != resume['owner']['processIdentity'] or own['worldInstance'] != resume['owner']['worldInstance'], 'actual new owner session required')
        elif transition in ('cold-server', 'cold-both'):
            need(host['processIdentity'] != resume['server']['processIdentity'], 'genuinely new host process required')
            if transition == 'cold-both':
                need(own['processIdentity'] != resume['owner']['processIdentity'], 'genuinely new owner process required')
        else:
            need(False, 'resume cannot claim a fresh run')
    else:
        need(plan.get('transition', 'fresh') == 'fresh', 'lifecycle transition needs prior original evidence')
        original = None
    baseline = {row['operation'] for row in observe(server, player)}
    checkpoints = plan.get('checkpoints')
    need(isinstance(checkpoints, list) and checkpoints, 'actual native checkpoint sequence required')
    acted = False
    for checkpoint in checkpoints:
        expected = checkpoint.get('expect')
        need(isinstance(expected, list) and expected, 'nonempty actual source/native phase assertions required')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'real native input only')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded real block-use gesture required')
            owner.guard_idle()
            owner.call(endpoint, 'POST', **values)
            acted |= endpoint in ('/press', '/key', '/ui/click')
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded native action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current_owner, current_host = topology(owner), topology(server, player)
            require_peers(current_owner, current_host)
            for name in ('worldKey', 'ownerKey', 'creationId', 'deaths', 'processIdentity', 'worldInstance'):
                need(current_owner[name] == own[name] and current_host[name] == host[name], 'unannounced native transition: ' + name)
            rows = observe(server, player, original['operation'] if original else None)
            if original is None:
                candidates = [row for row in rows if row['operation'] not in baseline and row['kind'] == plan['operationKind']]
                need(len(candidates) <= 1, 'ambiguous actual block original')
                if candidates:
                    original = candidates[0]
            matches = [] if original is None else [row for row in rows if row['operation'] == original['operation']]
            need(len(matches) <= 1, 'ambiguous same original')
            if matches:
                sample = matches[0]
                need(sample['binding'] == original['binding'] and sample['kind'] == plan['operationKind'], 'original source binding changed')
                need(sample['blockOriginal'].get('producerActive') is True, 'actual block original producer inactive')
                prior_sources = {source['id']: source for source in original['sources']}
                for source in sample['sources']:
                    if source['id'] in prior_sources:
                        for name in ('kind', 'beforeFingerprint', 'afterFingerprint', 'paidFingerprint'):
                            need(source[name] == prior_sources[source['id']][name], 'immutable source changed: ' + name)
                if all(at(sample, test['path']) == test['value'] for test in expected):
                    break
            need(time.monotonic() < deadline, 'actual source/native checkpoint did not qualify')
            time.sleep(.25)
        report.setdefault('observations', []).append({'name': checkpoint.get('name'), 'original': sample})
    need(resume or acted, 'fresh original must originate in actual native player input')
    need(original is not None, 'actual original never observed')
    report['resume'] = {'original': sample, 'owner': own, 'server': host}
    if plan.get('externalPause') is True:
        report['result'] = 'PAUSED_FOR_PARENT_NATIVE_LIFECYCLE'
        return
    need(sample['phase'] == 4 and sample['disposition'] in ('used', 'refunded', 'unused-no-payment'), 'saved original terminal required')
    for source in sample['sources']:
        if source['kind'] != 5:
            expected = source['beforeFingerprint'] if source['restored'] == 1 or source['attempted'] == 0 else source['afterFingerprint']
            need(source.get('currentNativeFingerprint') == expected, 'actual current native source differs from its original settled prefix')
    # A server terminal is not evidence that the owner received its terminal
    # reply or released inventory custody. Observe the exact saved owner original
    # and its real runtime hold/pending state without pumping or mutating it.
    owner_operation = sample['blockOriginal'].get('original')
    owner_digest = sample['blockOriginal'].get('originalDigest')
    need(isinstance(owner_operation, str) and len(owner_operation) == 32 and owner_digest,
         'mapped immutable owner original required for release observation')
    deadline = time.monotonic() + 90
    while True:
        current_owner, current_host = topology(owner), topology(server, player)
        require_peers(current_owner, current_host)
        for name in ('worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths', 'processIdentity', 'worldInstance'):
            need(current_owner[name] == own[name] and current_host[name] == host[name], 'terminal release changed native scope: ' + name)
        release = owner.call('/auditstate', domain='remoteBlockOwnerRelease', operation=owner_operation)['domains']['remoteBlockOwnerRelease']
        report['ownerRelease'] = release
        need(not release.get('unknown'), 'uncertain owner terminal cannot certify release')
        if release.get('available') is True:
            need(release['originalDigest'] == owner_digest, 'terminal owner original changed')
            if release['released'] is True:
                need(release['completedOriginal'] is True and release['retainedHold'] is False and release['pendingFinancial'] is False,
                     'saved owner terminal and actual hold release required')
                break
        need(time.monotonic() < deadline, 'owner terminal receipt/hold release did not qualify')
        time.sleep(.25)
    checks = plan.get('finalChecks')
    need(isinstance(checks, list) and len(checks) >= 2, 'native owner inventory/tool and actual target result checks required')
    need(any('inventory' in str(check.get('path', '')).lower() for check in checks), 'actual native inventory result required')
    need(any(check['endpoint'] == '/target' or check.get('purpose') == 'target' and check.get('peer') == 'server' for check in checks),
         'explicit actual target result assertion required')
    for check in checks:
        need(check['endpoint'] in ('/state', '/uitree', '/auditstate', '/target'), 'read-only native final evidence endpoint required')
        endpoint = server if check.get('peer') == 'server' else owner
        need(at(endpoint.call(check['endpoint'], **check.get('args', {})), check['path']) == check['value'], 'actual native owner/target result differs')
    report['terminal'] = sample
    report['result'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
    args = parser.parse_args()
    report = {'scenario': 'mp009-block-original-native', 'status': 'RUNNING', 'requests': []}
    try:
        run(args, report)
    except Exception as error:
        report['result'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
