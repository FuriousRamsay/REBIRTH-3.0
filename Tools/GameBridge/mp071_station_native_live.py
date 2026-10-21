"""AUTHORED_NOT_RUN. Actual remote station original and close/switch endpoints.

PLAN REPORT --owner-session FILE --server-session FILE --mode native|survivor
Uses observed native UI selectors and the real typed outbox/authority evidence.
No launch, build, packet injection, receipt editing, save request or gameplay cheat.
External fault/reconnect/reload phases use externalPause and the saved report resume.
"""
import argparse
import json
import math
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers

KINDS = {'Craft', 'Cancel', 'CraftPlayer', 'ScrapQueue', 'ScrapCancel', 'FuelOn', 'FuelOff'}
ORIGINAL = ('operation', 'job', 'kind', 'recipe', 'batches', 'tier', 'vehicle', 'installation', 'binding', 'owner', 'creation')
MISSING = object()


def lookup(value, path):
    for part in path.split('.'):
        if not isinstance(value, dict) or part not in value:
            return MISSING
        value = value[part]
    return value


def field(value, path):
    for part in path.split('.'):
        need(isinstance(value, dict) and part in value, 'missing actual observation field: ' + path)
        value = value[part]
    return value


def commands(runner, mode, player=None, operation=None):
    args = {'domain': 'stationCommands'}
    if player is not None:
        args['player'] = player
    if operation:
        args['operation'] = operation
    value = runner.call('/auditstate', **args)['domains']['stationCommands']
    need(value.get('schema') == 1 and value.get('available') is True and not value.get('truncated'),
         'complete actual original observation required')
    need(value.get('survivorMode') is (mode == 'survivor'), 'actual mode differs from requested qualification')
    return value


def unique(rows, operation):
    matches = [row for row in rows if row['operation'] == operation]
    need(len(matches) <= 1, 'ambiguous original cannot qualify a command')
    return matches[0] if matches else None


def actions(runner, offered):
    if offered:
        runner.guard_idle()
    for action in offered:
        endpoint, values = action['endpoint'], action.get('args', {})
        need(endpoint in ALLOWED and not values.get('hold'), 'only bounded native input permitted')
        if endpoint == '/ui/drag':
            need(values.get('strict') == 1, 'actual observed strict slot drag required')
        if endpoint == '/press':
            need(0 < float(values.get('seconds', .2)) <= 2, 'bounded actual press required')
        runner.call(endpoint, 'POST', **values)
        delay = float(action.get('wait', .2))
        need(0 <= delay <= 5, 'bounded input wait required')
        time.sleep(delay)


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0, 'observed server owner entity required')
    need(plan.get('episodes') and isinstance(plan['episodes'], list), 'native endpoint episodes required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    original_owner, original_server = topology(owner), topology(server, plan['serverPlayerId'])
    require_peers(original_owner, original_server)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    resumed = plan.get('resume', {})
    completed = set()
    cursor_mark = None
    for episode in plan['episodes']:
        name, kind = episode.get('name'), episode.get('kind')
        need(isinstance(name, str) and name and kind in KINDS | {'SessionSwitch'}, 'named command or switch episode required')
        need(episode.get('checkpoints'), 'explicit endpoint checkpoints required')
        observed = commands(owner, args.mode)
        initial_installation = observed.get('session', {}).get('installation')
        baseline = {row['operation'] for row in observed['records']}
        original = dict(resumed[name]) if name in resumed else None
        if original:
            need(kind != 'SessionSwitch' and original['kind'] == kind, 'resume cannot change command purpose')
            for key in ('worldKey', 'ownerKey', 'creationId'):
                need(original[key] == original_owner[key], 'resume changes original ' + key)
        resume_kind = episode.get('resumeKind')
        need(resume_kind in (None, 'reconnect', 'cold-owner', 'cold-both'), 'unknown native resume qualification')
        if resume_kind:
            need(original is not None and original.get('ownerTopology') and original.get('serverTopology'),
                 'resume qualification requires saved actual endpoint topology')
            previous_owner, previous_server = original['ownerTopology'], original['serverTopology']
            if resume_kind == 'reconnect':
                need(original_owner['worldInstance'] != previous_owner['worldInstance'] or
                     original_owner['processIdentity'] != previous_owner['processIdentity'],
                     'reconnect requires an actual changed native owner world or process')
            else:
                need(original_owner['processIdentity'] != previous_owner['processIdentity'],
                     'cold owner requires an actual new native process')
            if resume_kind == 'cold-both':
                need(original_server['processIdentity'] != previous_server['processIdentity'],
                     'cold authority requires an actual new native process')
        for checkpoint in episode['checkpoints']:
            need(checkpoint.get('name') and any(checkpoint.get(key) for key in ('expectOwner', 'expectAuthority', 'expectSession', 'terminal', 'markCursor', 'maxCursorDisplacementPixels', 'expectLogContains')),
                 'checkpoint requires concrete actual assertions')
            offered = checkpoint.get('actions', [])
            actions(owner, offered)
            deadline = time.monotonic() + min(180, max(1, float(checkpoint.get('timeout', 60))))
            while True:
                owner_scope, server_scope = topology(owner), topology(server, plan['serverPlayerId'])
                require_peers(owner_scope, server_scope)
                for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId'):
                    need(owner_scope[key] == original_owner[key] and server_scope[key] == original_server[key],
                         'native episode changed; pause and resume exact saved original: ' + key)
                own = commands(owner, args.mode, operation=original['operation'] if original else None)
                if original is None and kind != 'SessionSwitch':
                    fresh = [row for row in own['records'] if row['operation'] not in baseline and row['kind'] == kind]
                    need(len(fresh) <= 1, 'one gesture created multiple original identities')
                    if fresh:
                        need(offered, 'new original must follow this actual native input phase')
                        original = {key: fresh[0][key] for key in ORIGINAL}
                        original.update({key: owner_scope[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                        original['ownerTopology'] = dict(owner_scope)
                        original['serverTopology'] = dict(server_scope)
                host = commands(server, args.mode, plan['serverPlayerId'], original['operation'] if original else None)
                own_row = unique(own['records'], original['operation']) if original else None
                host_row = unique(host['records'], original['operation']) if original else None
                if original:
                    for row in (own_row, host_row):
                        if row:
                            need(row.get('readable') is True, 'unreadable original cannot qualify endpoint')
                            for key in ORIGINAL:
                                need(row[key] == original[key], 'immutable complete original differs: ' + key)
                    if own_row and own_row.get('attempted') is True:
                        if 'identityFingerprint' in original:
                            need(own_row['identityFingerprint'] == original['identityFingerprint'], 'outbox original changed after first send')
                        else:
                            original['identityFingerprint'] = own_row['identityFingerprint']
                    report.setdefault('resume', {})[name] = original
                session = own.get('session', {})
                matches = all(lookup(session, key) == expected for key, expected in checkpoint.get('expectSession', {}).items())
                matches &= not checkpoint.get('expectOwner') or own_row is not None and all(lookup(own_row, key) == expected for key, expected in checkpoint['expectOwner'].items())
                matches &= not checkpoint.get('expectAuthority') or host_row is not None and all(lookup(host_row, key) == expected for key, expected in checkpoint['expectAuthority'].items())
                if checkpoint.get('terminal'):
                    terminal = checkpoint['terminal']
                    need(terminal in ('Applied', 'Rejected') and kind != 'SessionSwitch', 'typed saved terminal required')
                    matches &= own_row is not None and host_row is not None and own_row['area'] == 'completed' and own_row['terminal'] == terminal and host_row['status'] == terminal and host_row.get('bindingQualified') is True and bool(host_row.get('effectProof'))
                    matches &= session.get('commandPending') is False and session.get('uncertain') is False
                if checkpoint.get('expectLogContains'):
                    logs = owner.call('/log', since=owner.start, limit=500)
                    text = json.dumps(logs, ensure_ascii=False)
                    matches &= all(message in text for message in checkpoint['expectLogContains'])
                if checkpoint.get('maxCursorDisplacementPixels') is not None:
                    limit = float(checkpoint['maxCursorDisplacementPixels'])
                    need(cursor_mark is not None and 0 <= limit <= 32, 'prior native cursor mark and bounded tolerance required')
                    current = session.get('cursor')
                    matches &= current is not None and current.get('visible') is True and math.hypot(current['x'] - cursor_mark['x'], current['y'] - cursor_mark['y']) <= limit
                report.setdefault('observations', []).append({'episode': name, 'checkpoint': checkpoint['name'], 'owner': own, 'authority': host, 'ownerTopology': owner_scope, 'serverTopology': server_scope})
                owner.save()
                if matches:
                    if checkpoint.get('markCursor'):
                        cursor_mark = dict(field(session, 'cursor'))
                        need(cursor_mark.get('visible') is True, 'actual native visible pointer required')
                    report.setdefault('checkpoints', []).append({'episode': name, 'name': checkpoint['name'], 'operation': original['operation'] if original else None})
                    break
                need(time.monotonic() < deadline, 'actual endpoint did not reach expected original state: ' + name + '/' + checkpoint['name'])
                time.sleep(.5)
            if checkpoint.get('externalPause'):
                report.update(status='AWAITING_EXTERNAL', topology='GENUINE_REMOTE', mode=args.mode)
                return
        need(kind == 'SessionSwitch' or episode['checkpoints'][-1].get('terminal'), 'command episode must finish with exact saved terminal or external pause')
        if kind == 'SessionSwitch':
            final = episode['checkpoints'][-1]
            need(final.get('maxCursorDisplacementPixels') is not None and session.get('current') is True and session.get('commandPending') is False,
                 'successful switch requires current settled session and native pointer continuity')
            need(field(session, 'window.associated') is True and field(session, 'window.showing') is True and field(session, 'window.closing') is False,
                 'successful switch must reopen actual native workstation association')
            need(session.get('installation') and session['installation'] != initial_installation and session['installation'] != '0' * 32,
                 'successful switch requires the actual different target installation')
        completed.add(kind)
    required = set(plan.get('requiredKinds', []))
    need(required <= KINDS | {'SessionSwitch'} and required <= completed, 'required command endpoint coverage incomplete')
    owner.errors();server.errors()
    report.update(status='PASS', topology='GENUINE_REMOTE', mode=args.mode, completedKinds=sorted(completed), dedicated='OBSERVED' if original_server.get('dedicated') else 'NOT_RUN',
                  scope='Only executed native input/checkpoint assertions; external fault/death/cold cases are not inferred')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan');parser.add_argument('report')
    parser.add_argument('--owner-session', required=True);parser.add_argument('--server-session', required=True)
    parser.add_argument('--mode', choices=('native', 'survivor'), required=True)
    args = parser.parse_args();report = {'status': 'FAIL', 'observations': [], 'checkpoints': [], 'resume': {}}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
