"""AUTHORED_NOT_RUN. Actual restoration input and read-only native proof.

PLAN REPORT --owner-session FILE --server-session FILE [--local]
External interruption checkpoints preserve the actual original in REPORT.
No process control, save edits, injected receipts or direct service invocation.
"""
import argparse
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(runner, player=None, operation=None):
    values = {'domain': 'restorationOriginal'}
    if player is not None:
        values['player'] = player
    if operation:
        values['operation'] = operation
    state = runner.call('/auditstate', **values)['domains']['restorationOriginal']
    need(state.get('schema') == 1 and state.get('available') is True,
         'complete actual restoration native observation required')
    return state


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0,
         'observed actual authority player required')
    need(plan.get('episodes'), 'named native input episodes required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    host = Runner(report, Path(args.report), session_path=args.server_session)
    owner_scope, host_scope = topology(owner), topology(host, plan['serverPlayerId'])
    require_peers(owner_scope, host_scope, args.local)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    for episode in plan['episodes']:
        name = episode.get('name')
        need(name and episode.get('checkpoints'), 'episode and actual endpoint assertions required')
        baseline = observe(owner)
        need(baseline.get('ownerCustody', {}).get('available') is True, 'native owner custody observation required')
        known = {row['operation'] for row in baseline['ownerCustody']['operations']}
        original = plan.get('resume', {}).get(name)
        if original:
            original = dict(original)
            need(original['ownerTopology']['worldKey'] == owner_scope['worldKey'] and
                 original['ownerTopology']['ownerKey'] == owner_scope['ownerKey'] and
                 original['ownerTopology']['creationId'] == owner_scope['creationId'], 'resume changes actual original scope')
        baseline_host = observe(host, plan['serverPlayerId'], original['operation'] if original else None)
        kind = episode.get('resumeKind')
        need(kind in (None, 'reconnect', 'cold-owner', 'cold-both', 'later-life'), 'unknown resume qualification')
        if kind:
            need(original is not None, 'resume requires preserved actual original')
            old_owner, old_host = original['ownerTopology'], original['hostTopology']
            if kind == 'later-life':
                need(baseline['deaths'] > original['nativeDeaths'], 'actual later native life required')
            elif kind == 'reconnect':
                need(owner_scope['worldInstance'] != old_owner['worldInstance'] or owner_scope['processIdentity'] != old_owner['processIdentity'], 'real owner session change required')
            else:
                need(owner_scope['processIdentity'] != old_owner['processIdentity'], 'real owner cold process required')
            if kind == 'cold-both':
                need(host_scope['processIdentity'] != old_host['processIdentity'], 'real authority cold process required')
        acted = False
        for checkpoint in episode['checkpoints']:
            need(checkpoint.get('name') and (checkpoint.get('terminal') or checkpoint.get('expectOwner') or checkpoint.get('expectHost')), 'positive checkpoint assertions required')
            for action in checkpoint.get('actions', []):
                endpoint, values = action['endpoint'], action.get('args', {})
                need(endpoint in ALLOWED and not values.get('hold'), 'bounded real native inputs required')
                if endpoint == '/ui/drag':
                    need(values.get('strict') == 1, 'strict observed slot drag required')
                if endpoint == '/press':
                    need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
                owner.call(endpoint, 'POST', **values)
                acted = True
                delay = float(action.get('wait', .2))
                need(0 <= delay <= 5, 'bounded native input delay required')
                time.sleep(delay)
            deadline = time.monotonic() + min(180, max(1, float(checkpoint.get('timeout', 60))))
            while True:
                now_owner, now_host = topology(owner), topology(host, plan['serverPlayerId'])
                require_peers(now_owner, now_host, args.local)
                for field in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId'):
                    need(now_owner[field] == owner_scope[field] and now_host[field] == host_scope[field], 'scope changed; preserve report and resume exact original: ' + field)
                own = observe(owner, operation=original['operation'] if original else None)
                need(own.get('ownerCustody', {}).get('available') is True, 'complete native owner custody required')
                if original is None:
                    fresh = [row for row in own['ownerCustody']['operations'] if row['operation'] not in known]
                    need(len(fresh) <= 1, 'one native gesture created multiple originals')
                    if fresh:
                        need(acted, 'new original must follow actual native input')
                        original = dict(fresh[0], ownerTopology=dict(now_owner), hostTopology=dict(now_host), nativeDeaths=own['deaths'])
                hosted = observe(host, plan['serverPlayerId'], original['operation'] if original else None)
                need(own['deaths'] == baseline['deaths'] and hosted['deaths'] == baseline_host['deaths'],
                     'native life changed during invocation; preserve report and use explicit later-life continuation')
                selected = None if original is None else next((row for row in own['ownerCustody']['operations'] if row['operation'] == original['operation']), None)
                if original and hosted.get('source', {}).get('original'):
                    source_original = hosted['source']['original']
                    need(hosted['source']['fingerprint'] == original['fingerprint'], 'source and owner original fingerprints differ')
                    if original.get('sourceOriginal'):
                        need(original['sourceOriginal'] == source_original, 'immutable source original changed')
                    original['sourceOriginal'] = source_original
                if selected:
                    need(selected['digest'] == original['digest'] and selected['original'] == original['original'], 'immutable actual request changed')
                    report['resume'][name] = original
                matches = selected is not None
                for key, value in checkpoint.get('expectOwner', {}).items():
                    matches &= own.get(key) == value
                for key, value in checkpoint.get('expectHost', {}).items():
                    matches &= hosted.get(key) == value
                if checkpoint.get('terminal') and selected:
                    source, saved = hosted.get('source', {}), hosted.get('savedNativeOwner', {})
                    native_original = source.get('original')
                    matches &= source.get('completionSaved') is True and native_original is not None
                    if native_original:
                        if original.get('sourceOriginal'):
                            need(original['sourceOriginal'] == native_original, 'immutable source original changed')
                        original['sourceOriginal'] = native_original
                        frame = ET.fromstring(native_original)
                        receipt = {row['receipt']: row['stage'] for row in saved.get('receipts', [])}
                        rejected = source.get('rejectedCompletionSaved') is True
                        matches &= selected['outcome'] == (0 if rejected else 1) and selected['phase'] == 3
                        if not rejected:
                            later_life = hosted['deaths'] > int(frame.attrib.get('deaths', hosted['deaths']))
                            if later_life:
                                need(checkpoint.get('historicalTerminal'), 'later-life closure must be explicitly metadata-only')
                            else:
                                matches &= all(receipt.get(item.attrib['receipt']) == 'Applied' for item in frame.find('inventory'))
                            if source.get('trainingRequired'):
                                matches &= source.get('trainingCompletionSaved') is True
                            if not checkpoint.get('historicalTerminal'):
                                matches &= source.get('nativeAssembly', {}).get('payload') == frame.findtext('after')
                                matches &= own['nativeOwner']['bag'] == saved.get('bag') and own['nativeOwner']['belt'] == saved.get('belt')
                        if not args.local:
                            matches &= receipt.get(original['operation']) == 'Applied'
                    matches &= own.get('nativeOwner', {}).get('vehicleHold') is False and source.get('runtimeCurrent') is False
                    if checkpoint.get('historicalTerminal'):
                        need(kind is not None, 'historical closure requires qualified real continuation')
                        if kind == 'later-life':
                            matches &= own['nativeOwner']['bag'] == baseline['nativeOwner']['bag'] and own['nativeOwner']['belt'] == baseline['nativeOwner']['belt']
                            if baseline_host.get('source', {}).get('nativeAssembly') is not None:
                                matches &= source.get('nativeAssembly') == baseline_host['source']['nativeAssembly']
                report['observations'].append({'episode': name, 'checkpoint': checkpoint['name'], 'owner': own, 'host': hosted})
                owner.save()
                if matches:
                    report['checkpoints'].append({'episode': name, 'checkpoint': checkpoint['name'], 'operation': original['operation']})
                    break
                need(time.monotonic() < deadline, 'native checkpoint pending: ' + name + '/' + checkpoint['name'])
                time.sleep(.5)
            if checkpoint.get('externalPause'):
                report['status'] = 'AWAITING_EXTERNAL'
                return
        need(episode['checkpoints'][-1].get('terminal'), 'episode must close original or explicitly pause')
    owner.errors(); host.errors()
    report.update(status='PASS', topology='HOST_LOCAL' if args.local else 'GENUINE_REMOTE', scope='Only the actual inputs and explicit native checkpoints in this plan; unexecuted interruption cases remain NOT_RUN')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan'); parser.add_argument('report')
    parser.add_argument('--owner-session', required=True); parser.add_argument('--server-session', required=True)
    parser.add_argument('--local', action='store_true')
    args = parser.parse_args(); report = {'status': 'FAIL', 'observations': [], 'checkpoints': [], 'resume': {}}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
