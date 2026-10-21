"""AUTHORED_NOT_RUN. Actual native hydration UI/original/terminal endpoints.

PLAN REPORT --owner-session FILE --server-session FILE [--local]
Native inputs and read-only hydrationOriginal observations only. External
disconnect/cold/fault checkpoints pause and preserve the exact report original.
No process control, save edits, receipt injection or direct service calls.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(runner, player=None, original=None):
    values = {'domain': 'hydrationOriginal'}
    if player is not None:
        values['player'] = player
    if original:
        values.update(operation=original['operation'], digest=original['digest'])
    result = runner.call('/auditstate', **values)['domains']['hydrationOriginal']
    need(result.get('schema') == 1 and result.get('available') is True,
         'complete actual hydration observation required')
    return result


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0,
         'observed server owner entity required')
    need(plan.get('episodes') and isinstance(plan['episodes'], list), 'actual native input episodes required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own_scope, host_scope = topology(owner), topology(server, plan['serverPlayerId'])
    require_peers(own_scope, host_scope, args.local)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    completed = []
    for episode in plan['episodes']:
        name = episode.get('name')
        need(isinstance(name, str) and name and type(episode.get('equip')) is bool,
             'named equip or unequip episode required')
        need(episode.get('checkpoints'), 'explicit actual endpoint checkpoints required')
        baseline_owner = observe(owner)
        baseline_host = observe(server, plan['serverPlayerId'])
        baseline = {row['operation'] for row in baseline_owner['operations']}
        prior = plan.get('resume', {}).get(name)
        original = dict(prior) if prior else None
        if original:
            for field in ('worldKey', 'ownerKey', 'creationId'):
                need(original['ownerTopology'][field] == own_scope[field], 'resume changes original ' + field)
        resume_kind = episode.get('resumeKind')
        need(resume_kind in (None, 'reconnect', 'cold-owner', 'cold-both'), 'unknown resume qualification')
        if resume_kind:
            need(original is not None, 'resume requires saved actual original')
            old_owner, old_host = original['ownerTopology'], original['hostTopology']
            if resume_kind == 'reconnect':
                need(own_scope['worldInstance'] != old_owner['worldInstance'] or own_scope['processIdentity'] != old_owner['processIdentity'],
                     'actual owner world/process must change before reconnect qualification')
            else:
                need(own_scope['processIdentity'] != old_owner['processIdentity'], 'actual cold owner process required')
            if resume_kind == 'cold-both':
                need(host_scope['processIdentity'] != old_host['processIdentity'], 'actual cold authority process required')
        had_input = False
        for checkpoint in episode['checkpoints']:
            need(checkpoint.get('name') and (checkpoint.get('expectOwner') or checkpoint.get('expectHost') or checkpoint.get('terminal') or checkpoint.get('settledRefusal')),
                 'checkpoint requires concrete actual assertions')
            actions = checkpoint.get('actions', [])
            if actions:
                owner.guard_idle()
            for action in actions:
                endpoint, values = action['endpoint'], action.get('args', {})
                need(endpoint in ALLOWED and not values.get('hold'), 'bounded actual native input required')
                if endpoint == '/ui/drag':
                    need(values.get('strict') == 1, 'strict observed native slot drag required')
                if endpoint == '/press':
                    need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
                owner.call(endpoint, 'POST', **values)
                had_input = True
                delay = float(action.get('wait', .2))
                need(0 <= delay <= 5, 'bounded native input wait required')
                time.sleep(delay)
            deadline = time.monotonic() + min(180, max(1, float(checkpoint.get('timeout', 60))))
            while True:
                current_owner, current_host = topology(owner), topology(server, plan['serverPlayerId'])
                require_peers(current_owner, current_host, args.local)
                for field in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId'):
                    need(current_owner[field] == own_scope[field] and current_host[field] == host_scope[field],
                         'native scope changed; preserve report and resume original: ' + field)
                own = observe(owner, original=original)
                if original is None:
                    fresh = [row for row in own['operations'] if row['operation'] not in baseline]
                    need(len(fresh) <= 1, 'native gesture created multiple original identities')
                    if fresh:
                        need(had_input, 'fresh original requires preceding native input')
                        original = dict(fresh[0], ownerTopology=dict(current_owner), hostTopology=dict(current_host))
                        own = observe(owner, original=original)
                        need(own.get('original') and own.get('equip') is episode['equip'], 'actual original purpose differs')
                        original['original'] = own['original']
                host = observe(server, plan['serverPlayerId'], original)
                if original:
                    for value in (own, host):
                        if value.get('operation'):
                            need(value['operation'] == original['operation'] and value['digest'] == original['digest'] and value['original'] == original['original'],
                                 'complete immutable original differs across endpoints')
                    report.setdefault('resume', {})[name] = original
                matches = all(own.get(key) == expected for key, expected in checkpoint.get('expectOwner', {}).items())
                matches &= all(host.get(key) == expected for key, expected in checkpoint.get('expectHost', {}).items())
                if checkpoint.get('terminal'):
                    need(checkpoint['terminal'] == 'Applied', 'positive joint Applied terminal required')
                    matches &= original is not None and host.get('financialApplied') is True and host.get('completionSaved') is True
                    matches &= own.get('nativeReadable') is True and own.get('nativeApplied') is True and own.get('nativeStage') == 1
                    matches &= own.get('ownerHeld') is False and host.get('sourceHeld') is False
                    matches &= args.local or own.get('outboxClosed') is True and own.get('nativeSeen') == 1
                    if checkpoint.get('historicalTerminal'):
                        need(resume_kind is not None and not args.local,
                             'historical metadata-only terminal requires an actual resumed remote original')
                    else:
                        matches &= own.get('liveOwnerAfter') is True
                if checkpoint.get('settledRefusal'):
                    need(had_input and original is None and baseline_host.get('source') is not None,
                         'fresh refusal requires actual input, no admitted original and observed source baseline')
                    matches &= {row['operation'] for row in own['operations']} == baseline
                    matches &= own.get('ownerHeld') is False and host.get('sourceHeld') is False
                    matches &= own.get('nativeBag') == baseline_owner.get('nativeBag') and own.get('nativeBelt') == baseline_owner.get('nativeBelt')
                    matches &= host.get('source') == baseline_host['source']
                report.setdefault('observations', []).append({'episode': name, 'checkpoint': checkpoint['name'], 'owner': own, 'host': host})
                owner.save()
                if matches:
                    report.setdefault('checkpoints', []).append({'episode': name, 'name': checkpoint['name'], 'operation': original['operation'] if original else None})
                    break
                need(time.monotonic() < deadline, 'actual endpoint did not reach expected state: ' + name + '/' + checkpoint['name'])
                time.sleep(.5)
            if checkpoint.get('externalPause'):
                report.update(status='AWAITING_EXTERNAL', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE')
                return
        need(episode['checkpoints'][-1].get('terminal') or episode['checkpoints'][-1].get('settledRefusal'),
             'episode must reach joint terminal, proven unchanged fresh refusal or external pause')
        completed.append(name)
    owner.errors();server.errors()
    report.update(status='PASS', completed=completed, topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE',
                  dedicated='OBSERVED' if host_scope.get('dedicated') else 'NOT_RUN',
                  scope='Only executed native inputs and explicit endpoint assertions; unexecuted interruption cases are not inferred')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan');parser.add_argument('report')
    parser.add_argument('--owner-session', required=True);parser.add_argument('--server-session', required=True)
    parser.add_argument('--local', action='store_true')
    args = parser.parse_args();report = {'status': 'FAIL', 'observations': [], 'checkpoints': [], 'resume': {}}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
