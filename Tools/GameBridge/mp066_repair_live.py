"""Deferred MP066 actual native repair driver; AUTHORED_NOT_RUN.

PLAN REPORT --owner-session FILE --server-session FILE.
PLAN supplies actual serverPlayerId and observed real-input checkpoints. No
launch/build, inventory hydration, repair invocation, file edit or receipt replay.
Use expectOwner/expectServer, terminal published/base-only/cancelled, and
externalPause plus exact report resume evidence for parent-owned lifecycle steps.
--local qualifies only actual same-process host control.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers, fingerprint


def terminal(owner, server, kind):
    if not owner or not server:
        return False
    if any(row.get('savedQueueOriginalPresent') is not False for row in (owner, server)):
        return False
    if not isinstance(server.get('nativeWorld'), dict) or server['nativeWorld'].get('Occurrences') != 0:
        return False  # Loaded dropped duplicates or an incomplete observer.
    if owner.get('ownerJournalActive') is not False or owner.get('ownerCardHeld') is not False or owner.get('pendingNativeOutcome') is not False or owner.get('journalArchived') is not True:
        return False
    if kind == 'published':
        settled = all(row.get('published') is True for row in (owner, server))
        expected = 'AfterMatches'
    elif kind == 'base-only':
        settled = all(row.get('baseOnly') is True and row.get('baseOnlyConfirmed') is True and row.get('published') is False for row in (owner, server))
        expected = 'AfterMatches'
    elif kind == 'cancelled':
        settled = all(row.get('cancelled') is True and row.get('cancelReturned') is True and row.get('cancelConfirmed') is True for row in (owner, server))
        expected = 'ReturnedInputMatches'
    else:
        raise AssertionError('explicit native terminal kind required')
    def native(row, field):
        value = row.get(field)
        return isinstance(value, dict) and value.get('Occurrences') == 1 and value.get(expected) is True
    # A dropped tool outside inventory is unresolved world custody here; never
    # certify it from queue absence or protocol flags alone.
    return settled and native(owner, 'nativeLive') and all(native(row, 'nativeSaved') for row in (owner, server))


def observe(runner, player=None):
    args = {'domain': ''}
    if player is not None:
        args['player'] = player
    snapshot = runner.call('/auditstate', **args)
    need(snapshot['domains']['topology'].get('gameName') == 'CodexTest', 'actual disposable CodexTest required')
    need('repairOriginals' in snapshot['domains'], 'actual repair domain missing')
    need(snapshot['domains']['repairOriginals'].get('scope') is True and not snapshot['domains']['repairOriginals'].get('truncated'), 'bounded repair observation unavailable')
    return snapshot


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(type(plan['serverPlayerId']) is int and plan['serverPlayerId'] > 0, 'actual authenticated owner required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    own_top, host_top = topology(owner), topology(server, plan['serverPlayerId'])
    require_peers(own_top, host_top, args.local)
    report['topology'] = 'HOST_LOCAL' if args.local else ('DEDICATED_REMOTE' if host_top.get('dedicated') else 'LISTEN_REMOTE')
    need(owner.call('/guard').get('enabled') is True, 'owner guard must remain enabled')
    report['arrival'] = owner.call('/surroundings')
    baseline = observe(server, plan['serverPlayerId'])
    known = {row['operation'] for row in baseline['domains']['repairOriginals']['records']}
    original = dict(plan['resume']) if plan.get('resume') else None
    operation = original.get('operation') if original else None
    input_image = original.get('inputFingerprint') if original else None
    expected_native = original.get('stagedOutputFingerprint') if original else None
    if operation is not None:
        need(fingerprint(input_image) and fingerprint(expected_native),
             'resume requires prior original full-image fingerprints')
        need(all(original.get(key) == own_top[key] for key in ('worldKey', 'ownerKey', 'creationId')), 'resume changed original stable identity')
        need(all(key in original for key in ('sourceOperation', 'sourceBinding', 'originalDeaths')), 'resume omitted original source/life binding')
    resume_kind = plan.get('resumeKind')
    need(resume_kind in (None, 'death', 'reconnect', 'cold-owner', 'cold-both'), 'unknown resume kind')
    if resume_kind:
        need(original and original.get('ownerTopology') and original.get('serverTopology'), 'actual prior endpoint provenance required')
        previous_owner, previous_server = original['ownerTopology'], original['serverTopology']
        if resume_kind == 'death':
            need(own_top['deaths'] > previous_owner['deaths'] and own_top['deaths'] == host_top['deaths'], 'actual later native life required')
        elif resume_kind == 'reconnect':
            need(any(own_top[key] != previous_owner[key] for key in ('processIdentity', 'worldInstance')), 'actual changed owner process/world required')
        else:
            need(own_top['processIdentity'] != previous_owner['processIdentity'], 'actual cold owner process required')
        if resume_kind == 'cold-both':
            need(host_top['processIdentity'] != previous_server['processIdentity'], 'actual cold authority process required')
    need(plan.get('checkpoints'), 'real native action checkpoints required')
    had_input = False
    for checkpoint in plan['checkpoints']:
        need(checkpoint.get('expectOwner') or checkpoint.get('expectServer') or checkpoint.get('terminal'), 'actual owner/server assertions required')
        if checkpoint.get('actions'):
            owner.guard_idle()
        for action in checkpoint.get('actions', []):
            need(action['endpoint'] in ALLOWED and action.get('session', 'owner') == 'owner', 'only owning native input permitted')
            values = action.get('args', {})
            need(not values.get('hold'), 'unbounded held input refused')
            if action['endpoint'] == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native input required')
            if action['endpoint'] == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native drag required')
            had_input |= action['endpoint'] in ('/ui/click', '/ui/drag', '/activate')
            owner.call(action['endpoint'], 'POST', **values)
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded native action wait required')
            time.sleep(delay)
        deadline = time.monotonic() + min(180, max(1, float(checkpoint.get('timeout', 60))))
        while True:
            current_owner, current_server = topology(owner), topology(server, plan['serverPlayerId'])
            require_peers(current_owner, current_server, args.local)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current_owner[key] == own_top[key] and current_server[key] == host_top[key], 'episode changed; resume recorded original: ' + key)
            need(owner.call('/guard').get('enabled') is True, 'owner guard disabled')
            snapshot = observe(server, plan['serverPlayerId'])
            owner_snapshot = observe(owner)
            report['observations'].append({'owner': owner_snapshot, 'server': snapshot})
            records = [row for row in snapshot['domains']['repairOriginals']['records'] if row.get('journalRole') == 'server']
            fresh = {row['operation'] for row in records} - known
            need(len(fresh) <= 1 and (operation is None or fresh <= {operation}), 'prepare exactly one actual native repair')
            if operation is None and fresh:
                need(had_input, 'fresh original requires real native UI input')
                operation = next(iter(fresh))
            matches = [row for row in records if row['operation'] == operation]
            need(len(matches) <= 1, 'ambiguous native original')
            if matches:
                row = matches[0]
                need(row.get('journalReadable') is True, 'unreadable primary is unknown')
                if input_image is None:
                    input_image, expected_native = row['inputFingerprint'], row['stagedOutputFingerprint']
                    need(fingerprint(input_image) and fingerprint(expected_native), 'full actual native image fingerprints required')
                    original = {key: row[key] for key in ('operation', 'inputFingerprint', 'stagedOutputFingerprint', 'sourceOperation', 'sourceBinding', 'originalDeaths')}
                    original.update({key: own_top[key] for key in ('worldKey', 'ownerKey', 'creationId')})
                need(row['inputFingerprint'] == input_image and row['stagedOutputFingerprint'] == expected_native,
                     'original full native input/expected output replaced')
                owner_rows = [value for value in owner_snapshot['domains']['repairOriginals']['records'] if value['operation'] == operation and value['journalRole'] == 'owner']
                need(len(owner_rows) <= 1, 'ambiguous owner original')
                owner_row = owner_rows[0] if owner_rows else None
                identity = ('inputFingerprint', 'stagedOutputFingerprint', 'sourceOperation', 'sourceBinding', 'originalDeaths')
                for current in (row, owner_row):
                    if current:
                        need(current.get('journalReadable') is True and all(current.get(key) == original[key] for key in identity), 'original full native/source/life binding changed')
                original['ownerTopology'], original['serverTopology'] = dict(own_top), dict(host_top)
                report['resume'] = original
                if owner_row and all(row.get(field) == value for field, value in checkpoint.get('expectServer', {}).items()) and all(owner_row.get(field) == value for field, value in checkpoint.get('expectOwner', {}).items()) and (not checkpoint.get('terminal') or terminal(owner_row, row, checkpoint['terminal'])):
                    report['checkpoints'].append({'name': checkpoint['name'], 'owner': owner_row, 'server': row})
                    break
            need(time.monotonic() < deadline, 'repair phase timeout: ' + checkpoint['name'])
            time.sleep(.3)
        if checkpoint.get('externalPause'):
            need(original is not None, 'external pause requires original evidence')
            report.update(status='WAITING_EXTERNAL', pause=checkpoint['externalPause'])
            return
    need(plan['checkpoints'][-1].get('terminal'), 'PASS requires actual native terminal evidence')
    report.update(status='PASS', operation=operation)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
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
