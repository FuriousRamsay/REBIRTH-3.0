"""AUTHORED_NOT_RUN. Actual host placed station input and native saved boundary.
No source setters, callbacks, save mutations, injected faults or process control.
Cold reattachment and remote/mobile purposes are separate open production legs.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(owner):
    state = owner.call('/auditstate', domain='recipeOutputOriginals')['domains']['recipeOutputOriginals']
    need(state.get('schema') == 1 and state.get('available') is True and not state.get('error'), 'actual recipe output observation required')
    need(isinstance(state.get('stationBefore'), list) and isinstance(state.get('stationRecords'), list), 'actual station native purpose observations required')
    return state


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    resume = plan.get('resume')
    owner = Runner(report, Path(args.report), session_path=args.session)
    scope = topology(owner)
    require_peers(scope, scope, True)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    initial = observe(owner)
    original = None
    if resume:
        prior = json.loads(Path(resume['report']).read_text(encoding='utf-8-sig'))
        saved = prior.get('original')
        need(saved and saved.get('operation') and saved.get('original') and saved.get('topology'), 'previous actual native source report required')
        old = saved['topology']
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(old[key] == scope[key], 'cold original owner/world/life changed: ' + key)
        need(old['processIdentity'] != scope['processIdentity'] and old['worldInstance'] != scope['worldInstance'], 'actual cold process and world replacement required')
        original = dict(operation=saved['operation'], original=saved['original'], topology=dict(scope), originTopology=old)
        report['original'] = original
        cold = [row for row in initial.get('stationCold', []) if row.get('operation') == original['operation'] and row.get('original') == original['original']]
        need(len(cold) == 1 and cold[0].get('ownerNativeLoaded') is True and cold[0].get('ownerHydrated') is True, 'actual saved native owner Load/Read/ToPlayer original required')
    baseline = {row['operation'] for row in initial['stationRecords'] if not original or row['operation'] != original['operation']}
    before_baseline = {row['operation'] for row in initial['stationBefore']}
    acted = False
    need(plan.get('checkpoints'), 'named native input/checkpoint plan required')
    for checkpoint in plan['checkpoints']:
        need(checkpoint.get('name') and (checkpoint.get('expectOriginal') or checkpoint.get('expectBefore') or checkpoint.get('expectCold')), 'explicit actual source assertions required')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'bounded actual native input only')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native slot path required')
            owner.call(endpoint, 'POST', **values)
            acted = True
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded input delay required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current = topology(owner)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == scope[key], 'native episode changed: ' + key)
            state = observe(owner)
            rows = [row for row in state['stationRecords'] if row['operation'] not in baseline]
            need(len(rows) <= 1, 'one actual station completion original required; use one queued unit')
            if rows:
                need(acted or resume, 'source must follow native input or exact actual cold source')
                row = rows[0]
                need(row.get('unknown') is False, 'unknown source cannot certify saved native progress')
                if original is None:
                    original = dict(operation=row['operation'], original=row['original'], topology=dict(scope))
                need(row['operation'] == original['operation'] and row['original'] == original['original'], 'immutable generated station original changed')
                report['original'] = original
            expected = checkpoint.get('expectOriginal')
            if checkpoint.get('expectCold'):
                need(resume and original, 'cold checkpoint requires actual original report and process replacement')
                cold = [row for row in state.get('stationCold', []) if row.get('operation') == original['operation'] and row.get('original') == original['original']]
                expected = checkpoint['expectCold']
                need(expected.get('ownerNativeLoaded') is True and expected.get('ownerHydrated') is True and expected.get('nativeReadCaptured') is True, 'positive actual native load lineage must be explicit')
                matches = len(cold) == 1 and all(cold[0].get(key) == value for key, value in expected.items())
                observed = cold[0] if cold else None
            elif expected:
                matches = bool(rows) and all(rows[0].get(key) == value for key, value in expected.items())
                if checkpoint.get('terminal'):
                    need(expected.get('callbackReturned') is True and expected.get('beforeNativeSaved') is True and expected.get('beforeOwnerSaved') is True, 'positive actual callback and saved native owner plus ready tile Before must be explicit')
                    matches = matches and all(rows[0].get(key) is True for key in ('gridAccepted', 'callbackReturned', 'beforeNativeSaved', 'beforeOwnerSaved', 'terminalSaved', 'savedQueueMatches', 'savedNativeTupleMatches')) and rows[0].get('active') is False
                observed = rows[0] if rows else None
            else:
                ready = [row for row in state['stationBefore'] if row['operation'] not in before_baseline]
                need(len(ready) <= 1, 'one actual pre-generation source required')
                matches = bool(ready) and all(ready[0].get(key) == value for key, value in checkpoint['expectBefore'].items())
                observed = ready[0] if ready else None
            if matches:
                report['checkpoints'].append(dict(name=checkpoint['name'], source=observed, observation=state))
                owner.save()
                break
            need(time.monotonic() < deadline, 'actual saved station checkpoint pending: ' + checkpoint['name'])
            time.sleep(.5)
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--session')
    args = parser.parse_args()
    report = dict(status='RUNNING', checkpoints=[], scope='actual host placed station native input/saved boundary; no cold/remote/mobile certification')
    try:
        run(args, report)
    except Exception as error:
        report['status'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
