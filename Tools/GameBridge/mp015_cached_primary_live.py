"""AUTHORED_NOT_RUN. Actual MP015 cache boundary/close/reentry driver.

boundary PLAN REPORT: run observed boundaryActions on an already prepared full
station and retain the one NEW actual cache operation. close PLAN REPORT runs
closeActions. finish PLAN REPORT [--cold] runs openActions, verifies the retained
rich original primary, then clearActions and waits for real physical/outbox ACK.
--session and --server-session select already running disposable Bridge peers.
No launch, reset, build, native-method invocation or receipt/file mutation.
"""
import argparse
import json
import time
from pathlib import Path
from mp015_completion_live import observe, originals
from camper_consolidated_regression_live import Runner, need, ALLOWED


def actions(client, plan, name):
    rows = plan.get(name)
    need(isinstance(rows, list) and rows, 'observed real-input ' + name + ' required')
    for row in rows:
        need(row['endpoint'] in ALLOWED, 'real input endpoint required')
        client.call(row['endpoint'], 'POST', **row.get('args', {}))
        time.sleep(min(5, max(0, row.get('wait', .3))))


def one(row, side, operation):
    matches = [value for value in row['cached'][side] if value['operation'] == operation]
    need(len(matches) <= 1, 'ambiguous actual cached source')
    return matches[0] if matches else None


def poll(client, server, report, operation, predicate):
    deadline = time.monotonic() + 120
    while True:
        local, authority = observe(client, operation), observe(server, operation)
        report.setdefault('observations', []).append({'client': local, 'server': authority})
        client.save()
        try:
            return predicate(local, authority)
        except AssertionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(.5)


def run(args, report, client, server, plan):
    if args.phase == 'boundary':
        initial = observe(server)
        known = {value['operation'] for value in initial['cached']['host']}
        report['sessions'] = {'client': client.scope, 'server': server.scope}
        actions(client, plan, 'boundaryActions')

        def boundary(local, authority):
            candidates = [value for value in authority['cached']['host'] if value['operation'] not in known]
            need(len(candidates) == 1, 'one new actual cached boundary required')
            value = candidates[0]
            need(value['active'] and value['saved'] and value['nativeCurrent'] and not value['physicalObserved'], 'actual saved full boundary required')
            need(value['originalPrimary'], 'original rich primary missing')
            return value
        saved = poll(client, server, report, None, boundary)
        report.update(operation=saved['operation'], original=saved['original'], primary=saved['originalPrimary'], epoch=saved['epoch'])
    elif args.phase == 'close':
        operation = report['operation']
        actions(client, plan, 'closeActions')

        def closed(local, authority):
            value = one(authority, 'host', operation)
            need(value and value['original'] == report['original'] and value['originalPrimary'] == report['primary'], 'close replaced original primary')
            need(value['creatorCloseSaved'] and value['saved'] and value['nativeCurrent'] and not value['restoreAttempted'], 'actual unused close native save/readback required')
            need(not value['physicalObserved'] and value['epoch'] > report['epoch'], 'unused close must preserve pending physical original')
            need(one(local, 'creator', operation) is None, 'unused creator observation was not released')
            return value
        saved = poll(client, server, report, operation, closed)
        report['closedEpoch'] = saved['epoch']
    else:
        operation = report['operation']
        if args.cold:
            need(client.scope != report['sessions']['client'] or server.scope != report['sessions']['server'], 'real new process session required')
        actions(client, plan, 'openActions')

        def opened(local, authority):
            host = one(authority, 'host', operation)
            creator = one(local, 'creator', operation)
            need(host and creator and host['original'] == creator['original'] == report['original'], 'reentry did not retain SAME original')
            need(host['saved'] and host['nativeCurrent'] and not host['physicalObserved'], 'saved native pending source required')
            if args.cold:
                need(host['actualLoaded'], 'actual installed native chunk load source required for cold reentry')
            need(creator['current'] and creator['prepared'] and creator['fullPrimary'] == report['primary'], 'actual returned-Process grant/full original primary required')
            need(not creator['nativeRan'] and not creator['attempted'], 'full output must hold native cached Update before clearing')
            return creator
        report['reentry'] = poll(client, server, report, operation, opened)
        actions(client, plan, 'clearActions')

        def finished(local, authority):
            host = one(authority, 'host', operation)
            retired = authority.get('cached', {}).get('retired')
            if host:
                need(host['physical'] and host['physicalObserved'] and host['saved'] and host['nativeCurrent'], 'actual physical native save required')
                need(host['original'] == report['original'], 'physical completion replaced original')
            else:
                need(retired and retired['operation'] == operation and retired['original'] == report['original'] and retired['physicalSaved'] and retired['resourceRetired'], 'strict historical saved physical retirement required')
            local_entries, server_entries = originals(local), originals(authority)
            need(operation in local_entries and operation in server_entries, 'independent durable outboxes required')
            local_state, local_original, _ = local_entries[operation]
            state, original, _ = server_entries[operation]
            need(state.attrib['witness'] == local_state.attrib['witness'], 'ACK changed immutable original')
            for key in ('owner', 'creation', 'viewer', 'viewerCreation', 'recipe'):
                need(original.attrib[key] == local_original.attrib[key] == plan[key], 'original attribution differs: ' + key)
            delivery = original.find('delivery/nativeOwningDelivery')
            need(delivery is not None and delivery.attrib['sink'] == plan['sink'], 'actual native delivery path differs')
            need(delivery.findtext('primaryNative') == report['primary'], 'native cached insertion changed rich seed/quality/metadata')
            need(int(delivery.attrib['beforeCount']) - int(delivery.attrib['afterCount']) == 1, 'native decrement must be exactly one')
            need(all(state.attrib[key] == 'true' for key in ('skill', 'statistics', 'decision', 'terminal')), 'independent durable effects remain pending')
            need(local_state.attrib['acknowledged'] == 'true', 'actual original client ACK not saved')
            return state.attrib['witness']
        report['witness'] = poll(client, server, report, operation, finished)
    report['status'] = 'PASS_' + args.phase.upper()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=('boundary', 'close', 'finish'))
    parser.add_argument('plan', type=Path)
    parser.add_argument('report', type=Path)
    parser.add_argument('--session', type=Path)
    parser.add_argument('--server-session', type=Path, required=True)
    parser.add_argument('--cold', action='store_true')
    args = parser.parse_args()
    report = {'status': 'NOT_RUN'} if args.phase == 'boundary' else json.loads(args.report.read_text(encoding='utf-8'))
    client = Runner(report, args.report, session_path=args.session)
    server = Runner(report, args.report, session_path=args.server_session)
    try:
        run(args, report, client, server, json.loads(args.plan.read_text(encoding='utf-8')))
    except Exception as error:
        report['status'] = 'FAIL'
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        client.save()
