"""AUTHORED_NOT_RUN. Actual bounded remote station inputs and native prefix audit.
No output/source setters, injected interruption, callbacks, native PDS or process controls.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(owner):
    state = owner.call('/auditstate', domain='recipeOutputOriginals')['domains']['recipeOutputOriginals']
    need(state.get('schema') == 1 and state.get('available') is True and not state.get('error'), 'actual recipe output audit required')
    need(isinstance(state.get('remotePrefixes'), list), 'actual remote prefix native purpose audit required')
    return state


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('role') in ('fresh', 'cached', 'deferred', 'final-callback'), 'explicit actual remote native role required')
    owner = Runner(report, Path(args.report), session_path=args.session)
    scope = topology(owner)
    server = Runner(report, Path(args.report), session_path=args.server_session)
    need(type(plan.get('serverPlayerId')) is int and plan['serverPlayerId'] > 0, 'actual authenticated remote owner entity required')
    host_scope = topology(server, plan['serverPlayerId'])
    require_peers(scope, host_scope)
    need(scope.get('remote') is True and scope.get('server') is False and scope.get('localActor') is True and scope.get('connected') is True, 'actual connected owning remote client required')
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    prior = plan.get('resume')
    need(plan['role'] != 'final-callback' or prior, 'final callback lifecycle requires saved original endpoint evidence')
    original = None
    if prior:
        need(plan.get('resumeKind') == 'cold-both' and plan['role'] in ('fresh', 'final-callback'), 'exact fresh cold-both native source role required')
        need(prior.get('topology') and prior.get('hostTopology'), 'saved actual endpoint topology required')
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(scope[key] == prior['topology'][key] and host_scope[key] == prior['hostTopology'][key], 'cold original identity changed: ' + key)
        for current, previous in ((scope, prior['topology']), (host_scope, prior['hostTopology'])):
            need(current['processIdentity'] != previous['processIdentity'] and current['worldInstance'] != previous['worldInstance'], 'both actual native processes and worlds must be replaced')
        need(observe(owner).get('remoteOwnerNativeLoaded') is True, 'actual owner native PlayerId hydration required')
        host = server.call('/auditstate', domain='recipeOutputOriginals', player=plan['serverPlayerId'])['domains']['recipeOutputOriginals']
        sources = [row for row in host.get('remoteHostSources', []) if row.get('nativeOperation') == prior.get('nativeOperation')]
        need(len(sources) == 1 and (sources[0].get('nativeLoadedCurrent') is True or plan['role'] == 'final-callback' and sources[0].get('nativeTerminalLoadedCurrent') is True), 'exact actual host station native load required before reentry')
        need(sources[0].get('nonce') == prior.get('nonce') and sources[0].get('source') == prior.get('source'), 'loaded host source must retain original nonce and immutable digest')
        if plan['role'] == 'final-callback':
            need(prior.get('afterSaved') is True and sources[0].get('finalCallbackSaved') is True, 'positive original final owner native save and actual host final source required')
        original = {key: prior[key] for key in ('operation', 'original', 'nativeOperation', 'nonce', 'source')}
    baseline = {row['nativeOperation'] for row in observe(owner)['remotePrefixes']} if not prior else set()
    acted = False
    need(plan.get('checkpoints'), 'explicit actual native input and native saved checkpoint plan required')
    for checkpoint in plan['checkpoints']:
        expected = checkpoint.get('expect', {})
        need(checkpoint.get('name') and expected, 'named positive native source assertions required')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'bounded actual native input only')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict actual observed slot path required')
            owner.call(endpoint, 'POST', **values)
            acted = True
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded actual input delay required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current = topology(owner)
            current_host = topology(server, plan['serverPlayerId'])
            require_peers(current, current_host)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == scope[key], 'actual native episode changed: ' + key)
                need(current_host[key] == host_scope[key], 'actual host native episode changed: ' + key)
            state = observe(owner)
            observed_rows = state.get('remoteColdFinals', []) if plan['role'] == 'final-callback' else state['remotePrefixes']
            rows = [row for row in observed_rows if row['nativeOperation'] not in baseline and row.get('operation') and (not prior or row['nativeOperation'] == original['nativeOperation'])]
            need(len(rows) <= 1, 'one actual queued output application required')
            if rows:
                row = rows[0]
                need((acted or plan['role'] == 'final-callback') and (plan['role'] == 'final-callback' or row.get('cached') == (plan['role'] == 'cached')), 'actual input and requested native role required')
                if plan['role'] != 'final-callback':
                    need(row.get('deferredNativeAdd') == (plan['role'] == 'deferred'), 'actual deferred Add identity must match the requested native role')
                if original is None:
                    original = {key: row[key] for key in ('operation', 'original', 'nativeOperation', 'nonce', 'source')}
                report['original'] = dict(original, topology=scope, hostTopology=host_scope)
                if row.get('afterSaved') is True:
                    report['original']['afterSaved'] = True
                need(all(row[key] == value for key, value in original.items()), 'immutable local/native output source identity changed')
                if checkpoint.get('savedPrefix'):
                    need(expected.get('prefixSaved') is True and expected.get('beforeSaved') is True, 'positive native Before and exact intermediate saved prefix required')
                    need(expected.get('acceptedPrefix', 0) > 0, 'explicit actual accepted output count required')
                if checkpoint.get('savedFinal'):
                    need(expected.get('afterSaved') is True, 'positive final native owner Saved checkpoint required')
                matches = all(row.get(key) == value for key, value in expected.items())
                if prior and plan['role'] != 'final-callback':
                    matches = matches and row.get('coldReattached') is True
                if plan['role'] == 'final-callback':
                    matches = matches and state.get('remoteOwnerMatchesLoaded') is True and row.get('current') is True and row.get('finalCallbackSaved') is True and row.get('callbackReturned') is True
                    need(expected.get('queueTerminalSaved') is True or expected.get('queueOnly') is True, 'positive actual queue-only binding or consumed-row terminal required')
                if matches and checkpoint.get('savedPrefix'):
                    matches = row.get('current') is True and row.get('nativeSent') is True and row.get('nativeCaptured') is True and row.get('nativeLength', 0) > 0 and bool(row.get('nativeDigest'))
                    if row.get('historicalObservation'):
                        matches = matches and row.get('ownerMatchesSaved') is True and row.get('ownerImage') == state.get('ownerImage')
                if matches and checkpoint.get('savedFinal'):
                    matches = row.get('current') is True and row.get('afterNativeSent') is True and row.get('afterNativeCaptured') is True and row.get('afterNativeLength', 0) > 0 and bool(row.get('afterNativeDigest')) and row.get('ownerMatchesSaved') is True and row.get('ownerImage') == state.get('ownerImage')
                if matches:
                    report['checkpoints'].append(dict(name=checkpoint['name'], source=row, observation=state))
                    owner.save()
                    break
            need(time.monotonic() < deadline, 'actual native prefix checkpoint pending: ' + checkpoint['name'])
            time.sleep(.2)
    need(plan['role'] == 'final-callback' or any(checkpoint.get('savedPrefix') or checkpoint.get('savedFinal') for checkpoint in plan['checkpoints']), 'at least one positive native owner prefix or final save checkpoint required')
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--session', required=True)
    parser.add_argument('--server-session', required=True)
    args = parser.parse_args()
    report = dict(status='RUNNING', checkpoints=[], execution='actual native endpoints only')
    try:
        run(args, report)
    except Exception as error:
        report.update(status='FAIL', error=str(error))
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
