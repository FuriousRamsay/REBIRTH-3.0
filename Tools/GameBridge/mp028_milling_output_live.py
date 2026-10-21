"""AUTHORED_NOT_RUN. Actual managed/mobile milling output native source driver.
Only existing bounded native inputs; no callback/source setters or process control.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED
from mp010_pull_lifecycle_live import topology, require_peers


def observe(owner):
    value = owner.call('/auditstate', domain='recipeOutputOriginals')['domains']['recipeOutputOriginals']
    need(value.get('available') is True and not value.get('error') and isinstance(value.get('milling'), dict), 'actual read-only milling native domain required')
    need(isinstance(value['milling'].get('records'), list), 'actual immutable milling originals required')
    return value['milling']


def run(args, report):
    plan = json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    need(plan.get('mode') in ('managed-static', 'mobile-tile'), 'explicit native source mode required')
    owner = Runner(report, Path(args.report), session_path=args.session)
    scope = topology(owner)
    require_peers(scope, scope, True)
    need(owner.call('/guard')['enabled'] is True, 'native instincts remain enabled')
    owner.call('/surroundings')
    first = observe(owner)
    original = None
    resume = plan.get('resume')
    if resume:
        prior = json.loads(Path(resume['report']).read_text(encoding='utf-8-sig'))
        saved = prior.get('original')
        need(saved and saved.get('operation') and saved.get('original') and saved.get('topology'), 'prior actual immutable native original report required')
        old = saved['topology']
        for key in ('worldKey', 'ownerKey', 'creationId', 'deaths'):
            need(old[key] == scope[key], 'native cold scope changed: ' + key)
        need(old['processIdentity'] != scope['processIdentity'] and old['worldInstance'] != scope['worldInstance'], 'actual cold process/world replacement required')
        original = dict(saved, topology=dict(scope), originTopology=old)
        report['original'] = original
    baseline = {row['operation'] for row in first['records'] if not original or row['operation'] != original['operation']}
    acted = False
    need(plan.get('checkpoints'), 'actual native checkpoints required')
    for checkpoint in plan['checkpoints']:
        expected = checkpoint.get('expectOriginal')
        need(checkpoint.get('name') and isinstance(expected, dict) and expected, 'explicit native source assertions required')
        for action in checkpoint.get('actions', []):
            endpoint, values = action['endpoint'], action.get('args', {})
            need(endpoint in ALLOWED and not values.get('hold'), 'existing bounded actual native input only')
            if endpoint == '/press':
                need(0 < float(values.get('seconds', .2)) <= 2, 'bounded native press required')
            if endpoint == '/ui/drag':
                need(values.get('strict') == 1, 'strict observed native slot path required')
            owner.call(endpoint, 'POST', **values)
            acted = True
            delay = float(action.get('wait', .2))
            need(0 <= delay <= 5, 'bounded actual input delay required')
            time.sleep(delay)
        deadline = time.monotonic() + min(120, max(1, float(checkpoint.get('timeout', 30))))
        while True:
            current = topology(owner)
            for key in ('processIdentity', 'worldInstance', 'worldKey', 'ownerKey', 'creationId', 'entityId', 'deaths'):
                need(current[key] == scope[key], 'actual native episode changed: ' + key)
            state = observe(owner)
            rows = [row for row in state['records'] if row['operation'] not in baseline]
            need(len(rows) <= 1, 'one original source required; queue one milling unit')
            row = rows[0] if rows else None
            if row:
                need(acted or resume, 'native original requires actual input or actual cold report')
                need(row.get('unknown') is False, 'unknown original cannot certify completion')
                need(row.get('mobile') == (plan['mode'] == 'mobile-tile'), 'actual native source type differs')
                if original is None:
                    original = dict(operation=row['operation'], original=row['original'], topology=dict(scope))
                    report['original'] = original
                need(row['operation'] == original['operation'] and row['original'] == original['original'], 'frozen native original changed')
            matched = row is not None and all(row.get(key) == value for key, value in expected.items())
            if checkpoint.get('terminal'):
                need(expected.get('nativeSaved') is True and expected.get('terminalSaved') is True and expected.get('nativeOutputQueueMatches') is True, 'positive saved native output plus queue terminal must be explicit')
                if resume:
                    need(expected.get('coldNativeContinuation') is True, 'actual native-loaded prefix entry must be explicit after cold restart')
                matched = matched and row.get('active') is False and row.get('nativeTickReturned') is True
            if matched:
                report['checkpoints'].append(dict(name=checkpoint['name'], source=row, observation=state))
                owner.save()
                break
            need(time.monotonic() < deadline, 'actual native milling checkpoint pending: ' + checkpoint['name'])
            time.sleep(.5)
    report['status'] = 'PASS'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', required=True)
    parser.add_argument('--report', required=True)
    parser.add_argument('--session')
    args = parser.parse_args()
    report = dict(status='RUNNING', checkpoints=[], scope='actual managed/static or mobile tile native source; input and saved output/queue original; AUTHORED_NOT_RUN')
    try:
        run(args, report)
    except Exception as error:
        report['status'] = 'FAIL'
        report['error'] = str(error)
        raise
    finally:
        Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
