"""AUTHORED_NOT_RUN. Actual local personal output original and native cold load.
External process/lifecycle changes are parent-owned. No injected effects,
callbacks, source setters, receipts, queue changes, grants or save editing.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(runner):
    state = runner.call('/auditstate', domain='recipeOutputOriginals')['domains']['recipeOutputOriginals']
    need(state.get('schema') == 1 and state.get('available') is True and not state.get('error'), 'actual bounded original observation required')
    need(all(row.get('unknown') is False for row in state['records']), 'unknown original cannot certify native progress')
    return state


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    owner = Runner(report, Path(args.report), session_path=args.session)
    scope = topology(owner)
    require_peers(scope, scope, True)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    initial = observe(owner)
    baseline = {row['operation'] for row in initial['records']}
    original = plan.get('resume')
    kind = plan.get('resumeKind')
    need(kind in (None, 'reconnect', 'cold-owner'), 'supported explicit continuation required')
    if original:
        need(kind is not None and original.get('operation') and original.get('original'), 'preserved immutable original required')
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(scope[key] == original['topology'][key], 'changed original scope: ' + key)
        if kind == 'cold-owner':
            need(scope['processIdentity'] != original['topology']['processIdentity'], 'cold continuation needs actual new process')
        else:
            need(scope['worldInstance'] != original['topology']['worldInstance'] or scope['processIdentity'] != original['topology']['processIdentity'], 'reconnect needs actual new world/session')
    else:
        need(kind is None, 'continuation cannot invent an original')
    need(plan.get('checkpoints'), 'named native checkpoints required')
    acted = False
    for checkpoint in plan['checkpoints']:
        need(checkpoint.get('name') and checkpoint.get('expectOriginal'), 'explicit actual original assertions required')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'bounded native input only')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed drag required')
            owner.call(endpoint, 'POST', **values)
            acted = True
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded input delay required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current = topology(owner)
            require_peers(current, current, True)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == scope[key], 'episode changed; preserve exact original and resume: ' + key)
            state = observe(owner)
            if original is None:
                fresh = [row for row in state['records'] if row['operation'] not in baseline]
                need(len(fresh) <= 1, 'one actual craft completion original required')
                if fresh:
                    need(acted, 'new original must follow native crafting input')
                    original = dict(operation=fresh[0]['operation'], original=fresh[0]['original'], topology=dict(scope))
            rows = [] if original is None else [row for row in state['records'] if row['operation'] == original['operation']]
            need(len(rows) <= 1, 'ambiguous original evidence')
            if rows:
                row = rows[0]
                need(row['original'] == original['original'], 'immutable generated original changed')
                report['resume'] = original
                report['observations'].append(dict(name=checkpoint['name'], state=state))
                matches = all(row.get(key) == value for key, value in checkpoint['expectOriginal'].items())
                if checkpoint.get('terminal'):
                    matches &= row.get('terminalSaved') is True and row.get('active') is False and row.get('marker') == 0
                    matches &= row.get('savedQueueMatches') is True and row.get('ownerMatchesAccepted') is True
                    need(checkpoint['expectOriginal'].get('acceptedPrefix') == row.get('outputs'), 'terminal must explicitly assert complete original prefix')
                    need(checkpoint['expectOriginal'].get('callbackReturned') is True, 'positive actual callback return required')
                if matches:
                    report['checkpoints'].append(dict(name=checkpoint['name'], original=row))
                    owner.save()
                    break
            need(time.monotonic() < deadline, 'actual personal output checkpoint pending: ' + checkpoint['name'])
            time.sleep(.5)
        if checkpoint.get('externalPause'):
            report['status'] = 'AWAITING_EXTERNAL'
            return
    need(plan['checkpoints'][-1].get('terminal') is True, 'PASS needs complete native output/queue terminal')
    owner.errors()
    report.update(status='PASS', topology='HOST_LOCAL', scope='Only explicit actual input/checkpoints executed; remote, unexecuted faults and native invocation counts are not inferred')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan'); parser.add_argument('report'); parser.add_argument('--session', required=True)
    args = parser.parse_args(); report = dict(status='FAIL', observations=[], checkpoints=[])
    try:
        run(args, report)
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
