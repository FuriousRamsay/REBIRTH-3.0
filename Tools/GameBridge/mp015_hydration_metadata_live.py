"""AUTHORED_NOT_RUN. Real selected rich metadata loss/retry exercise.

Requires already-running disposable host/client and a genuine saved+closed
cached-primary report from mp015_cached_primary_live.py. PLAN.openActions are
observed real UI inputs; metadata must actually exceed the compact64KiB path.
No launch/build, receipt mutation, synthetic native calls or native replay.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from mp015_completion_live import observe, command, strings
from mp015_cached_primary_live import actions, one


def run(args, client, server, report, cached, plan):
    operation = cached['operation']
    client.call('/cleararea', 'POST', radius=40)
    client.call('/restore', 'POST')
    before = observe(client, operation)
    known = {row['group'] for row in before['cached']['metadata']['waiting']}
    configured = command(client, 'rbwccompletionfixture metadata confirm=CodexTest')
    need(any('[REBIRTH MP015 fixture] metadata' in text for text in strings(configured)), 'actual DEBUG disposable metadata hold unavailable')
    try:
        actions(client, plan, 'openActions')
        deadline = time.monotonic() + 10
        while True:
            local, authority = observe(client, operation), observe(server, operation)
            report.setdefault('observations', []).append({'client': local, 'server': authority})
            client.save()
            waiting = [row for row in local['cached']['metadata']['waiting'] if row['group'] not in known]
            if waiting:
                need(len(waiting) == 1, 'one actual retained native component required')
                pending = waiting[0]
                sent = [row for row in authority['cached']['metadata']['send'] if row['group'] == pending['group']]
                need(len(sent) == 1 and sent[0]['enqueued'] and sent[0]['metadataBytes'] > 0, 'actual all-fragment selected producer required')
                need(not pending['nativeEntered'], 'metadata absence entered native Process')
                need(not any(row['group'] == pending['group'] for row in local['cached']['creator']), 'metadata absence issued creator source')
                if pending['queries'] >= 1:
                    need(pending['queries'] < 4, 'fixture held beyond bounded retry window')
                    report['group'] = pending['group']
                    report['native'] = pending['native']
                    report['observedServerRetries'] = sent[0]['retries']
                    break
            need(time.monotonic() < deadline, 'actual rich metadata path/retry was not reached')
            time.sleep(.1)
    finally:
        command(client, 'rbwccompletionfixture release confirm=CodexTest')
    deadline = time.monotonic() + 20
    while True:
        local, authority = observe(client, operation), observe(server, operation)
        report.setdefault('observations', []).append({'client': local, 'server': authority})
        client.save()
        creator, host = one(local, 'creator', operation), one(authority, 'host', operation)
        if creator and creator['group'] == report['group'] and creator['current'] and creator['prepared']:
            need(creator['original'] == host['original'] == cached['original'], 'metadata retry replaced original')
            need(creator['fullPrimary'] == cached['primary'] and not creator['nativeRan'] and not creator['attempted'], 'metadata retry inserted/rewarded/restored another primary')
            need(not any(row['group'] == report['group'] for row in local['cached']['metadata']['waiting']), 'same native component still pending after actual source issuance')
            need(host['saved'] and not host['physicalObserved'], 'metadata source substituted physical completion')
            if not any(row['group'] == report['group'] for row in local['cached']['metadata']['ack']) and not any(row['group'] == report['group'] for row in authority['cached']['metadata']['send']):
                report['status'] = 'PASS_ACTUAL_METADATA_RETRY'
                return
        need(time.monotonic() < deadline, 'actual one-Process grant/metadata ACK did not finish')
        time.sleep(.2)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan', type=Path)
    parser.add_argument('cached_report', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path, required=True)
    parser.add_argument('--server-session', type=Path, required=True)
    args = parser.parse_args()
    report = {'status': 'NOT_RUN'}
    client = Runner(report, args.report, session_path=args.session)
    server = Runner(report, args.report, session_path=args.server_session)
    try:
        run(args, client, server, report, json.loads(args.cached_report.read_text(encoding='utf-8')), json.loads(args.plan.read_text(encoding='utf-8')))
    except Exception as error:
        report['status'] = 'FAIL'
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        client.save()
