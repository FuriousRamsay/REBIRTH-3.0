"""AUTHORED_NOT_RUN. Genuine camper removal -> owner prefixes -> saved Backpack.

Uses real input and read-only bridge observations. The operator owns actual
disconnect/restart/relocation between runs; no native save/original is injected.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def at(value, path):
    for key in path:
        value = value[key]
    return value


def audit(peer, player=None):
    args = {'domain': 'removalParcelOriginals'}
    if player is not None:
        args['player'] = player
    value = peer.call('/auditstate', **args)['domains']['removalParcelOriginals']
    need(value.get('available') is True and value.get('installed') is True, 'actual removal producer audit required')
    need(not value.get('unknown') and not value.get('bounded'), 'complete bounded original observation required')
    need(all(row.get('unknown') is False for row in value['originals']), 'unknown original cannot qualify delivery')
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('topology') in ('local', 'listen-remote', 'dedicated-remote'), 'explicit genuine topology required')
    transition = plan.get('transition', 'fresh')
    need(transition in ('fresh', 'reconnect', 'cold-owner', 'cold-server', 'cold-both', 'relocation'), 'explicit lifecycle required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = owner if plan['topology'] == 'local' else Runner(report, Path(args.report), session_path=args.server_session)
    player = plan.get('serverPlayerId')
    if plan['topology'] != 'local':
        need(type(player) is int and player > 0, 'observed authenticated remote actor entity required')
    own, host = topology(owner), topology(server, player)
    if plan['topology'] != 'local':
        require_peers(own, host)
        need(bool(host['dedicated']) == (plan['topology'] == 'dedicated-remote'), 'actual dedicated mode differs')
    else:
        need(own['localActor'] and own['server'] and not own['remote'], 'actual authoritative local primary required')
    need(owner.call('/guard')['enabled'] is True, 'guard remains enabled')
    owner.call('/surroundings')
    def stable():
        current_owner, current_host = topology(owner), topology(server, player)
        for current, initial in ((current_owner, own), (current_host, host)):
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == initial[key], 'native topology/body/life changed within run: ' + key)
        if plan['topology'] != 'local':
            require_peers(current_owner, current_host)
    resume = plan.get('resume')
    original = None
    if resume:
        original = resume['original']
        for name in ('worldKey', 'ownerKey', 'creationId'):
            need(own[name] == resume['owner'][name] and host[name] == resume['server'][name], 'same original world/owner/creation required')
        need(own['deaths'] == resume['owner']['deaths'], 'this required restart case preserves its actual owner life')
        if transition in ('cold-owner', 'cold-both'):
            need(own['processIdentity'] != resume['owner']['processIdentity'], 'genuinely new owner process required')
        if transition in ('cold-server', 'cold-both'):
            need(host['processIdentity'] != resume['server']['processIdentity'], 'genuinely new host process required')
        if transition == 'reconnect':
            need(own['worldInstance'] != resume['owner']['worldInstance'] or own['processIdentity'] != resume['owner']['processIdentity'], 'genuine new owner session required')
        need(transition != 'fresh', 'resume cannot claim fresh removal')
    else:
        need(transition == 'fresh', 'lifecycle transition requires previous real original evidence')
    baseline = {row['operation'] for row in audit(server, player)['originals']}
    checkpoints = plan.get('checkpoints')
    need(isinstance(checkpoints, list) and checkpoints, 'nonempty native checkpoint sequence required')
    acted = False
    for checkpoint in checkpoints:
        expected = checkpoint.get('expect')
        need(isinstance(expected, list) and expected, 'explicit native owner/source checkpoint assertions required')
        for action in checkpoint.get('actions', []):
            need(action['endpoint'] in ALLOWED, 'only established real-input bridge actions permitted')
            owner.call(action['endpoint'], **action.get('args', {}))
            stable()
            acted = True
        deadline = time.monotonic() + checkpoint.get('timeoutSeconds', 60)
        while True:
            stable()
            host_audit, owner_audit = audit(server, player), audit(owner)
            candidates = host_audit['originals']
            if original is None:
                candidates = [row for row in candidates if row['operation'] not in baseline]
                need(len(candidates) <= 1, 'ambiguous new source removal originals')
            else:
                candidates = [row for row in candidates if row['operation'] == original['operation']]
            if candidates:
                sample = candidates[0]
                if original is None:
                    original = sample
                for name in ('binding', 'source', 'removal', 'vehicle', 'installation', 'owner', 'creation', 'deaths', 'payloadFingerprint'):
                    need(sample[name] == original[name], 'immutable removal changed: ' + name)
                owner_rows = [row for row in owner_audit['originals'] if row['operation'] == sample['operation']]
                value = {'host': sample, 'owner': owner_rows[0] if owner_rows else {}, 'ownerHeld': owner_audit['localOwnerHeld']}
                if all(at(value, test['path']) == test['value'] for test in expected):
                    break
            need(time.monotonic() < deadline, 'actual saved removal/native checkpoint did not qualify')
            time.sleep(.25)
        report.setdefault('observations', []).append({'name': checkpoint.get('name'), 'state': value})
    need(resume or acted, 'fresh removal must originate in actual native player input')
    report['resume'] = {'original': sample, 'owner': own, 'server': host}
    if plan.get('externalPause') is True:
        report['result'] = 'PAUSED_FOR_ACTUAL_NATIVE_LIFECYCLE'
        return
    need(value['host']['originalStage'] == 2 and value['host']['savedOverflow'] is True, 'positive saved original/overflow terminal required')
    need(value['owner'].get('ownerOriginalStage') == 2 and value['owner'].get('savedOwnerPhase') == 1000, 'actual saved envelope retirement/local original terminal required')
    need(value['owner'].get('savedRetiredFrame') is True and value['owner'].get('savedOwnerOriginalLife') is True, 'full original-life native retired owner/carrier frame required')
    need(value['ownerHeld'] is False, 'actual financial owner hold release required')
    checks = plan.get('finalChecks')
    need(isinstance(checks, list) and len(checks) >= 2, 'actual native inventory and overflow/carrier final checks required')
    need(any('inventory' in str(check.get('path', '')).lower() for check in checks), 'native owner inventory result required')
    if value['host']['overflowStacks'] > 0:
        need(value['host'].get('nativeOverflowBagMatches') is True and value['host'].get('nativeOverflowBagFingerprint') == value['host'].get('expectedOverflowBagFingerprint'), 'actual current native Backpack contents must match the source-bound saved overflow')
        need(any(check['endpoint'] in ('/entities', '/state', '/auditstate') and check.get('provesOverflowContents') is True for check in checks), 'actual saved Backpack contents evidence required')
    for check in checks:
        need(check['endpoint'] in ('/state', '/uitree', '/auditstate', '/entities', '/target'), 'read-only native final endpoint required')
        peer = server if check.get('peer') == 'server' else owner
        need(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']) == check['value'], 'actual owner/overflow/carrier result differs')
        stable()
    report['terminal'] = value
    report['result'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session')
    args = parser.parse_args()
    report = {'scenario': 'mp073-removal-parcel-native', 'status': 'RUNNING', 'requests': []}
    try:
        run(args, report)
    except Exception as error:
        report['result'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
