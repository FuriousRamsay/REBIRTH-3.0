"""MP044 real two-peer owner-slot hire scenarios. AUTHORED_NOT_RUN.

PLAN supplies owner, dog and observed hire_actions for the actual remote client.
Both native games must use disposable CodexTest saves and reviewed DEBUG source.
This driver never constructs a payment grant, changes inventory, edits originals,
builds, launches, kills, or substitutes a native boundary result.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mpC_dog_hire_live import console, observe, strings, wait, CUT, HEAD


def client_observe(runner, hold=None):
    command = 'rbdoghireclientaudit' if hold is None else (
        'rbdoghireclienthold boundary=' + hold + ' confirm=CodexTest')
    response = runner.call('/console', 'POST', cmd=command)
    prefix = '[REBIRTH dog hire client scenario] '
    frames = {text.split(prefix, 1)[1] for text in strings(response) if prefix in text}
    need(len(frames) == 1, 'one complete native owner-client observation required')
    frame = json.loads(frames.pop())
    need(frame.get('available') and frame.get('remote'), 'actual DEBUG CodexTest remote client required')
    return frame


def actual_hire(client, plan):
    need(plan.get('hire_actions'), 'actual observed Hire UI inputs required')
    for action in plan['hire_actions']:
        need(action['endpoint'] in ALLOWED, 'only native real-input actions accepted')
        client.guard_idle()
        client.call(action['endpoint'], 'POST', **action.get('args', {}))
        time.sleep(min(5, max(0, action.get('wait', .25))))


def run(host, client, plan, report, case):
    baseline = client_observe(client, 'after-debit-native' if case == 'lost-publication' else 'none')
    need(not baseline['held'] and baseline['actor'] == int(plan['owner']), 'fresh actual remote original required')
    host_cut = CUT[case[len('restart-'):]] if case.startswith('restart-') else ('before-refund-permit' if case == 'refund' else 'none')
    setup = console(host, f"rbdoghirefixture owner={int(plan['owner'])} dog={int(plan['dog'])} "
                    f"boundary={host_cut} confirm=CodexTest")
    report.update(case=case, baseline=baseline, setup=setup)
    actual_hire(client, plan)

    if case.startswith('restart-'):
        cut = case[len('restart-'):]
        def stopped():
            result, row, _ = observe(host, setup['stable'])
            owner = client_observe(client)
            need(row['control'] == HEAD[cut] and row['held'] and owner['held'], 'actual original first-permit/save cut not reached')
            need(owner['nativeDebits'] == (0 if cut == 'ready-debit' else 1), 'unexpected native remote debit count at actual cut')
            return result, row, owner
        report.update(cut=wait(stopped), status='PAUSED_FOR_REAL_BOTH_PROCESS_CRASH_RESTART')
        return

    if case == 'lost-publication':
        def retained():
            owner = client_observe(client)
            _, row, original = observe(host, setup['stable'])
            need(owner['held'] and owner['current'] and owner['debited'] and owner['nativeDebits'] == 1,
                 'actual client setter return must remain held without its publication')
            need(owner['meat'] == baseline['meat'] - int(original.attrib['price']), 'actual native owner debit missing')
            need(row['control'] == 'UncertainDebit' and row['held'] and row['ownership'] == 'None',
                 'host treated a missing client native receipt/PDS as permission')
            need('committed.xml' not in row['stages'] and 'refunded.xml' not in row['stages']
                 and 'owner-after-saved.xml' not in row['stages'], 'unknown client publication fabricated terminal evidence')
            need(owner['original'] == row['original'], 'client action did not bind the full immutable host original')
            return owner, row
        first = wait(retained)
        actual_hire(client, plan)  # repeated real user input cannot issue a second financial original.
        time.sleep(5)
        second = retained()
        need(first[0]['belt'] == second[0]['belt'] and first[0]['bag'] == second[0]['bag']
             and second[0]['nativeDebits'] == 1, 'retry replayed an uncertain native payment')
        report.update(status='PASS_UNKNOWN_RETAINED', retained=second)
        return

    if case == 'refund':
        def reversal_saved():
            _, row, _ = observe(host, setup['stable'])
            owner = client_observe(client)
            need(row['control'] == 'ReadyRefund' and 'reversal-saved.xml' in row['stages'] and row['held'],
                 'refund first permit must follow actual complete saved owner reversal')
            need(owner['held'] and owner['nativeDebits'] == 1 and owner['nativeRefunds'] == 0,
                 'refund escaped before the saved-reversal first permit')
            return row
        report['reversal'] = wait(reversal_saved)
        console(host, 'rbdoghirefixture owner=0 dog=0 boundary=release confirm=CodexTest')

    def settled():
        _, row, original = observe(host, setup['stable'])
        owner = client_observe(client)
        refund = case == 'refund'
        need(('refunded.xml' if refund else 'committed.xml') in row['stages'] and not row['held']
             and not owner['held'] and owner['terminals'] == 1, 'both actual original custody owners must retire positively')
        need(owner['original'] == row['original'] and owner['nativeDebits'] == 1
             and owner['nativeRefunds'] == (1 if refund else 0), 'original client native setters were replayed/replaced')
        for phase in ('before', 'after', 'refund' if refund else 'settled'):
            need('owner-' + phase + '-saved.xml' in row['stages'], 'actual full native saved PDS checkpoint missing: ' + phase)
        need(owner['meat'] == baseline['meat'] - (0 if refund else int(original.attrib['price'])),
             'server proxy inventory was substituted for actual remote owner payment')
        if refund:
            need(owner['belt'] == baseline['belt'] and owner['bag'] == baseline['bag'],
                 'exact source refund altered native metadata or unrelated owner slots')
            need(row['ownership'] == 'None', 'saved refund retained paid ownership')
        else:
            need(row['ownership'] == 'Player' and row['owner'] == original.attrib['owner'],
                 'actual saved ownership did not follow the paid owner original')
        return owner, row
    report.update(terminal=wait(settled), status='PASS_REMOTE_' + case.upper())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path, required=True, help='actual authoritative host bridge session')
    parser.add_argument('--client-session', type=Path, required=True, help='actual remote payer bridge session')
    parser.add_argument('--case', choices=('normal', 'refund', 'lost-publication', 'restart-ready-debit', 'restart-ready-owner', 'restart-ready-refund', 'restart-terminal'), required=True)
    parser.add_argument('--resume', action='store_true', help='observe after operator-owned real crash/restart of both disposable processes')
    args = parser.parse_args()
    report = json.loads(args.report.read_text(encoding='utf-8')) if args.resume else {'status': 'AUTHORED_NOT_RUN'}
    host = Runner(report, args.report, session_path=args.session)
    client = Runner(report, args.report, session_path=args.client_session)
    try:
        if args.resume:
            need(report['status'] == 'PAUSED_FOR_REAL_BOTH_PROCESS_CRASH_RESTART', 'actual saved two-peer native cut report required')
            def recovered():
                result, row, original = observe(host, report['setup']['stable'])
                owner = client_observe(client)
                need(result['process'] != report['setup']['process'] and owner['process'] != report['baseline']['process'], 'both actual native processes must restart')
                need(row['original'] == report['cut'][1]['original'] and owner['original'] == row['original'], 'restart replaced original click/payment identity')
                refund = report['case'] == 'restart-ready-refund'
                need(('refunded.xml' if refund else 'committed.xml') in row['stages'] and not row['held'] and not owner['held'], 'actual same-original recovery did not retire both custody owners')
                need(row['nativeOwner'] and row['nativeOwner']['ReadReturned'] and row['nativeOwner']['Hydrated'] and row['nativeDog'], 'actual native server primary/player/dog reconstruction required')
                need(owner['meat'] == report['baseline']['meat'] - (0 if refund else int(original.attrib['price'])), 'cold native owner payment replayed')
                need(owner['nativeDebits'] == (1 if report['case'] == 'restart-ready-debit' else 0), 'already-saved native debit was replayed')
                need(owner['nativeJoin'] and owner['nativeJoin']['Returned'] and owner['nativeJoin']['Hydrated'] and owner['nativeJoin']['Joined'], 'actual authenticated ReadNetwork/PlayerId/ToPlayer client reconstruction required')
                need(owner['nativeRefunds'] == (1 if refund else 0), 'cold refund invocation count differs')
                if refund:
                    need(owner['belt'] == report['baseline']['belt'] and owner['bag'] == report['baseline']['bag'], 'cold native refund altered unrelated slots/full item metadata')
                return result, row, owner
            report.update(recovered=wait(recovered), status='PASS_REMOTE_NATIVE_RESTART')
        else:
            run(host, client, json.loads(args.plan.read_text(encoding='utf-8-sig')), report, args.case)
    except Exception as error:
        report.update(status='FAIL', failure=type(error).__name__ + ': ' + str(error))
        raise
    finally:
        host.save()
