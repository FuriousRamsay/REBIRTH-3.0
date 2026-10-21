"""MP044 actual native hire scenarios; AUTHORED_NOT_RUN.

start PLAN REPORT --case normal|ready-debit|ready-owner|ready-refund|terminal|uncertain
resume PLAN REPORT

Uses real observed UI hire actions and readonly production original/native witnesses.
Operator owns actual disposable process crash/restart; this driver never builds,
launches, kills, edits receipts, injects inventory, or calls a native financial setter.
Restart cases require the real dog already present in the disposable saved world.
"""
import argparse
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED

PREFIX = '[REBIRTH dog hire scenario] '
CUT = {'normal': 'none', 'ready-debit': 'before-debit-permit',
       'ready-owner': 'before-ownership-permit', 'ready-refund': 'before-refund-permit',
       'terminal': 'before-terminal', 'uncertain': 'before-native-debit'}
HEAD = {'ready-debit': 'ReadyDebit', 'ready-owner': 'ReadyOwnership',
        'ready-refund': 'ReadyRefund', 'terminal': 'OwnedSaved', 'uncertain': 'UncertainDebit'}


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def console(runner, command):
    response = runner.call('/console', 'POST', cmd=command)
    matches = {text.split(PREFIX, 1)[1] for text in strings(response) if PREFIX in text}
    need(len(matches) == 1, 'one complete actual production observation required')
    result = json.loads(matches.pop())
    need(result.get('available'), 'reviewed DEBUG CodexTest producer required')
    return result


def observe(runner, stable):
    result = console(runner, 'rbdoghireaudit stable=' + stable)
    need(result['stable'] == stable and len(result['originals']) == 1,
         'fresh disposable case must contain exactly one complete hire original')
    row = result['originals'][0]
    original = ET.fromstring(row['original'])
    need(original.tag == 'dogHireOriginal' and original.attrib['dog'] == stable,
         'full original native payment identity mismatch')
    sources = original.findall('source')
    need(sources and sum(int(s.attrib['beforeCount']) - int(s.attrib['afterCount']) for s in sources)
         == int(original.attrib['price']), 'complete original source cost mismatch')
    need(all(s.findtext('beforeItem') and s.findtext('afterItem') for s in sources),
         'full native compensation items required')
    return result, row, original


def wait(check):
    deadline = time.monotonic() + 120
    while True:
        try:
            return check()
        except AssertionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(.4)


def terminal(runner, report, restarted):
    result, row, original = observe(runner, report['stable'])
    refund = report['case'] == 'ready-refund'
    stage = 'refunded.xml' if refund else 'committed.xml'
    need(stage in row['stages'] and not row['held'] and not row['warm'],
         'actual durable terminal must release original custody')
    price = int(original.attrib['price'])
    need(row['meat'] == report['baseline']['meat'] - (0 if refund else price),
         'payment did not resolve exactly once')
    need(row['original'] == report['original'], 'cold recovery replaced the immutable financial original')
    if refund:
        need(row['belt'] == report['baseline']['belt'] and row['bag'] == report['baseline']['bag'],
             'refund changed full native item metadata or unrelated inventory rows')
    else:
        need(row['ownership'] == 'Player' and row['owner'] == original.attrib['owner'],
             'terminal paid owner differs from the original')
    if restarted:
        need(result['process'] != report['process'], 'genuine actual process restart required')
        need(row['nativeOwner'] and row['nativeOwner']['Loaded'] and row['nativeOwner']['ReadReturned']
             and row['nativeOwner']['Hydrated'] and row['nativeDog'],
             'actual primary native Read/Load/ToPlayer and RBD1 reconstruction required')
        need(row['nativeDog']['stable'] == report['stable'], 'another native body cannot settle this original')
    return result, row


def run(args, runner, report, plan):
    if args.phase == 'start':
        need(args.case in CUT, 'explicit actual boundary case required')
        baseline = console(runner, f"rbdoghirefixture owner={int(plan['owner'])} dog={int(plan['dog'])} "
                           f"boundary={CUT[args.case]} confirm=CodexTest")
        report.update(case=args.case, stable=baseline['stable'], baseline=baseline,
                      process=baseline['process'], status='RUNNING')
        need(plan.get('hire_actions'), 'observed real UI hire input required')
        for action in plan['hire_actions']:
            need(action['endpoint'] in ALLOWED, 'native real-input endpoint required')
            runner.guard_idle()
            runner.call(action['endpoint'], 'POST', **action.get('args', {}))
            time.sleep(min(5, max(0, action.get('wait', .25))))
        def prepared():
            result, row, original = observe(runner, report['stable'])
            if args.case != 'normal':
                need(row['control'] == HEAD[args.case] and row['held'], 'actual purpose-specific native cut not reached')
            report['original'] = row['original']
            return result, row
        initial = wait(prepared)
        report['cut'] = initial
        if args.case == 'normal':
            report['terminal'] = wait(lambda: terminal(runner, report, False))
            report['status'] = 'PASS_NORMAL'
        else:
            report['status'] = 'PAUSED_FOR_REAL_DISPOSABLE_CRASH_RESTART'
        return
    need(report['status'] == 'PAUSED_FOR_REAL_DISPOSABLE_CRASH_RESTART', 'actual saved cut report required')
    if report['case'] == 'uncertain':
        def retained():
            result, row, original = observe(runner, report['stable'])
            need(result['process'] != report['process'], 'genuine process restart required')
            need(row['original'] == report['original'] and row['control'] == 'UncertainDebit' and row['held'],
                 'uncertain original was reinterpreted or replayed')
            need('committed.xml' not in row['stages'] and 'refunded.xml' not in row['stages'],
                 'unknown native attempt fabricated terminal settlement')
            need(row['belt'] == report['baseline']['belt'] and row['bag'] == report['baseline']['bag'],
                 'unknown native intent altered restored native owner inventory')
            need(row['nativeOwner'] and row['nativeOwner']['ReadReturned'] and row['nativeOwner']['Hydrated']
                 and row['nativeDog'], 'negative hold must still use real native reconstruction')
            return result, row
        first = wait(retained)
        time.sleep(5)
        second = retained()
        need(first[1]['original'] == second[1]['original'] and first[1]['belt'] == second[1]['belt']
             and first[1]['bag'] == second[1]['bag'], 'ordinary pumping replayed uncertain native intent')
        report.update(status='PASS_UNCERTAIN_RETAINED', retained=second)
    else:
        report['terminal'] = wait(lambda: terminal(runner, report, True))
        report['status'] = 'PASS_RESTART_SETTLEMENT'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=('start', 'resume'))
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path, required=True)
    parser.add_argument('--case', choices=tuple(CUT))
    args = parser.parse_args()
    report = {'status': 'AUTHORED_NOT_RUN'} if args.phase == 'start' else json.loads(args.report.read_text(encoding='utf-8'))
    runner = Runner(report, args.report, session_path=args.session)
    try:
        run(args, runner, report, json.loads(args.plan.read_text(encoding='utf-8-sig')))
    except Exception as error:
        report.update(status='FAIL', failure=type(error).__name__ + ': ' + str(error))
        raise
    finally:
        runner.save()
