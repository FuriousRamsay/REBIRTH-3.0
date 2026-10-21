"""MP043 real production fixture driver; AUTHORED_NOT_RUN.
start|resume PLAN REPORT --boundary prepared|before-ingestion-permit|ingestion-intent|
before-secondary-permit|before-owner-effect-permit|before-terminal-metadata

PLAN: npcEntity, itemKey, itemType, saveDirectory (actual CodexTest), optional
ownerEntity (actual authenticated server-side remote owner), session. Operator
owns genuine process crash/restart and peer reconnect. Never launches or resets.
"""
import argparse
import json
import re
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED

HEAD = {'prepared': 'prepared', 'before-ingestion-permit': 'source-saved',
        'ingestion-intent': 'ingestion-intent', 'before-secondary-permit': 'ingestion-saved',
        'before-owner-effect-permit': 'owner-secondary-saved',
        'before-terminal-metadata': 'owner-after-saved'}


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def bounded_xml(path):
    with path.open('rb') as stream:
        need(0 < path.stat().st_size <= 16 * 1024 * 1024, 'actual journal size invalid')
        data = stream.read(16 * 1024 * 1024 + 1)
    need(len(data) <= 16 * 1024 * 1024 and b'<!DOCTYPE' not in data, 'bounded exact journal required')
    return ET.fromstring(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['start', 'resume', 'loss'])
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--boundary', choices=list(HEAD), default='before-ingestion-permit')
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding='utf-8-sig'))
    report = {'status': 'RUNNING', 'boundary': args.boundary}
    prior = json.loads(args.report.read_text(encoding='utf-8-sig')) if args.mode == 'resume' else None
    runner = Runner(report, args.report, session_path=plan.get('session'))
    topology = runner.call('/auditstate', domain='topology')['domains']['topology']
    need(topology.get('gameName') == 'CodexTest', 'actual disposable CodexTest required')
    owner = (' ' + str(int(plan['ownerEntity']))) if 'ownerEntity' in plan else ''
    root = Path(plan['saveDirectory']) / 'RebirthData'
    need(Path(plan['saveDirectory']).name == 'CodexTest', 'only actual CodexTest path supported')
    if args.mode == 'loss':
        need('ownerEntity' in plan and 'clientSession' in plan, 'actual server/owner peer sessions required')
        client_report = {'status': 'RUNNING_OWNER_LOSS'}
        client = Runner(client_report, args.report.with_suffix('.owner.json'), session_path=plan['clientSession'])
        need(client.call('/auditstate', domain='topology')['domains']['topology'].get('gameName') == 'CodexTest', 'actual disposable owner peer required')
        client.call('/console', 'POST', cmd='rbnpcconsumescenario lossclient')
        response = runner.call('/console', 'POST', cmd='rbnpcconsumescenario lossarm %d %s %d%s' % (int(plan['npcEntity']), plan['itemKey'], int(plan['itemType']), owner))
        arm = '\n'.join(text for text in strings(response) if '[MP043 ARM]' in text)
        match = re.search(r'operation\s*=\s*([0-9a-f]{32})', arm)
        need(match is not None and match.group(1) != '0' * 32, 'actual lost-courier original missing')
        operation = match.group(1)
        deadline = time.monotonic() + 60
        while True:
            directories = [root / area / operation for area in ('NpcConsumption', 'NpcConsumptionTerminal')]
            if any((directory / 'committed.xml').is_file() for directory in directories):
                break
            need(time.monotonic() < deadline, 'same original failed real packet-loss convergence')
            time.sleep(0.5)
        host = runner.call('/console', 'POST', cmd='rbnpcconsumescenario lossverifyhost %d %s%s' % (int(plan['npcEntity']), operation, owner))
        owner_reply = client.call('/console', 'POST', cmd='rbnpcconsumescenario lossverifyclient ' + operation)
        need(any('[MP043 PASS] lossverifyhost' in text for text in strings(host)) and any('[MP043 PASS] lossverifyclient' in text for text in strings(owner_reply)), 'actual no-replay native host/client assertions failed')
        report.update(operation=operation, status='PASS')
        args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
        return
    if args.mode == 'start':
        response = runner.call('/console', 'POST', cmd='rbnpcconsumescenario arm %d %s %d %s%s' %
                               (int(plan['npcEntity']), plan['itemKey'], int(plan['itemType']), args.boundary, owner))
        arm = '\n'.join(text for text in strings(response) if '[MP043 ARM]' in text)
        match = re.search(r'operation\s*=\s*([0-9a-f]{32}).*process\s*=\s*(\d+)', arm)
        need(match is not None, 'actual production original arm response required')
        operation, process = match.group(1), int(match.group(2))
        directory = root / 'NpcConsumption' / operation
        deadline = time.monotonic() + 60
        while True:
            head = bounded_xml(directory / 'control.xml')
            if head.attrib['stage'] == HEAD[args.boundary]:
                break
            need(time.monotonic() < deadline, 'actual native owner/saved boundary not reached')
            time.sleep(0.5)
        prepared = bounded_xml(directory / 'prepared.xml')
        original = ET.fromstring(prepared.findtext('original'))
        need(original.attrib['operation'] == operation and head.findtext('original') == prepared.findtext('original'),
             'checkpoint changed full original')
        report.update(operation=operation, process=process, status='AWAITING_REAL_PROCESS_RESTART')
    else:
        need(prior['status'] == 'AWAITING_REAL_PROCESS_RESTART' and prior['boundary'] == args.boundary,
             'actual prior armed report required')
        operation = prior['operation']
        response = runner.call('/console', 'POST', cmd='rbnpcconsumescenario verify %d %s %d %s%s' %
                               (int(plan['npcEntity']), operation, int(prior['process']), args.boundary, owner))
        need(any('[MP043 PASS] verify' in text for text in strings(response)),
             'actual compiled production/native fixture assertions did not pass')
        report.update(operation=operation, status='PASS')
    args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
