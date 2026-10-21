"""AUTHORED_NOT_RUN. Actual Survivor placed nearby-resource source4 lifecycle.

The operator supplies observed native UI selectors and owns genuine restarts.
No launch/build/receipt/source/queue/native-file injection or direct helper call.
Fresh mode saves a resumable original after actual native owner identity publication.
Cold mode requires a changed authority process and actual initial native load plus
same-original owner lookup; final native output/skill checks are mandatory.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(server, player):
    value = server.call('/auditstate', domain='placedSource4Originals', player=player)['domains']['placedSource4Originals']
    need(value.get('schema') == 1 and value.get('available') is True and value.get('active') is True,
         'actual DEBUG CodexTest Source4 observer required')
    need(value.get('identityObserversInstalled') is True and value.get('truncated') is False,
         'complete actual identity observer projection required')
    return value['records']


def actions(owner, sequence):
    need(isinstance(sequence, list) and sequence, 'actual observed crafting/UI actions required')
    for action in sequence:
        endpoint, values = action['endpoint'], action.get('args', {})
        need(endpoint in ALLOWED and not values.get('hold'), 'native input endpoint required')
        owner.guard_idle()
        owner.call(endpoint, 'POST', **values)
        delay = float(action.get('wait', .2))
        need(0 <= delay <= 5, 'bounded input wait required')
        time.sleep(delay)


def path_value(value, path):
    for key in path:
        value = value[key]
    return value


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('topology') in ('listen-remote', 'dedicated-remote'), 'explicit genuine remote topology required')
    need(plan.get('mode') in ('fresh', 'cold-server', 'cold-both'), 'explicit actual lifecycle mode required')
    player = plan.get('serverPlayerId')
    need(type(player) is int and player > 0, 'observed authoritative peer entity required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own, host = topology(owner), topology(server, player)
    require_peers(own, host)
    need(bool(host.get('dedicated')) == (plan['topology'] == 'dedicated-remote'), 'actual dedicated topology differs')
    need(owner.call('/guard')['enabled'] is True, 'native guard remains enabled')
    owner.call('/surroundings')
    resume = plan.get('resume')
    if plan['mode'] != 'fresh':
        need(isinstance(resume, dict) and resume.get('original'), 'saved genuine previous original required')
        for name in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(own[name] == resume['owner'][name] and host[name] == resume['server'][name], 'same saved original scope required: ' + name)
        need(host['processIdentity'] != resume['server']['processIdentity'], 'cold server requires genuine authority process change')
        if plan['mode'] == 'cold-both':
            need(own['processIdentity'] != resume['owner']['processIdentity'], 'cold-both requires genuine owner process change')
        original = resume['original']
    else:
        need(resume is None, 'fresh cannot reuse old lifecycle evidence')
        before = {row['sourceOperation'] for row in observe(server, player)}
        actions(owner, plan.get('actions'))
        original = None
    deadline = time.monotonic() + min(120, max(1, float(plan.get('timeout', 45))))
    while True:
        current_owner, current_host = topology(owner), topology(server, player)
        require_peers(current_owner, current_host)
        for name in ('worldKey', 'ownerKey', 'creationId', 'deaths', 'processIdentity', 'worldInstance'):
            need(current_owner[name] == own[name] and current_host[name] == host[name], 'unannounced native lifecycle transition: ' + name)
        rows = observe(server, player)
        if original is None:
            candidates = [row for row in rows if row['sourceOperation'] not in before and row.get('ownerIdentityPublicationObserved') is True]
            need(len(candidates) <= 1, 'ambiguous new actual source4 original')
            if candidates:
                original = candidates[0]
        matches = [] if original is None else [row for row in rows if row['sourceOperation'] == original['sourceOperation']]
        need(len(matches) <= 1, 'ambiguous original source observation')
        if matches:
            sample = matches[0]
            for name in ('binding', 'ownerKey', 'creationId', 'birth', 'proof'):
                need(sample[name] == original[name], 'immutable saved source identity changed: ' + name)
            ready = sample.get('ownerIdentityPublicationObserved') is True
            if plan['mode'] != 'fresh':
                ready = ready and sample.get('actualNativeLoadObserved') is True and sample.get('nativeOwnerReentryObserved') is True and sample.get('nativeReadFence', 0) > 0
            if ready:
                break
        need(time.monotonic() < deadline, 'actual saved original/native load/owner reentry did not qualify')
        time.sleep(.25)
    report['source4'] = sample
    report['resume'] = {'original': original, 'owner': own, 'server': host}
    if plan.get('externalPause') is True:
        report['result'] = 'PAUSED_FOR_PARENT_NATIVE_RESTART'
        return
    checks = plan.get('finalChecks')
    need(isinstance(checks, list) and len(checks) >= 2, 'actual native output and skill/completion checks required')
    need(any('inventory' in str(check.get('path', '')).lower() for check in checks) and
         any(word in str(checks).lower() for word in ('skill', 'experience', 'completion')),
         'checks must observe actual native inventory and skill/completion result')
    for check in checks:
        need(check['endpoint'] in ('/state', '/uitree', '/auditstate'), 'read-only final evidence endpoint required')
        value = owner.call(check['endpoint'], **check.get('args', {}))
        need(path_value(value, check['path']) == check['value'], 'native final output/skill/completion differs')
    resource = server.call('/auditstate', domain='remoteResourceOriginals', player=player, operation=original['sourceOperation'])['domains']['remoteResourceOriginals']
    originals = resource.get('records', resource.get('originals', []))
    matches = [row for row in originals if row.get('operation') == original['sourceOperation']]
    need(len(matches) == 1 and matches[0].get('phase') == 4 and matches[0].get('disposition') == 'used' and
         matches[0].get('ownerRejecting') == 0 and matches[0].get('outcome') == 1,
         'same source original must reach saved used terminal without another source debit')
    report['terminal'] = matches[0]
    report['result'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
    args = parser.parse_args()
    report = {'scenario': 'mp008-source4-native-original', 'status': 'RUNNING', 'requests': []}
    try:
        run(args, report)
    except Exception as error:
        report['result'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
