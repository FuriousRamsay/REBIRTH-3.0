"""AUTHORED_NOT_RUN. Actual remote native earned original/echo/output phases.

Native input and read-only observations only; independent authenticated peer
sessions required. External cold/reconnect occurs outside the driver.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers
from mp015_completion_live import strings


def at(value, path):
    for key in path:
        value = value[key]
    return value


def observe(runner, role, viewer=False):
    response = runner.call('/console', 'POST', cmd='rbearnedstatus viewerTools' if viewer else 'rbearnedstatus')
    if viewer:
        role = 'viewer-tools-' + role
    values = []
    for text in strings(response):
        try:
            value = json.loads(text)
        except (ValueError, TypeError):
            continue
        if isinstance(value, dict) and value.get('role') == role:
            values.append(value)
    need(len(values) == 1 and values[0].get('readOnly') is True and values[0].get('enabled') is True,
         'actual reviewed native earned observer required')
    return values[0]


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    viewer = plan.get('viewerTools') is True
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0, 'actual observed authority actor required')
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    host = Runner(report, Path(args.report), session_path=args.server_session)
    scopes = [topology(owner), topology(host, plan['serverPlayerId'])]
    require_peers(*scopes)
    need(plan.get('topology') in ('listen-remote', 'dedicated-remote'), 'explicit actual remote topology required')
    need(bool(scopes[1]['dedicated']) == (plan['topology'] == 'dedicated-remote'), 'actual dedicated topology differs')
    transition = plan.get('transition', 'fresh')
    need(transition in ('fresh', 'reconnect', 'cold-owner', 'cold-server', 'cold-both'), 'explicit lifecycle required')
    report['topology'] = scopes
    identity = None
    operation = plan.get('operation')
    if args.resume:
        prior = json.loads(Path(args.resume).read_text(encoding='utf-8-sig'))
        identity = tuple(prior['identity'])
        operation = identity[0]
        for current, previous in zip(scopes, prior['topology']):
            for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
                need(current.get(key) == previous.get(key), 'original scope changed: ' + key)
        need(transition != 'fresh', 'resumed plan must name its actual lifecycle transition')
        for index, role in enumerate(('owner', 'server')):
            if transition in ('cold-' + role, 'cold-both'):
                need(scopes[index]['processIdentity'] != prior['topology'][index]['processIdentity'], 'genuine new ' + role + ' process required')
        if transition == 'reconnect':
            need(any(current['processIdentity'] != previous['processIdentity'] or current['worldInstance'] != previous['worldInstance']
                     for current, previous in zip(scopes, prior['topology'])), 'genuine reconnect World/session change required')
    else:
        need(transition == 'fresh', 'cold/reconnect needs an existing saved original report')
    if viewer and plan.get('noToolGesture'):
        checks = plan.get('unchangedChecks', [])
        for kind in ('bag', 'belt'):
            need(any(check.get('kind') == kind for check in checks), 'actual no-gesture ' + kind + ' fingerprint required')
        before = []
        for check in checks:
            need(check['endpoint'] in ('/state', '/uitree', '/target', '/auditstate'), 'read-only no-gesture baseline required')
            peer = host if check.get('peer') == 'server' else owner
            before.append(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']))
        viewing = plan.get('stationViewingChecks', [])
        for kind in ('queue', 'tool', 'output'):
            need(any(check.get('kind') == kind for check in viewing), 'actual open-card ' + kind + ' fingerprint required')
        viewing_before = []
        for check in viewing:
            need(check['endpoint'] in ('/state', '/uitree', '/target', '/auditstate'), 'read-only native viewing baseline required')
            peer = host if check.get('peer') == 'server' else owner
            viewing_before.append(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']))
        opened = closed = False
        for checkpoint in plan.get('noGestureCheckpoints', []):
            for action in checkpoint.get('actions', []):
                for current, initial in zip([topology(owner), topology(host, plan['serverPlayerId'])], scopes):
                    for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                        need(current.get(key) == initial.get(key), 'native scope changed before no-gesture action')
                need(action['endpoint'] in ALLOWED, 'actual native card input required')
                owner.call(action['endpoint'], 'POST', **action.get('args', {}))
            for check in checkpoint.get('checks', []):
                need(check['endpoint'] == '/uitree', 'actual native card UI observation required')
                need(at(owner.call('/uitree', **check.get('args', {})), check['path']) == check['value'], 'actual native card observation differs')
                opened |= check.get('kind') == 'openCard'
                closed |= check.get('kind') == 'closedCard'
                if check.get('kind') == 'openCard':
                    for native, value in zip(viewing, viewing_before):
                        peer = host if native.get('peer') == 'server' else owner
                        need(at(peer.call(native['endpoint'], **native.get('args', {})), native['path']) == value, 'viewing changed held native station contents')
        need(opened and closed, 'actual opened and closed native card must both be observed')
        for check, value in zip(checks, before):
            peer = host if check.get('peer') == 'server' else owner
            need(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']) == value, 'no-gesture native contents changed')
        deadline = time.monotonic() + min(60, plan.get('timeout', 30))
        while True:
            for current, initial in zip([topology(owner), topology(host, plan['serverPlayerId'])], scopes):
                for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                    need(current.get(key) == initial.get(key), 'native scope changed during no-gesture retirement')
            rows = [observe(owner, 'owner', True)['originals'], observe(host, 'host', True)['originals']]
            if all(row.get('nativeViewerToolsReleased') is True for row in rows[0]) and all(row.get('nativeViewerToolsHeld') is False for row in rows[1]):
                break
            need(time.monotonic() < deadline, 'no-gesture close left active viewer tool custody')
            time.sleep(.2)
        for check, value in zip(checks, before):
            peer = host if check.get('peer') == 'server' else owner
            need(at(peer.call(check['endpoint'], **check.get('args', {})), check['path']) == value, 'final no-gesture native contents changed')
        report['noGesture'] = {'before': before, 'afterRows': rows, 'opened': opened, 'closed': closed}
        report['status'] = 'PASS'
        return
    known = {row['operation'] for row in observe(owner, 'owner', viewer)['originals']}
    for checkpoint in plan['checkpoints']:
        for action in checkpoint.get('actions', []):
            need(action['endpoint'] in ALLOWED, 'actual native player input required')
            owner.call(action['endpoint'], 'POST', **action.get('args', {}))
        deadline = time.monotonic() + min(180, checkpoint.get('timeout', 60))
        while True:
            for current, initial in zip([topology(owner), topology(host, plan['serverPlayerId'])], scopes):
                for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                    need(current.get(key) == initial.get(key), 'native scope changed within invocation: ' + key)
            observations = [observe(owner, 'owner', viewer), observe(host, 'host', viewer)]
            report['observations'].append(observations)
            if operation is None:
                fresh = [row for row in observations[0]['originals'] if row['operation'] not in known]
                need(len(fresh) <= 1, 'prepare one actual native original')
                if fresh:
                    operation = fresh[0]['operation']
            matched = [[row for row in value['originals'] if row['operation'] == operation] for value in observations]
            need(all(len(rows) <= 1 for rows in matched), 'ambiguous actual native original')
            if all(matched):
                rows = [items[0] for items in matched]
                observed_identity = tuple(rows[0][key] for key in ('operation', 'birth', 'nonce', 'digest'))
                if identity is None:
                    if not rows[0].get('nonce') or rows[0]['nonce'] == '0' * 32 or rows[1].get('nonce') != rows[0]['nonce']:
                        need(time.monotonic() < deadline, 'actual original admission nonce timeout')
                        time.sleep(.2)
                        continue
                    identity = observed_identity
                need(identity == observed_identity and all(tuple(row[key] for key in ('operation', 'birth', 'nonce', 'digest')) == identity for row in rows),
                     'original immutable identity changed across peers')
                need(not any(row.get('unknown') for row in rows), 'unknown actual observation')
                report['identity'] = list(identity)
                expectations = [checkpoint['expectOwner'], checkpoint['expectHost']]
                need(all(expected and all(type(value) is bool for value in expected.values()) for expected in expectations), 'exact Boolean actual phases required')
                if all(all(row.get(key) is value for key, value in expected.items()) for row, expected in zip(rows, expectations)):
                    codec_results = []
                    for check in checkpoint.get('codecs', []):
                        need(check.get('codec') in ('output', 'pending', 'tools', 'viewerSource'), 'actual native codec required')
                        need(not any(row.get('released') for row in rows), 'codec observation must precede native release')
                        runner = host if check.get('peer') == 'server' else owner
                        values = []
                        for text in strings(runner.call('/console', 'POST', cmd='rbearnedcodec ' + check['codec'] + ' ' + operation)):
                            try:
                                value = json.loads(text)
                            except (ValueError, TypeError):
                                continue
                            if isinstance(value, dict) and value.get('codec') == check['codec']:
                                values.append(value)
                        need(len(values) == 1 and values[0].get('operation') == operation and
                             all(values[0].get(key) is True for key in ('readOnly', 'actualRetainedNative', 'passed')) and
                             len(values[0].get('assertions', [])) == 7, 'actual retained native codec assertions failed')
                        codec_results.append(values[0])
                    report['checkpoints'].append({'name': checkpoint['name'], 'rows': rows, 'codecs': codec_results})
                    break
            need(time.monotonic() < deadline, 'native phase timeout: ' + checkpoint['name'])
            time.sleep(.2)
    final_rows = report['checkpoints'][-1]['rows'] if report['checkpoints'] else []
    if plan.get('externalPause'):
        need(bool(plan.get('pauseReason')) and final_rows, 'retained checkpoint needs an explicit reason and actual observed native phases')
        report['status'] = 'RETAINED_EXTERNAL_PAUSE'
        return
    if viewer:
        need(len(final_rows) == 2 and all(row.get('nativeViewerToolsReleased') is True and row.get('nativeViewerToolsHeld') is False for row in final_rows),
             'both actual viewer tool payer holds must be released before PASS')
        need(all(row.get('payer') == scopes[0]['ownerKey'] for row in final_rows) and
             plan.get('earnedOwnerKey') and plan['earnedOwnerKey'] != scopes[0]['ownerKey'], 'actual tool payer must differ from immutable earned recipient')
    else:
        need(len(final_rows) == 2 and all(row.get('closed') is True and row.get('released') is True and row.get('nativeHeld') is False for row in final_rows),
             'both actual original financial/native holds must be released before PASS')
    if plan.get('nativeStationAdoption'):
        need(all(row.get('nativeStationAdopted') is True for row in final_rows), 'both peers must record exact authenticated native station custody adoption')
        if plan.get('ownerAbsentBeforeClose'):
            need(final_rows[0].get('nativeAdoptionQueried') is True or
                 any(item['rows'][0].get('closeQueryOnly') is True and item['rows'][0].get('closeRequested') is False
                     for item in report['checkpoints']), 'cold row-absent owner must observe its actual metadata-only adoption query')
    checks = plan.get('nativeChecks')
    need(isinstance(checks, list) and checks, 'explicit actual native queue/tool/output assertions required')
    for name in ('queue', 'tool', 'output'):
        need(any(check.get('kind') == name and check.get('path') for check in checks), 'actual native ' + name + ' assertion required')
    if viewer:
        for name in ('earnedRecipient', 'earnedOriginal'):
            need(any(check.get('kind') == name and check.get('path') for check in checks), 'immutable actual ' + name + ' assertion required')
        need(any(check.get('kind') == 'earnedRecipient' and check.get('value') == plan['earnedOwnerKey'] for check in checks),
             'native recipient observation must match the distinct original earned owner')
        need(any(check.get('kind') == 'earnedOriginal' and check.get('value') == identity[3] for check in checks),
             'native original observation must match the unchanged payer-scope original digest')
    if plan.get('lastFuelPaidCompletion'):
        for name in ('fuel', 'burning'):
            need(any(check.get('kind') == name and check.get('path') for check in checks), 'actual unchanged last-fuel ' + name + ' assertion required')
    for check in checks:
        need(check['endpoint'] in ('/state', '/uitree', '/target', '/auditstate'), 'read-only native result endpoint required')
        runner = host if check.get('peer') == 'server' else owner
        need(at(runner.call(check['endpoint'], **check.get('args', {})), check['path']) == check['value'], 'actual native result differs')
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan')
    parser.add_argument('report')
    parser.add_argument('--owner-session', required=True)
    parser.add_argument('--server-session', required=True)
    parser.add_argument('--resume')
    args = parser.parse_args()
    report = {'status': 'FAIL', 'observations': [], 'checkpoints': []}
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
