"""Deferred actual native MP015 driver; NOT_RUN. Never launches/resets/installs.

start PLAN REPORT [--hold skill|statistics|reagent|acceptedAck]
resume PLAN REPORT [--cold]
PLAN uses observed real UI input actions and exact original owner/creation/viewer/
viewerCreation/recipe/count and observed serverPlayerId for the native viewer.
--server-session is required for a remote viewer; --local explicitly qualifies
host-local evidence. --cold-scope owner|server|both defaults to both and requires
actual changed process identities, preserving original world/character/life.
The caller owns setup, genuine disconnect/reconnect and ordinary process reload.
"""
import argparse
import json
import time
import xml.etree.ElementTree as ET
import base64
import hashlib
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers

PREFIX = '[REBIRTH MP015 audit] '


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)
    elif isinstance(value, dict):
        for item in value.values():
            yield from strings(item)


def command(runner, text):
    return runner.call('/console', 'POST', cmd=text)


def observe(runner, operation=None):
    topology = runner.call('/auditstate', domain='topology')['domains']['topology']
    need(topology.get('gameName') == 'CodexTest', 'actual disposable CodexTest required')
    response = command(runner, 'rbwccompletionaudit' + (' operation=' + operation if operation else ''))
    images = {text.split(PREFIX, 1)[1] for text in strings(response) if PREFIX in text}
    need(len(images) == 1, 'exact read-only original audit reply required')
    row = json.loads(images.pop())
    need(row['available'] is True, 'completion audit unavailable')
    for item in row['rows']:
        need(item['error'] is None and not item['temporary'], 'uncertain durable primary must remain held')
    return row


def originals(row):
    result = {}
    for item in row['rows']:
        if item['area'] not in ('pending', 'archive'):
            continue
        state = ET.fromstring(item['xml'])
        original = state.find('original/workstationCompletionOriginal')
        need(original is not None and state.tag == 'workstationCompletionOutbox', 'actual outbox schema required')
        op = original.attrib['operation']
        need(op == item['operation'] and op not in result, 'ambiguous original custody')
        result[op] = (state, original, item)
    return result


def check(row, operation, plan, terminal):
    entries = originals(row)
    need(operation in entries, 'same durable original missing')
    state, original, item = entries[operation]
    for field in ('owner', 'creation', 'viewer', 'viewerCreation', 'recipe'):
        need(original.attrib[field] == plan[field], 'original attribution changed: ' + field)
    need(int(original.attrib['count']) == plan['count'], 'original full recipe count differs')
    delivery = original.find('delivery/nativeOwningDelivery')
    need(delivery is not None and delivery.attrib['operation'] == operation, 'native delivery operation differs')
    need(int(delivery.attrib['beforeCount']) - int(delivery.attrib['afterCount']) == 1, 'actual card decrement must be one')
    need(delivery.attrib['nativeOriginalAccepted'] == 'true' or delivery.attrib['nativeChildrenAccepted'] == 'true', 'actual native accepted insertion required')
    need(delivery.attrib['sink'] == plan['sink'], 'wrong actual native output path')
    for name in ('recipeNative', 'primaryNative', 'extrasNative', 'outputBefore', 'outputAfter', 'bagBefore', 'bagAfter', 'beltBefore', 'beltAfter'):
        need(delivery.find(name) is not None and delivery.find(name).text, 'full native image missing: ' + name)
    if row['role'] == 'server':
        intents = [ET.fromstring(value['xml']) for value in row['rows'] if value['operation'] == operation and value['area'] in ('intents', 'intentArchive')]
        need(len(intents) == 1, 'exact durable native source intent required')
        accepted = intents[0].find('tileAccepted')
        need(accepted is not None and accepted.findtext('queueBefore') and accepted.findtext('queueAfter'), 'actual native queue sync/upload proof required')
        need(accepted.findtext('queueBefore') != accepted.findtext('queueAfter'), 'native queue was not decremented')
        need(accepted.findtext('outputAfter') == delivery.findtext('outputAfter'), 'native accepted physical image differs')
        if plan['sink'] == 'Inventory':
            inventory = intents[0].find('inventoryAccepted')
            need(inventory is not None and inventory.findtext('bagAfter') == delivery.findtext('bagAfter') and inventory.findtext('beltAfter') == delivery.findtext('beltAfter'), 'accepted native inventory proof differs')
        if terminal:
            need(item['area'] == 'archive' and all(state.attrib[key] == 'true' for key in ('skill', 'statistics', 'decision', 'terminal')), 'independent original stages not durably terminal')
            if state.attrib['reagent']:
                need(state.attrib.get('reagentPublished') == 'true', 'actual saved native reagent publication required')
                native_proof(row, operation, state)
    elif terminal:
        need(item['area'] == 'archive' and state.attrib['acknowledged'] == 'true', 'viewer ACK custody not durable')
    return {'digest': state.attrib['witness'], 'original': ET.tostring(original, encoding='unicode'), 'stage': dict(state.attrib), 'area': item['area']}


def native_proof(row, operation, state):
    proofs = [ET.fromstring(value['xml']) for value in row['rows'] if value['operation'] == operation and value['area'] == 'nativeReagent']
    need(len(proofs) == 1, 'one independently durable saved-native reagent original required')
    proof = proofs[0].find('before/nativeReagentPublication')
    need(proof is not None and proof.attrib['operation'] == operation and proof.attrib['witness'] == state.attrib['witness'], 'saved native proof original differs')
    need(proof.attrib['reagent'] == state.attrib['reagent'] and proof.attrib['after'] == hashlib.sha256(state.findtext('reagentAfter').encode('utf-8')).hexdigest(), 'native reagent physical after binding differs')
    need(int(proof.attrib['generation']) > 0 and len(proof.attrib['payload']) == 64 and proof.attrib['worldState'], 'actual snapshot/publication lineage missing')
    for name in ('input', 'queue', 'output', 'completion'):
        span = proof.find(name)
        need(span is not None and int(span.attrib['offset']) >= 8 and 0 < len(base64.b64decode(span.text, validate=True)) <= 256 * 1024, 'actual native serialized span missing: ' + name)
    need(base64.b64decode(proof.findtext('nativeBody'), validate=True), 'full native original body missing')
    actual = row.get('native')
    need(actual and actual.get('available') is True and actual['operation'] == operation and actual['worldState'] == proof.attrib['worldState'], 'actual loaded native body observation required')
    need(actual['output'] == state.findtext('reagentAfter') and base64.b64decode(actual['body'], validate=True), 'actual loaded full native output changed or material replayed')
    return ET.tostring(proof, encoding='unicode')


def run(args, report, client, server, plan):
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0,
         'observed server-side native viewer entity required')
    viewer_scope = topology(client)
    server_scope = topology(server, plan['serverPlayerId'])
    require_peers(viewer_scope, server_scope, args.local)
    need(viewer_scope['ownerKey'] == plan['viewer'] and viewer_scope['creationId'] == plan['viewerCreation'],
         'native delivery viewer must match planned attribution; reward recipient may differ')
    for scope in (viewer_scope, server_scope):
        need(scope.get('processIdentity') and scope.get('worldInstance') and type(scope.get('deaths')) is int,
             'actual process/world/native life evidence required')
    need(client.call('/guard')['enabled'] is True, 'native instincts must remain enabled')
    client.call('/surroundings')
    def stable_scope():
        for runner, initial, player in ((client, viewer_scope, None), (server, server_scope, plan['serverPlayerId'])):
            current = topology(runner, player)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current.get(key) == initial.get(key), 'native invocation scope changed: ' + key)
    if args.phase == 'start':
        before = observe(client)
        report['before'] = before
        report['sessions'] = {'client': client.scope, 'server': server.scope}
        report['nativeTopology'] = {'viewer': viewer_scope, 'server': server_scope, 'local': args.local}
        known = set(originals(before))
        if args.hold:
            command(server, 'rbwccompletionfixture ' + args.hold + ' confirm=CodexTest')
        need(plan['actions'], 'observed real native crafting inputs required')
        for action in plan['actions']:
            stable_scope()
            need(action['endpoint'] in ALLOWED, 'only native real-input actions permitted')
            client.call(action['endpoint'], 'POST', **action.get('args', {}))
            time.sleep(min(5, max(0, action.get('wait', .3))))
        deadline = time.monotonic() + 90
        while True:
            observed = observe(client)
            added = set(originals(observed)) - known
            if args.hold == 'reagentPublication':
                # Natural bonus rolls may be empty. Select only the actual held
                # positive material original; never force RNG or invent one.
                authority_row = observe(server)
                proof_ops = {value['operation'] for value in authority_row['rows'] if value['area'] == 'nativeReagent'}
                added &= proof_ops
            need(len(added) <= 1, 'prepare exactly one native completion application')
            if added:
                report['operation'] = added.pop()
                break
            need(time.monotonic() < deadline, 'no actual durable completion observed')
            time.sleep(.5)
    else:
        prior = report.get('nativeTopology')
        need(prior and prior['local'] == args.local, 'resume requires captured actual topology and same topology kind')
        for current, old in ((viewer_scope, prior['viewer']), (server_scope, prior['server'])):
            for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
                need(current[key] == old[key], 'same-original continuation changed native scope: ' + key)
        if args.cold:
            if args.cold_scope in ('owner', 'both'):
                need(viewer_scope['processIdentity'] != prior['viewer']['processIdentity'], 'actual cold native viewer process required')
            if args.cold_scope in ('server', 'both'):
                need(server_scope['processIdentity'] != prior['server']['processIdentity'], 'actual cold authority process required')
        stable_scope()
        command(server, 'rbwccompletionfixture release confirm=CodexTest')
    operation = report['operation']
    terminal = args.phase == 'resume' or not args.hold
    deadline = time.monotonic() + 90
    while True:
        stable_scope()
        client_row, server_row = observe(client, operation), observe(server, operation)
        report.setdefault('observations', []).append({'client': client_row, 'server': server_row})
        client.save()
        try:
            local = check(client_row, operation, plan, terminal)
            authority = check(server_row, operation, plan, terminal)
            need(local['digest'] == authority['digest'], 'server/client original witness differs')
            if args.hold in ('skill', 'statistics', 'reagent') and args.phase == 'start':
                key = {'skill': 'skill', 'statistics': 'statistics', 'reagent': 'decision'}[args.hold]
                need(authority['stage'][key] == 'false' and authority['area'] == 'pending', 'actual held original stage differs')
                if args.hold != 'skill':
                    need(authority['stage']['skill'] == 'true', 'positive skill receipt boundary required')
                if args.hold == 'reagent':
                    state = originals(server_row)[operation][0]
                    birth = state.attrib.get('nativeStationBirth')
                    need(isinstance(birth, str) and len(birth) == 32 and server_row.get('native', {}).get('sameNativeStation') is True,
                         'actual native station binding must be saved before deferred unattempted material')
                    report['nativeStationBirth'] = birth
            if args.phase == 'resume' and report.get('nativeStationBirth'):
                state = originals(server_row)[operation][0]
                need(state.attrib.get('nativeStationBirth') == report['nativeStationBirth']
                     and server_row.get('native', {}).get('sameNativeStation') is True,
                     'deferred material reentry changed original native station identity')
            if args.hold == 'acceptedAck' and args.phase == 'start':
                need(client_row['role'] == 'client' and local['stage']['acknowledged'] == 'false', 'actual remote lost ACK required')
            if args.hold == 'reagentPublication' and args.phase == 'start':
                state = originals(server_row)[operation][0]
                need(authority['area'] == 'pending' and state.attrib['observed'] == 'true' and state.attrib.get('reagentPublished') == 'false', 'hold must follow actual native reagent effect and saved proof but precede outbox publication ACK')
                report['nativeReagentProof'] = native_proof(server_row, operation, state)
            if args.phase == 'resume' and 'nativeReagentProof' in report:
                state = originals(server_row)[operation][0]
                need(native_proof(server_row, operation, state) == report['nativeReagentProof'], 'cold/warm recovery replaced original native publication proof')
            if 'witness' in report:
                need(report['witness']['digest'] == authority['digest'] and report['witness']['original'] == authority['original'], 'retry replaced immutable original/native witness')
            report['witness'] = authority
            break
        except AssertionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(.5)
    report['status'] = 'PASS_' + args.phase.upper()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=('start', 'resume'))
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path)
    parser.add_argument('--server-session', type=Path)
    parser.add_argument('--hold', choices=('skill', 'statistics', 'reagent', 'reagentPublication', 'acceptedAck'))
    parser.add_argument('--cold', action='store_true')
    parser.add_argument('--cold-scope', choices=('owner', 'server', 'both'), default='both')
    parser.add_argument('--local', action='store_true', help='qualify host-local evidence explicitly; never counts as remote')
    args = parser.parse_args()
    report = {'status': 'NOT_RUN'} if args.phase == 'start' else json.loads(args.report.read_text(encoding='utf-8'))
    client = Runner(report, args.report, session_path=args.session)
    server = Runner(report, args.report, session_path=args.server_session) if args.server_session else client
    try:
        run(args, report, client, server, json.loads(args.plan.read_text(encoding='utf-8')))
    except Exception as error:
        report['status'] = 'FAIL'
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        client.save()
