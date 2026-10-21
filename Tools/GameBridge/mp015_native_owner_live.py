"""Deferred MP015 actual native owner verifier; NOT_RUN. No launch/build/setup.

PLAN REPORT --session VIEWER [--owner-session OWNER] [--server-session HOST]
Plan actions use real Bridge inputs, each tagged session=viewer|owner|server.
Plan stage=early checks saved native owner reward before cached physical output;
stage=terminal requires the independently saved native-only owner terminal file.
Caller supplies actual topology/earlier native enqueue and native PDS save flow.
"""
import argparse
import base64
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp015_completion_live import observe


def native_files(row, operation=None):
    result = {}
    for item in row['rows']:
        if item['area'] != 'nativeOwners':
            continue
        image = ET.fromstring(item['xml'])
        op = image.attrib['operation']
        if operation is None or op == operation:
            result.setdefault(op, []).append(image)
    return result


def full(node, name):
    text = node.findtext(name)
    need(text, 'full actual native image missing: ' + name)
    data = base64.b64decode(text, validate=True)
    need(data and base64.b64encode(data).decode() == text, 'noncanonical native image: ' + name)
    return data


def run(args, report, sessions, plan):
    before = observe(sessions['server'])
    known = set(native_files(before))
    need(plan['actions'], 'actual native input flow required')
    for action in plan['actions']:
        need(action['endpoint'] in ALLOWED, 'only native input actions allowed')
        sessions[action.get('session', 'viewer')].call(action['endpoint'], 'POST', **action.get('args', {}))
        time.sleep(min(5, max(0, action.get('wait', .3))))
    deadline = time.monotonic() + 120
    while True:
        server = observe(sessions['server'])
        operations = set(native_files(server)) - known
        operation = plan.get('operation')
        if operation is None:
            need(len(operations) <= 1, 'prepare one actual original application')
            operation = next(iter(operations), None)
        report.setdefault('observations', []).append(server)
        sessions['viewer'].save()
        try:
            need(operation, 'actual typed native Add original missing')
            exact = observe(sessions['server'], operation)
            images = native_files(exact, operation)[operation]
            sources = [image for image in images if image.find('originalQueueOwner') is not None]
            need(len(sources) == 1, 'one immutable earlier native-owner source required')
            source = sources[0]
            owner = source.find('originalQueueOwner')
            need(owner.attrib['owner'] == plan['sourceOwner'] and owner.attrib['creation'] == plan['sourceCreation'], 'original queued owner changed')
            native_before = source.find('before/nativeOwningBefore')
            need(native_before is not None and native_before.attrib['operation'] == operation, 'actual owning before is not this operation')
            need(owner.findtext('recipeBinding') and native_before.findtext('recipeFull'), 'full original recipe sidecars missing')
            if plan.get('stage', 'terminal') == 'early':
                owner_rows = observe(sessions['owner'], operation)['nativeOwners']
                matches = [row for row in owner_rows if row['operation'] == operation]
                need(len(matches) == 1, 'actual retained native owner observation missing')
                row = matches[0]
                need(row['consumed'] and row['nativeSavedEvent'] and not row['delivered'], 'real native saved reward before physical output required')
                need(row['recipient'] == plan['recipient'] and row['recipientCreation'] == plan['recipientCreation'], 'native aggregate recipient changed')
            else:
                terminals = [image.find('nativeOwnerTerminal') for image in images if image.find('nativeOwnerTerminal') is not None]
                need(len(terminals) == 1, 'positive independent native owner terminal primary required')
                terminal = terminals[0]
                need(terminal.attrib['recipient'] == plan['recipient'] and terminal.attrib['creation'] == plan['recipientCreation'], 'saved native recipient changed')
                need(len(base64.b64decode(terminal.attrib['nativeFileDigest'], validate=True)) == 32, 'actual saved PDS digest missing')
                for name in ('ownerBefore', 'ownerAfter', 'bagBefore', 'bagAfter', 'beltBefore', 'beltAfter', 'beforeComplete', 'afterComplete', 'aggregate'):
                    full(terminal, name)
                delivery = terminal.find('delivery/nativeOwningDelivery')
                need(delivery is not None and delivery.attrib['operation'] == operation, 'saved owner event lacks actual physical delivery')
                need(int(delivery.attrib['beforeCount']) - int(delivery.attrib['afterCount']) == 1, 'actual physical application decrement differs')
                need(delivery.attrib['nativeOriginalAccepted'] == 'true' or delivery.attrib['nativeChildrenAccepted'] == 'true', 'real accepted native insertion missing')
                members = {member.attrib['operation']: dict(member.attrib) for member in terminal.findall('members/member')}
                for expected in plan['members']:
                    key = operation if expected['operation'] == '$operation' else expected['operation']
                    need(key in members, 'native aggregate contribution missing')
                    for field in ('kind', 'source', 'creation', 'contribution'):
                        need(members[key][field] == str(expected[field]), 'native contribution changed: ' + field)
                need(len(members) == len(plan['members']), 'unrepresented native aggregate membership')
            report.update(status='PASS', operation=operation, source=ET.tostring(source, encoding='unicode'))
            return
        except (AssertionError, KeyError):
            if time.monotonic() >= deadline:
                raise
            time.sleep(.5)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path)
    parser.add_argument('--owner-session', type=Path)
    parser.add_argument('--server-session', type=Path)
    args = parser.parse_args()
    report = {'status': 'NOT_RUN'}
    viewer = Runner(report, args.report, session_path=args.session)
    owner = Runner(report, args.report, session_path=args.owner_session) if args.owner_session else viewer
    server = Runner(report, args.report, session_path=args.server_session) if args.server_session else viewer
    try:
        run(args, report, {'viewer': viewer, 'owner': owner, 'server': server}, json.loads(args.plan.read_text(encoding='utf-8')))
    except Exception as error:
        report.update(status='FAIL', failure=type(error).__name__ + ': ' + str(error))
        raise
    finally:
        viewer.save()
