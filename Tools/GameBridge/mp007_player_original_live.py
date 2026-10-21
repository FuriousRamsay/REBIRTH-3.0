"""AUTHORED_NOT_RUN. Real owner-input player-consumption original lifecycle.

External process/reconnect/death controls belong to the parent. A resume plan
must carry the previous report resume object and an explicit resumeKind.
No grants, callbacks, source setters, receipt writes or fault simulation.
"""
import argparse
import json
import re
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe_state(runner, player=None, operation=None):
    values = {'domain': 'playerConsumption'}
    if player is not None:
        values['player'] = player
    if operation:
        values['operation'] = operation
    state = runner.call('/auditstate', **values)['domains']['playerConsumption']
    need(state.get('schema') == 1 and state.get('available') is True and state.get('active') is True
         and state.get('truncated') is False, 'actual activated bounded player original audit required')
    need(all(not row.get('unknown') for row in state['records']), 'unknown disk stage cannot qualify settlement')
    return state


def observe(runner, player=None, operation=None):
    return observe_state(runner, player, operation)['records']


def refusal(args, plan, report, owner, server, player, own, host, local):
    before_owner, before_host = observe_state(owner), observe_state(server, player)
    need(re.fullmatch(r'[0-9a-f]{64}', before_owner.get('inventoryFingerprint', '')), 'actual complete native owner inventory fingerprint required')
    need(all(not row.get('activeOriginal') for row in before_host['records']) and all(not row.get('activeClient') for row in before_owner['records']), 'refusal starts without an existing owning action')
    baseline = {row['operation'] for row in before_host['records']}
    previous_refusal = before_host.get('lastRefusal', {}).get('sequence', 0)
    previous_feedback = before_owner.get('feedback', {}).get('sequence', 0)
    expected = plan.get('expectFeedback', {})
    need(type(expected.get('failure')) is int and expected['failure'] > 0 and expected.get('messageContains'), 'explicit native refusal failure and feedback text required')
    actions = plan.get('actions')
    need(isinstance(actions, list) and actions, 'actual native Use action required')
    used = False
    for action in actions:
        endpoint, values = action['endpoint'], action.get('args', {})
        need(endpoint in ALLOWED and not values.get('hold'), 'bounded actual native input only')
        if endpoint == '/press':
            need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native Use required')
        if endpoint == '/ui/drag':
            need(values.get('strict') == 1, 'strict observed native drag required')
        used |= endpoint in ('/press', '/ui/click', '/key')
        owner.call(endpoint, 'POST', **values)
        delay = float(action.get('wait', .2))
        need(0 <= delay <= 5, 'bounded native action wait required')
        time.sleep(delay)
    need(used, 'native refusal must follow native owner input')
    deadline = time.monotonic() + min(60, max(1, float(plan.get('timeout', 20))))
    while True:
        current_owner, current_host = topology(owner), topology(server, player)
        require_peers(current_owner, current_host, local)
        for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
            need(current_owner[key] == own[key] and current_host[key] == host[key], 'refusal episode changed: ' + key)
        recipient, authority = observe_state(owner), observe_state(server, player)
        need(recipient.get('inventoryFingerprint') == before_owner['inventoryFingerprint'], 'refused native owner full inventory changed')
        need({row['operation'] for row in authority['records']} == baseline, 'refusal unexpectedly admitted an authoritative original')
        proof, feedback = authority.get('lastRefusal', {}), recipient.get('feedback', {})
        if proof.get('sequence', 0) > previous_refusal and feedback.get('sequence', 0) > previous_feedback:
            need(proof.get('success') is False and proof.get('failure') == expected['failure'] and proof.get('sourcePermissionIssued') is False, 'positive original pre-admission refusal evidence required')
            for domain in ('source', 'canonical'):
                need(re.fullmatch(r'[0-9a-f]{64}', proof.get(domain + 'Before', '')) and proof[domain + 'Before'] == proof.get(domain + 'After'), 'actual ' + domain + ' changed across refused serving')
            need(feedback.get('success') is False and feedback.get('failure') == expected['failure'] and expected['messageContains'] in feedback.get('message', ''), 'actual owner refusal feedback required')
            report['checkpoints'].append({'name': 'native-pre-admission-refusal', 'owner': recipient, 'server': authority})
            owner.errors();server.errors()
            report.update(status='PASS', topology=plan['topology'], scope='Actual native pre-admission refusal, full owner/source inventory conservation, canonical serving conservation and owner feedback; no-payment is not inferred from absent original')
            return
        need(time.monotonic() < deadline, 'actual native pre-admission refusal evidence timed out')
        time.sleep(.5)


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    player = plan.get('serverPlayerId')
    need(type(player) is int and player > 0, 'observed authoritative owner entity required')
    need(plan.get('topology') in ('host-local', 'listen-remote', 'dedicated-remote'), 'explicit topology required')
    local = plan['topology'] == 'host-local'
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own, host = topology(owner), topology(server, player)
    require_peers(own, host, local)
    need(bool(host.get('dedicated')) == (plan['topology'] == 'dedicated-remote'), 'actual dedicated topology differs from plan')
    need(owner.call('/guard')['enabled'] is True, 'native guard remains enabled')
    owner.call('/surroundings')
    mode = plan.get('mode', 'original')
    need(plan.get('case', 'ordinary') in ('ordinary', 'rich-inventory'), 'supported native case required')
    need(mode in ('original', 'preadmission-refusal'), 'explicit supported native scenario mode required')
    if mode == 'preadmission-refusal':
        need(not plan.get('resume') and not plan.get('resumeKind'), 'fresh pre-admission refusal cannot claim original recovery')
        refusal(args, plan, report, owner, server, player, own, host, local)
        return
    original = plan.get('resume')
    resume_kind = plan.get('resumeKind')
    need(resume_kind in (None, 'reconnect', 'death', 'cold-owner', 'cold-both'), 'explicit known resume qualification required')
    if original:
        need(resume_kind is not None and re.fullmatch(r'[0-9a-f]{32}', original.get('operation', ''))
             and re.fullmatch(r'[0-9a-f]{64}', original.get('binding', '')), 'resume requires exact immutable original')
        for field in ('worldKey', 'ownerKey', 'creationId'):
            need(original[field] == own[field], 'resume changed original ' + field)
        previous_owner, previous_host = original['ownerTopology'], original['serverTopology']
        if resume_kind == 'reconnect':
            need(own['worldInstance'] != previous_owner['worldInstance'] or own['processIdentity'] != previous_owner['processIdentity'], 'reconnect requires genuinely new owner world/session')
        if resume_kind == 'death':
            need(own['deaths'] > previous_owner['deaths'], 'death qualification requires actual later life')
        if resume_kind in ('cold-owner', 'cold-both'):
            need(own['processIdentity'] != previous_owner['processIdentity'], 'cold owner requires new actual native process')
        if resume_kind == 'cold-both':
            need(host['processIdentity'] != previous_host['processIdentity'], 'cold authority requires new actual native process')
    else:
        need(resume_kind is None, 'resume kind requires original evidence')
    baseline = {row['operation'] for row in observe(server, player)}
    checkpoints = plan.get('checkpoints')
    need(isinstance(checkpoints, list) and checkpoints, 'nonempty native checkpoints required')
    native_input = False
    for checkpoint in checkpoints:
        need(checkpoint.get('name') and checkpoint.get('expectOwner') and checkpoint.get('expectServer'), 'named checkpoints need both actual endpoint assertions')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'only bounded real native input allowed')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native use required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native drag required')
            owner.guard_idle()
            owner.call(endpoint, 'POST', **values)
            native_input |= endpoint in ('/press', '/ui/click', '/key')
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded native action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current_owner, current_host = topology(owner), topology(server, player)
            require_peers(current_owner, current_host, local)
            for field in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current_owner[field] == own[field] and current_host[field] == host[field], 'episode changed; resume explicitly: ' + field)
            host_rows = observe(server, player, original['operation'] if original else None)
            if original is None:
                fresh = [row for row in host_rows if row['operation'] not in baseline]
                need(len(fresh) <= 1, 'one actual owner original required')
                if fresh:
                    need(native_input and fresh[0].get('binding'), 'fresh original requires native consumption input')
                    original = {key: fresh[0][key] for key in ('operation', 'binding')}
                    original.update({key: own[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                    original['ownerTopology'], original['serverTopology'] = dict(own), dict(host)
            if original:
                host_rows = [row for row in host_rows if row['operation'] == original['operation']]
                owner_rows = observe(owner, operation=original['operation'])
                need(len(host_rows) <= 1 and len(owner_rows) <= 1, 'ambiguous original evidence')
                if host_rows and owner_rows:
                    authority, recipient = host_rows[0], owner_rows[0]
                    if plan.get('case') == 'rich-inventory':
                        need(type(authority.get('purposeBytes')) is int and authority['purposeBytes'] > 48000, 'rich-inventory case requires actual full original owner purpose above48k')
                    need(authority.get('binding') == original['binding'], 'authoritative full original changed')
                    if recipient.get('binding'):
                        need(recipient['binding'] == original['binding'], 'owner purpose bound another original')
                    report['observations'].append({'name': checkpoint['name'], 'owner': recipient, 'server': authority})
                    report['resume'] = original
                    owner.save()
                    if all(recipient.get(key) == value for key, value in checkpoint['expectOwner'].items()) and all(authority.get(key) == value for key, value in checkpoint['expectServer'].items()):
                        if checkpoint.get('terminal'):
                            need(authority.get('diskCommitted') is True and authority.get('diskRetired') is True and authority.get('activeOriginal') is False
                                 and recipient.get('sourceMarker') == 0 and recipient.get('activeClient') is False, 'terminal requires authoritative saved original and actual owner marker/hold retirement')
                            need(all(key in checkpoint['expectOwner'] for key in ('sourceCalls', 'hydrationCalls', 'useCalls'))
                                 and all(key in checkpoint['expectServer'] for key in ('canonicalCalls', 'deferredCalls')), 'terminal requires explicit actual effect invocation counts')
                        report['checkpoints'].append({'name': checkpoint['name'], 'owner': recipient, 'server': authority})
                        break
            need(time.monotonic() < deadline, 'actual original checkpoint timed out: ' + checkpoint['name'])
            time.sleep(.5)
        if checkpoint.get('externalPause'):
            report.update(status='AWAITING_EXTERNAL', topology=plan['topology'])
            return
    need(checkpoints[-1].get('terminal') is True, 'PASS requires complete native original terminal')
    owner.errors()
    server.errors()
    report.update(status='PASS', topology=plan['topology'], scope='Executed real-input assertions only; unexecuted death/reconnect/cold cases are NOT_RUN')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan');parser.add_argument('report')
    parser.add_argument('--owner-session', required=True);parser.add_argument('--server-session', required=True)
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
