"""MP040 actual owning-action/native boundary fixture driver. AUTHORED_NOT_RUN.

PLAN: saveDirectory (actual CodexTest), target {x,y,z}, optional session,
fault (normal|never-entered|returned-receipt-loss|native-unobserved|
pause-prepared|exact production journal stage), timeoutSeconds.
Default mode service runs real production eligibility and actual service price.
mode native-input uses prepared actions (lookat/select/bounded Primary) to press
the actual electrical tool. A debug watcher only retains its resulting original.
mode primitive additionally requires itemKey/count and an already serviced target;
that mode isolates resource settlement, not business eligibility.
Optional expectedSourceIds/expectedKinds/mixed assert the actual selected original.
pause-prepared exercises actual native GM target pickup refusal before releasing.
No launch/reset/restart, setup substitutes, simulated acknowledgements, or tests run
by merely importing this source. Remote first-owner and cold are not certified here.
"""
import argparse
import json
import re
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def xml(path):
    with path.open('rb') as stream:
        need(0 < path.stat().st_size <= 24 * 1024 * 1024, 'actual artifact exceeds bound')
        data = stream.read(24 * 1024 * 1024 + 1)
    need(len(data) <= 24 * 1024 * 1024 and b'<!DOCTYPE' not in data, 'strict bounded original required')
    return ET.fromstring(data)


def native_operation(runner, plan, save, position, topology):
    """Observe a normal native tool click; the debug watcher only retains evidence."""
    need(plan.get('fault', 'normal') == 'normal', 'normal-input mode does not inject financial faults')
    actions = plan.get('actions')
    need(isinstance(actions, list) and actions, 'prepared native tool input actions required')
    need(any(action.get('endpoint') == '/press' for action in actions), 'native primary tool input required')
    for action in actions:
        endpoint, values = action['endpoint'], action.get('args', {})
        need(endpoint in ('/lookat', '/select', '/press', '/release'), 'only native observed tool input allowed')
        if endpoint == '/press':
            need(values.get('action') == 'Primary' and not values.get('hold') and 0 < float(values.get('seconds', .2)) <= 2,
                 'bounded actual primary input required')
    need(runner.call('/guard')['enabled'] is True, 'guard must remain enabled')
    runner.call('/surroundings')
    root = save / 'RebirthData' / 'ActionPayments'
    before = {path.name for path in root.iterdir()} if root.exists() else set()
    need(len(before) <= 4096, 'native scenario discovery exceeds bounded fixture history')
    response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario watch ' + position)
    need(any('[MP040 WATCH] armed' in text for text in strings(response)), 'native observation could not be armed')
    try:
        runner.guard_idle()
        for action in actions:
            runner.call(action['endpoint'], 'POST', **action.get('args', {}))
        current = runner.call('/auditstate', domain='topology')['domains']['topology']
        need(all(current[key] == topology[key] for key in ('processIdentity', 'worldKey', 'ownerKey', 'entityId', 'creationId')),
             'normal-input actor/world changed during native action')
        deadline = time.monotonic() + min(180, max(10, float(plan.get('timeoutSeconds', 90))))
        while True:
            found = []
            candidates = [path for path in root.iterdir() if path.name not in before] if root.exists() else []
            need(len(candidates) <= 8, 'ambiguous normal-input operation cohort')
            for candidate in candidates:
                prepared = candidate / '000000-prepared.xml'
                if not prepared.exists():
                    continue
                original = ET.fromstring(xml(prepared).findtext('original'))
                need(re.fullmatch(r'[0-9a-f]{32}', original.attrib.get('operation', '')) is not None
                     and original.attrib['operation'] == candidate.name, 'native original path/operation mismatch')
                target = original.find('electrical')
                if (target is not None and all(int(target.attrib[key]) == int(plan['target'][key]) for key in ('x', 'y', 'z'))
                        and int(original.attrib['nativeId']) == topology['entityId']
                        and original.attrib['creation'] == topology['creationId']):
                    found.append(original.attrib['operation'])
            need(len(found) <= 1, 'multiple normal-input originals matched the same owner/target')
            if found:
                return found[0]
            need(time.monotonic() < deadline, 'normal native tool did not produce a retained service original')
            time.sleep(.25)
    finally:
        runner.call('/console', 'POST', cmd='rbactionpaymentscenario unwatch')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding='utf-8-sig'))
    save = Path(plan['saveDirectory'])
    need(save.name == 'CodexTest', 'actual disposable save only')
    report = {'status': 'RUNNING', 'scope': 'MP040 actual warm action/native settlement'}
    runner = Runner(report, args.report, session_path=plan.get('session'))
    topology = runner.call('/auditstate', domain='topology')['domains']['topology']
    need(topology.get('gameName') == 'CodexTest', 'native running save is not CodexTest')
    target = plan['target']
    position = '%d %d %d' % tuple(int(target[key]) for key in ('x', 'y', 'z'))
    fault = plan.get('fault', 'normal')
    need(re.fullmatch(r'[A-Za-z0-9_-]+', fault) is not None, 'invalid actual boundary name')
    mode = plan.get('mode', 'service')
    if mode == 'native-input':
        operation = native_operation(runner, plan, save, position, topology)
    elif mode == 'service':
        command = 'rbactionpaymentscenario service ' + position
    else:
        need(plan['mode'] == 'primitive', 'unknown fixture mode')
        need(re.fullmatch(r'[A-Za-z][A-Za-z0-9_]*', plan['itemKey']) is not None, 'invalid real item key')
        need(int(plan['count']) > 0, 'positive actual quantity required')
        command = 'rbactionpaymentscenario begin %s %s %d' % (position, plan['itemKey'], int(plan['count']))
    if mode != 'native-input':
        if fault != 'normal':
            command += ' ' + fault
        response = runner.call('/console', 'POST', cmd=command)
        matches = [match.group(1) for text in strings(response) for match in re.finditer(r'\[MP040 ARM\] ([0-9a-f]{32})', text)]
        need(len(set(matches)) == 1, 'actual production original was not armed')
        operation = matches[0]
    directory = save / 'RebirthData' / 'ActionPayments' / operation
    deadline = time.monotonic() + min(180, max(10, float(plan.get('timeoutSeconds', 90))))
    prepared = directory / '000000-prepared.xml'
    while time.monotonic() < deadline and not prepared.exists():
        time.sleep(.25)
    need(prepared.exists(), 'actual original preparation did not complete')
    original_text = xml(prepared).findtext('original')
    original = ET.fromstring(original_text)
    need(original.attrib['operation'] == operation and original.attrib['action'] == 'electrical-service', 'wrong owning original')
    training = original.find('training')
    need(training is not None and training.attrib.get('receipt') == 'electrical-action:' + operation,
         'original electrical training receipt missing')
    sources = list(original.find('sources'))
    ids = {source.attrib['id'] for source in sources}
    if 'expectedSourceIds' in plan:
        need(ids == set(plan['expectedSourceIds']), 'actual producer selected different sources')
    if 'expectedKinds' in plan:
        need({int(source.attrib['kind']) for source in sources} == set(plan['expectedKinds']), 'actual participant kinds differ')
    if plan.get('mixed'):
        need(len(sources) > 1 and len({source.attrib['kind'] for source in sources}) > 1, 'actual mixed native/NPC/local plan required')
    if fault == 'pause-prepared':
        response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario head-conflict ' + operation)
        need(any('[MP040 PASS] head-conflict' in text for text in strings(response)), 'conflicting full current head allowed native payment')
        response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario escape %s -1' % operation)
        need(any('[MP040 PASS] escape' in text for text in strings(response)), 'actual native target pickup refusal did not pass')
        if 'escapeSourceIndex' in plan:
            response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario escape %s %d' % (operation, int(plan['escapeSourceIndex'])))
            need(any('[MP040 PASS] escape' in text for text in strings(response)), 'actual participant pickup refusal did not pass')
        refusals = list((directory / 'NativeRefusals').glob('*.xml'))
        need(refusals and all(xml(path).findtext('original') == original_text for path in refusals), 'refusal changed full original or omitted durable native boundary')
        runner.call('/console', 'POST', cmd='rbactionpaymentscenario release ' + operation)
    if fault in ('native-unobserved', 'candidate-count', 'candidate-metadata'):
        need(any(not source.attrib['id'].startswith('N:') for source in sources), 'actual native setter source required for lost-return fixture')
        while time.monotonic() < deadline and not list(directory.glob('*-*-debit-intent.xml')):
            time.sleep(.25)
        need(list(directory.glob('*-*-debit-intent.xml')), 'actual native attempt WAL not reached')
        response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario unknown ' + operation)
        need(any('[MP040 PASS] unknown' in text for text in strings(response)), 'actual uncertain setter retention assertion failed')
        need(not list(directory.glob('*-used.xml')) and not list(directory.glob('*-returned.xml')), 'unknown financial effect falsely terminalized')
        outcome = 'UNKNOWN_RETAINED'
    else:
        stage = 'returned' if fault == 'never-entered' else 'used'
        while time.monotonic() < deadline and not list(directory.glob('*-' + stage + '.xml')):
            time.sleep(.25)
        need(list(directory.glob('*-' + stage + '.xml')), 'actual coupled saved outcome did not complete')
        verb = 'returned' if stage == 'returned' else 'verify'
        response = runner.call('/console', 'POST', cmd='rbactionpaymentscenario %s %s' % (verb, operation))
        need(any('[MP040 PASS] ' + verb in text for text in strings(response)), 'compiled full native images/one-invocation/saved outcome assertions failed')
        outcome = stage.upper()
        if stage == 'used':
            training_stage = 'training-saved' if training.attrib['required'] == 'true' else 'training-not-required'
            training_saved = list(directory.glob('*-' + training_stage + '.xml'))
            need(len(training_saved) == 1, 'saved original training event missing or duplicated')
            need(ET.tostring(xml(training_saved[0]).find('training')) == ET.tostring(training),
                 'saved training changed the original reward')
    events = [path for path in directory.glob('*.xml') if re.fullmatch(r'\d{6}-.+\.xml', path.name)]
    need(0 < len(events) <= 256, 'causal event count invalid')
    need(all(xml(path).findtext('original') == original_text for path in events), 'financial retry changed immutable full original')
    latest = max(events, key=lambda path: int(path.name[:6]))
    need((directory / 'current.xml').read_bytes() == latest.read_bytes(), 'latest issued permission head differs from full actual event')
    report.update(status='PASS', operation=operation, outcome=outcome, fault=fault, mode=mode,
                  sources=sorted(ids), tests='ACTUAL_COMPILED_FIXTURE_ASSERTIONS',
                  remainingScope=['remote native owner first grant', 'cold original reconstruction'])
    args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
