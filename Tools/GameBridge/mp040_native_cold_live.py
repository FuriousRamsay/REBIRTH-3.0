"""AUTHORED_NOT_RUN. Actual electrical tool input and original native cold continuation.

PLAN REPORT --owner-session FILE --server-session FILE [--local]. Capture ends
at a real durable original checkpoint. External restart/reconnect is performed
outside this driver; resume requires the capture report. No process control,
inventory writes, fabricated receipts, direct service call or recovery simulation.
"""
import argparse
import hashlib
import json
import re
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from mp010_pull_lifecycle_live import topology, require_peers
from mpC_action_payment_live import strings, xml


def command(runner, verb):
    return runner.call('/console', 'POST', cmd='rbactionpaymentscenario ' + verb)


def stable(runner, initial, player=None):
    current = topology(runner, player)
    for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
        need(current.get(key) == initial.get(key), 'actual invocation changed native scope: ' + key)
    return current


def original(directory, operation):
    prepared = xml(directory / '000000-prepared.xml')
    text = prepared.findtext('original')
    import xml.etree.ElementTree as ET
    image = ET.fromstring(text)
    need(image.attrib.get('operation') == operation and image.attrib.get('action') == 'electrical-service', 'wrong actual original')
    return image, hashlib.sha256(text.encode('utf-8')).hexdigest()


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    save = Path(plan['saveDirectory'])
    need(save.name == 'CodexTest', 'actual disposable save required')
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0, 'observed authority actor required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    host = Runner(report, Path(args.report), session_path=args.server_session)
    owner_scope, host_scope = topology(owner), topology(host, plan['serverPlayerId'])
    require_peers(owner_scope, host_scope, args.local)
    need(type(owner_scope.get('deaths')) is int and type(host_scope.get('deaths')) is int, 'native life observation required')
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    timeout = min(180, max(10, float(plan.get('timeoutSeconds', 90))))
    if plan['phase'] == 'capture':
        target = plan['target']
        position = '%d %d %d' % tuple(int(target[key]) for key in ('x', 'y', 'z'))
        stage = plan['checkpoint']
        need(stage in ('target-before-saved', 'paid', 'publication-returned', 'service-after-saved', 'target-after-saved', 'used', 'returned'), 'named positive native checkpoint required')
        actions = plan.get('actions')
        need(actions and any(action['endpoint'] == '/press' for action in actions), 'real electrical tool input required')
        pending = save / 'RebirthData' / 'ActionPaymentPending'
        known = {path.stem for path in pending.glob('*.original')} if pending.exists() else set()
        need(len(known) <= 32, 'pending original discovery exceeds bound')
        command(owner, 'remote-restart-hold')
        response = command(host, 'watch %s %d %s' % (position, plan['serverPlayerId'], stage))
        need(any('[MP040 WATCH] armed' in text for text in strings(response)), 'actual native observer unavailable')
        try:
            for action in actions:
                endpoint, values = action['endpoint'], action.get('args', {})
                need(endpoint in ('/lookat', '/select', '/press', '/release') and not values.get('hold'), 'bounded native input only')
                if endpoint == '/press':
                    need(values.get('action') == 'Primary' and 0 < float(values.get('seconds', .2)) <= 2, 'bounded native tool Primary required')
                owner.call(endpoint, 'POST', **values)
                stable(owner, owner_scope)
                stable(host, host_scope, plan['serverPlayerId'])
            deadline, operation = time.monotonic() + timeout, None
            while operation is None:
                matches = []
                for path in pending.glob('*.original'):
                    if path.stem in known:
                        continue
                    need(re.fullmatch(r'[0-9a-f]{32}', path.stem) is not None, 'invalid pending operation')
                    directory = save / 'RebirthData' / 'ActionPayments' / path.stem
                    image, digest = original(directory, path.stem)
                    target_image = image.find('electrical')
                    if (int(image.attrib['nativeId']) == host_scope['entityId'] and image.attrib['creation'] == host_scope['creationId']
                            and all(int(target_image.attrib[key]) == int(target[key]) for key in ('x', 'y', 'z'))):
                        head = xml(directory / 'current.xml')
                        if head.attrib.get('stage') == stage:
                            matches.append((path.stem, image, digest))
                need(len(matches) <= 1, 'ambiguous actual native original')
                if matches:
                    operation, image, digest = matches[0]
                    break
                stable(owner, owner_scope)
                stable(host, host_scope, plan['serverPlayerId'])
                need(time.monotonic() < deadline, 'native input did not reach actual checkpoint')
                time.sleep(.25)
        finally:
            command(host, 'unwatch')
        rows = list(image.find('sources'))
        need({int(row.attrib['kind']) for row in rows} == set(plan['expectedKinds']), 'actual rich source cohort differs')
        if plan.get('mixed'):
            need(len(rows) > 1 and len({row.attrib['kind'] for row in rows}) > 1, 'actual mixed source original required')
        report.update(status='EXTERNAL_CHECKPOINT', operation=operation, originalDigest=digest, checkpoint=stage,
                      ownerTopology=owner_scope, hostTopology=host_scope, sourceKinds=sorted({int(row.attrib['kind']) for row in rows}),
                      ownerContribution=image.find('ownerOriginal') is not None)
        return
    need(plan['phase'] == 'resume', 'capture or resume required')
    prior = json.loads(Path(plan['captureReport']).read_text(encoding='utf-8-sig'))
    need(prior.get('status') == 'EXTERNAL_CHECKPOINT', 'preserved real capture report required')
    operation = prior['operation']
    need(re.fullmatch(r'[0-9a-f]{32}', operation) is not None, 'preserved actual operation required')
    image, digest = original(save / 'RebirthData' / 'ActionPayments' / operation, operation)
    need(digest == prior['originalDigest'], 'cold continuation changed immutable original')
    for current, old in ((owner_scope, prior['ownerTopology']), (host_scope, prior['hostTopology'])):
        for key in ('worldKey', 'ownerKey', 'creationId'):
            need(current[key] == old[key], 'resume changes native original scope: ' + key)
    kind = plan['resumeKind']
    need(kind in ('cold-both', 'cold-owner', 'reconnect', 'later-life'), 'explicit real continuation kind required')
    if kind == 'later-life':
        need(owner_scope['deaths'] > prior['ownerTopology']['deaths'] and host_scope['deaths'] > prior['hostTopology']['deaths'], 'actual later life required')
        need(prior['checkpoint'] in ('paid', 'publication-returned', 'service-after-saved', 'target-after-saved', 'used', 'returned'), 'new-life financial admission forbidden')
    else:
        need(owner_scope['deaths'] == prior['ownerTopology']['deaths'] and host_scope['deaths'] == prior['hostTopology']['deaths'], 'life change needs explicit later-life continuation')
        need(owner_scope['processIdentity'] != prior['ownerTopology']['processIdentity'] or owner_scope['worldInstance'] != prior['ownerTopology']['worldInstance'], 'real changed owner session required')
    if kind == 'cold-both':
        need(host_scope['processIdentity'] != prior['hostTopology']['processIdentity'], 'real authority cold process required')
    completed = prior['checkpoint'] in ('used', 'returned')
    verb = ('local-completed-restart' if args.local else 'remote-completed-restart') if completed else 'remote-restart-verify'
    if completed and not prior['ownerContribution']:
        verb = 'ownerless-completed-restart'
    response = command(host, verb + ' ' + operation)
    need(any('[MP040 PASS] ' + verb in text for text in strings(response)), 'actual native loaded original assertions did not pass')
    stable(owner, owner_scope)
    stable(host, host_scope, plan['serverPlayerId'])
    if not completed:
        response = command(host, 'remote-restart-release ' + operation)
        need(any('[MP040 PASS] remote-restart-release' in text for text in strings(response)), 'actual qualified original did not release')
        directory = save / 'RebirthData' / 'ActionPayments' / operation
        deadline = time.monotonic() + timeout
        while not list(directory.glob('*-used.xml')):
            stable(owner, owner_scope)
            stable(host, host_scope, plan['serverPlayerId'])
            need(time.monotonic() < deadline, 'existing saved original did not finish')
            time.sleep(.25)
    _, final_digest = original(save / 'RebirthData' / 'ActionPayments' / operation, operation)
    need(final_digest == digest, 'native completion changed original')
    # A used source head or restored terminal courier is not final release.
    # Observe actual owner live/saved and authority accepted/saved metadata
    # removal, with no pending native financial/terminal graph on either peer.
    deadline = time.monotonic() + timeout
    while True:
        stable(owner, owner_scope)
        stable(host, host_scope, plan['serverPlayerId'])
        owner_release = command(owner, 'terminal-released ' + operation)
        host_release = command(host, 'terminal-released %s %d' % (operation, plan['serverPlayerId']))
        report['terminalRelease'] = {'owner': owner_release, 'authority': host_release}
        if all(any('[MP040 PASS] terminal-released' in text for text in strings(value)) for value in (owner_release, host_release)):
            break
        need(time.monotonic() < deadline, 'actual native terminal metadata/custody not released')
        time.sleep(.3)
    report.update(status='PASS', operation=operation, originalDigest=digest, resumeKind=kind,
                  scope='Actual native input plus existing production native-load/original assertions; no process control or native invocation counter trace beyond compiled scenario scope')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
    parser.add_argument('--local', action='store_true')
    args = parser.parse_args()
    report = {'status': 'RUNNING', 'finding': 'MP040'}
    try:
        run(args, report)
    except Exception as error:
        report.update(status='FAIL', error=str(error))
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
