"""Actual remote cancellation driver; AUTHORED_NOT_RUN. Never enables or launches.

start PLAN REPORT --case normal|receipt1|checkpoint|before2|before2unknown|before2consumed|receipt2|receipt3|unknown|custody
resume PLAN REPORT --restarted client|server|both|none

Requires a reviewed DEBUG build, genuine two-peer CodexTest sessions, and observed
real-input selectors in PLAN.actions. The operator owns genuine process restarts;
resume verifies changed authenticated bridge process identities. No receipt,
inventory, native read, saved file or journal is injected by this driver.
"""
import argparse
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED

PREFIX = '[REBIRTH placed cancellation scenario] '


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for item in value.values():
            yield from strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)


def console(runner, command):
    reply = runner.call('/console', 'POST', cmd=command)
    values = {text.split(PREFIX, 1)[1] for text in strings(reply) if PREFIX in text}
    need(len(values) == 1, 'one complete actual production observation required')
    result = json.loads(values.pop())
    # Successful checkpoints below retain the complete validated originals.
    # Do not accumulate another full console copy on every polling request.
    runner.report['requests'][-1]['reply'] = {'ok': True, 'observedOperation': result.get('operation'),
                                            'available': result.get('available'), 'boundary': result.get('boundary')}
    runner.save()
    return result


def observe(runner, operation=None):
    result = console(runner, 'rbplacedcancelaudit' + (' operation=' + operation if operation else ''))
    need(result['available'] and result['active'], 'actual active original unavailable')
    for row in result['journals']:
        need(row['before'] == row['after'] and row['revision'] == 0, 'mixed full immutable original')
    return result


def hold(runner, boundary, operation=None, slot=0):
    result = console(runner, 'rbplacedcancelfixture ' + boundary + ' slot=' + str(slot) +
                     (' operation=' + operation if operation else '') + ' confirm=CodexTest')
    need(result['boundary'] == boundary and result['slot'] == slot, 'actual fixture boundary refused')


def record(row, purpose, required=True):
    matches = [entry for entry in row['journals'] if entry['purpose'] == purpose]
    need(len(matches) <= 1 and (not required or len(matches) == 1), 'ambiguous/missing full original: ' + purpose)
    return matches[0] if matches else None


def events(row, boundary=None):
    return [event for event in row['events'] if boundary is None or event['boundary'] == boundary]


def wait(check, timeout=90):
    deadline = time.monotonic() + timeout
    while True:
        try:
            return check()
        except AssertionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(.4)


def actions(runner, sequence):
    need(sequence, 'observed real-input actions required')
    for action in sequence:
        need(action['endpoint'] in ALLOWED, 'native input endpoint required; no fixture financial writes')
        runner.guard_idle()
        runner.call(action['endpoint'], 'POST', **action.get('args', {}))
        time.sleep(min(10, max(0, action.get('wait', .25))))


def pair(client, server, operation=None):
    local = observe(client, operation)
    authority = observe(server, local['operation'])
    need(local['role'] == 'client' and authority['role'] == 'server', 'genuine remote peers required')
    need(local['world'] == authority['world'], 'different native save worlds')
    cp, sp = record(local, 'Permission'), record(authority, 'Permission')
    need(cp['before'] == sp['before'] and cp['source'] == sp['source'] and cp['creation'] == sp['creation'], 'full original permission/owner differs')
    return local, authority, ET.fromstring(cp['before'])


def slots(local):
    result = {}
    for row in local['journals']:
        if row['purpose'] == 'OwnerSlot':
            body = ET.fromstring(row['before'])
            ordinal = int(body.attrib['ordinal'])
            need(ordinal not in result, 'duplicate slot original')
            result[ordinal] = row
    return result


def checkpoint(local, prefix):
    matches = [ET.fromstring(row['before']) for row in local['journals'] if row['purpose'] == 'OwnerCheckpoint'
               and row['phase'] == 'Applied' and int(ET.fromstring(row['before']).attrib['ordinal']) == prefix - 1]
    need(len(matches) == 1 and matches[0].findtext('nativeOwner'), 'full positively saved native checkpoint required')
    return matches[0]


def paused(case, client, server, operation, prefix):
    local, authority, permission = pair(client, server, operation)
    state = local['client']
    need(state is not None and state['held'], 'actual owner custody missing')
    if case in ('before2', 'before2unknown', 'before2consumed'):
        need(local['receipt'] == 2 and state['stage'] == 2 and local['actor'] == permission.findtext('afterActor'), 'full saved owner2 After required; no refund remains')
        saved = authority['savedOwner']
        need(saved and saved['receipt'] == 2 and saved['actor'] == permission.findtext('afterActor'), 'actual primary native owner2 must already contain entire refund After')
        custody = record(authority, 'NativePlacedCancellationHostCustody')
        need(custody['phase'] == ('Uncertain' if case == 'before2consumed' else 'Prepared') and ET.fromstring(custody['before']).attrib['version'] == '2', 'exact positive v2 custody boundary required')
        need(authority['server']['held'] and authority['server']['QueueAttempted'] == (case == 'before2consumed') and not authority['server']['AfterSaved'] and
             authority['nativeCanonical'] == permission.findtext('beforeTile'), 'actual held native Before, not a later equal getter or saved After')
        mutation = record(authority, 'Mutation', required=False)
        need((mutation is None) if case == 'before2' else mutation and mutation['phase'] == 'Uncertain', 'wrong real first-attempt intent boundary')
        need(all(row['phase'] == 'Applied' for row in slots(local).values()), 'all owner refund slots must already be durably complete')
    elif case in ('receipt1', 'checkpoint', 'custody', 'unknown'):
        need(state['stage'] == 1 and local['receipt'] == 1 and state['completed'] == prefix and state['baseline'], 'actual receipt1 saved prefix required')
        plan = record(local, 'OwnerPlan')
        need(plan['phase'] == 'Applied', 'full manifest must precede any actual setter')
        commands = slots(local)
        need(len(commands) > prefix, 'at least one remaining real refund slot required')
        for ordinal, row in commands.items():
            expected = 'Applied' if ordinal < prefix else 'Uncertain' if case in ('unknown', 'checkpoint') and ordinal == prefix else 'Prepared'
            need(row['phase'] == expected, 'wrong exact attempted boundary: ' + str(ordinal))
        need(record(authority, 'NativePlacedCancellationHostCustody')['phase'] == 'Prepared', 'positive v2 never-entered native custody required')
        need(authority['server']['held'] and authority['nativeCanonical'] == permission.findtext('beforeTile'), 'entire native Before must remain held')
        if case in ('unknown', 'checkpoint'):
            need(state['steps'][prefix]['NativeReturned'] is True and events(local, 'actualOwnerSetter' + str(prefix)), 'fault must follow real original setter return, not a stand-in success')
        if case != 'unknown':
            saved = authority['savedOwner']
            committed_prefix = prefix + 1 if case == 'checkpoint' else prefix
            full_checkpoint = checkpoint(local, committed_prefix)
            need(saved and saved['receipt'] == 1 and saved['actor'] == full_checkpoint.findtext('actor'), 'host must have actually saved the same complete prefix before restart')
            if case == 'checkpoint':
                need(full_checkpoint.attrib['setterReturned'] == 'true' and local['actor'] == full_checkpoint.findtext('actor'), 'positive actual setter-return native checkpoint required before slot metadata')
    else:
        need(local['receipt'] == (2 if case == 'receipt2' else 3), 'wrong actual native owner receipt')
        for purpose in ('Mutation', 'After', 'Terminal'):
            need(record(authority, purpose)['phase'] == 'Applied', 'saved native effect/terminal must already be complete')
        need(authority['nativeCanonical'] == permission.findtext('afterTile') and authority['server']['held'], 'full native saved After remains frozen at real boundary')
        need(local['actor'] == permission.findtext('afterActor'), 'entire actual refund image differs')
    return local, authority


def terminal(client, server, operation, baseline, restarted):
    local, authority, permission = pair(client, server, operation)
    need(local['receipt'] == 0 and local['client'] is None and authority['server'] is None and authority['archive'] is None, 'normal final ACK must release both actual originals')
    need(local['actor'] == permission.findtext('afterActor'), 'full owner After changed or refund duplicated')
    need(local['retainedBytes'] == 0 and authority['retainedBytes'] == 0, 'isolated fixture native/financial graph allocations not retired after actual ACK/send callbacks')
    for row in (local, authority):
        need(record(row, 'Terminal')['phase'] == 'Applied' and record(row, 'FinalAck')['phase'] == 'Applied', 'both full terminal/ACK originals required')
    need(ET.fromstring(record(local, 'Terminal')['before']).findtext('evidence') == record(authority, 'After')['before'], 'client terminal must contain exact full actual native After evidence')
    for purpose in ('Permission', 'OwnerPlan'):
        old = record(baseline[0], purpose, required=False)
        if old:
            need(record(local, purpose)['before'] == old['before'], 'restart replaced complete original ' + purpose)
    old_commands = slots(baseline[0])
    for ordinal, old in old_commands.items():
        current = slots(local)[ordinal]
        need(current['before'] == old['before'] and current['phase'] == 'Applied', 'slot command changed or incomplete')
    if restarted in ('client', 'both'):
        case = baseline[2]
        need(events(local, 'actualOwnerHydration' + ('1' if case in ('receipt1', 'checkpoint', 'custody') else '2' if case in ('receipt2', 'before2') else '3')), 'actual authenticated PlayerId/ToPlayer boundary missing')
        if case == 'before2':
            need(events(local, 'actualBeforeTwoAfterProjection'), 'actual authenticated Before-load first-attempt saved-After metadata projection required')
        return_events = [event for event in events(local) if event['boundary'].startswith('actualOwnerSetter')]
        actual_returns = {int(event['boundary'][17:]) for event in return_events}
        expected = {ordinal for ordinal, row in old_commands.items() if row['phase'] == 'Prepared'} if case in ('receipt1', 'checkpoint', 'custody') else set()
        need(actual_returns == expected and len(return_events) == len(expected), 'historical/duplicate owner setter replay or missing actual remaining setter')
    else:
        returns = [event for event in events(local) if event['boundary'].startswith('actualOwnerSetter')]
        need(len(returns) == len(old_commands) and len({event['boundary'] for event in returns}) == len(old_commands), 'normal/warm owner setters must each return exactly once')
    if restarted in ('server', 'both'):
        case = baseline[2]
        boundary = 'actualNativeBeforeLoad' if case in ('receipt1', 'checkpoint', 'custody') else 'actualNativeBeforeLoad2' if case == 'before2' else 'actualNativeAfterLoad2' if case == 'receipt2' else 'actualNativeAfterLoad3'
        need(events(authority, boundary), 'actual private saved native load/factory/read lineage missing')
        if case in ('receipt2', 'receipt3'):
            need(not events(authority, 'actualNativeQueueCompleted'), 'already saved native effect replayed')
        else:
            need(len(events(authority, 'actualNativeQueueCompleted')) == 1, 'reconstructed Before must enter native queue/input mutation exactly once')
    elif baseline[2] in ('normal', 'receipt2', 'receipt3', 'before2'):
        need(len(events(authority, 'actualNativeQueueCompleted')) == 1, 'normal queue/input mutation must complete exactly once')
    return local, authority


def run(args, report, client, server, plan):
    owner_topology = client.call('/auditstate', domain='topology')['domains']['topology']
    host_topology = server.call('/auditstate', domain='topology', player=owner_topology['entityId'])['domains']['topology']
    topology = {'client': owner_topology, 'server': host_topology}
    need(all(row['gameName'] == 'CodexTest' for row in topology.values()), 'genuine disposable CodexTest only')
    need(owner_topology['remote'] and owner_topology['connected'] and host_topology['server'] and host_topology['authoritativePeer'], 'actual connected owner and authoritative native peer required')
    need(owner_topology['processIdentity'] != host_topology['processIdentity'], 'independent actual owner and host processes required')
    need(plan.get('topology') in ('listen', 'dedicated') and host_topology['dedicated'] is (plan['topology'] == 'dedicated'), 'explicit actual native listen/dedicated topology must match plan')
    need(client.call('/guard').get('enabled') is True, 'client instincts must remain enabled')
    report['surroundings'] = client.call('/surroundings')
    if args.phase == 'start':
        report.update(case=args.case, prefix=plan.get('prefix', 0), sessions={'client': client.scope, 'server': server.scope}, topology=topology)
        case = args.case
        if case in ('receipt1', 'checkpoint', 'custody', 'unknown'):
            hold(client, 'ownerReturned' if case == 'unknown' else 'ownerCheckpoint' if case == 'checkpoint' else 'ownerPrepared', slot=report['prefix'])
        elif case != 'normal':
            hold(server, 'nativeBefore' if case == 'before2' else 'nativeIntent' if case == 'before2unknown' else 'nativeConsumed' if case == 'before2consumed' else 'savedAfter' if case == 'receipt2' else 'terminalAck')
        else:
            hold(server, 'savedAfter')
        actions(client, plan['actions'])
        if case == 'normal':
            local, authority = wait(lambda: paused('receipt2', client, server, None, 0))
            report['operation'] = local['operation']
            report['baseline'] = [local, authority, case]
            hold(server, 'release', local['operation'])
            report['terminal'] = wait(lambda: terminal(client, server, report['operation'], report['baseline'], 'none'))
        else:
            local, authority = wait(lambda: paused(case, client, server, None, report['prefix']))
            report['operation'] = local['operation']
            if case == 'custody':
                before_native, before_actor, before_block = authority['native'], local['actor'], authority['block']
                known = {row['key'] for row in authority['journals'] if row['purpose'] == 'NativePlacedCancellationHostRefusal'}
                actions(client, plan['escapeActions'])
                local, authority = wait(lambda: paused(case, client, server, report['operation'], report['prefix']))
                refusals = [row for row in authority['journals'] if row['purpose'] == 'NativePlacedCancellationHostRefusal' and row['key'] not in known and row['phase'] == 'Applied']
                need(refusals and authority['native'] == before_native and local['actor'] == before_actor and authority['block'] == before_block, 'actual escape must be refused before native station/block/owner change')
            report['baseline'] = [local, authority, case]
        report['status'] = 'PASS_START_BOUNDARY' if case != 'normal' else 'PASS_NORMAL'
        return
    case, operation = report['case'], report['operation']
    need(case != 'normal', 'normal flow already complete')
    if case == 'checkpoint':
        need(args.restarted in ('client', 'both'), 'positive-checkpoint/slot-metadata interruption requires genuine owner process restart')
    if case == 'before2':
        need(args.restarted in ('server', 'both'), 'cold owner2/nativeBefore requires actual host process loss')
    for name, runner in (('client', client), ('server', server)):
        changed = topology[name]['processIdentity'] != report['topology'][name]['processIdentity']
        need(changed == (args.restarted in (name, 'both')), 'actual authenticated process restart differs: ' + name)
        for field in ('worldKey', 'ownerKey', 'creationId'):
            need(topology[name][field] == report['topology'][name][field], 'original native identity changed across process restart: ' + field)
    if case in ('before2unknown', 'before2consumed'):
        need(args.restarted in ('server', 'both'), 'actual host intent context must be lost')
        def retained_before_two():
            local, authority, permission = pair(client, server, operation)
            need(local['receipt'] == 2 and local['actor'] == permission.findtext('afterActor') and local['client']['held'], 'already refunded actual owner must remain held without another refund')
            need(record(authority, 'Mutation')['phase'] == 'Uncertain', 'existing Uncertain intent must never re-enter native mutation')
            if case == 'before2consumed':
                need(authority['server'] is None and authority['archive'] and authority['archive']['BeforeUncertain'] and authority['archive']['loaded'] and authority['archive']['held'] and authority['nativeLoad'], 'consumed attempt must have actual loaded negative custody only, with no continuation server')
            else:
                need(authority['server'] and authority['server']['held'] and not authority['server']['QueueAttempted'] and events(authority, 'actualNativeBeforeLoad2'), 'unconsumed intent must remain held without native re-entry')
            need(authority['nativeCanonical'] == permission.findtext('beforeTile') and not events(authority, 'actualNativeQueueCompleted'), 'actual full Before reconstruction must remain held with no native queue/input replay')
            if args.restarted == 'both':
                need(events(local, 'actualOwnerHydration2') and not any(event['boundary'].startswith('actualOwnerSetter') for event in events(local)), 'native2 hydration cannot grant owner refund replay')
            return local, authority
        first = wait(retained_before_two)
        time.sleep(5)
        second = retained_before_two()
        need(first[0]['actor'] == second[0]['actor'] and first[1]['native'] == second[1]['native'], 'uncertain native intent changed over ordinary pumps')
        report.update(retained=second, status='PASS_UNCERTAIN_NATIVE_INTENT_RETAINED')
        return
    if case == 'unknown':
        need(args.restarted in ('client', 'both'), 'uncertain native-return context must actually be lost')
        def retained():
            local, authority, permission = pair(client, server, operation)
            need(local['client'] and local['client']['restartedBefore'] and local['client']['held'] and not local['client']['resumable'], 'unknown slot must retain actual hydrated owner hold')
            need(slots(local)[report['prefix']]['phase'] == 'Uncertain' and not events(local, 'actualOwnerSetter' + str(report['prefix'])), 'unknown original setter was replayed')
            need(record(authority, 'Mutation', required=False) is None and authority['nativeCanonical'] == permission.findtext('beforeTile'), 'unknown owner refund caused native queue/input mutation')
            return local, authority
        first = wait(retained)
        time.sleep(5)
        second = retained()
        need(first[0]['actor'] == second[0]['actor'] and first[1]['native'] == second[1]['native'], 'unknown hold changed over ordinary pumps')
        report['retained'] = second
        report['status'] = 'PASS_UNKNOWN_RETAINED'
        return
    hold(client, 'release', operation)
    hold(server, 'release', operation)
    report['terminal'] = wait(lambda: terminal(client, server, operation, report['baseline'], args.restarted))
    report['status'] = 'PASS_RESTART_COMPLETION'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=('start', 'resume'))
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path, required=True)
    parser.add_argument('--server-session', type=Path, required=True)
    parser.add_argument('--case', choices=('normal', 'receipt1', 'checkpoint', 'before2', 'before2unknown', 'before2consumed', 'receipt2', 'receipt3', 'unknown', 'custody'))
    parser.add_argument('--restarted', choices=('client', 'server', 'both', 'none'), default='none')
    args = parser.parse_args()
    report = {'status': 'AUTHORED_NOT_RUN'} if args.phase == 'start' else json.loads(args.report.read_text(encoding='utf-8'))
    client = Runner(report, args.report, session_path=args.session)
    server = Runner(report, args.report, session_path=args.server_session)
    try:
        need(args.phase != 'start' or args.case, 'select an explicit actual boundary case')
        run(args, report, client, server, json.loads(args.plan.read_text(encoding='utf-8-sig')))
    except Exception as error:
        report.update(status='FAIL', failure=type(error).__name__ + ': ' + str(error))
        raise
    finally:
        client.save()
