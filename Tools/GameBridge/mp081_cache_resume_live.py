"""MP081 actual Bridge assertions. PREPARED / NOT_RUN; never launches or resets.

boundary REPORT --step 0..4: use a separately prepared genuine backup per run.
warm REPORT: resumes that paused original and checks all five retirements.
capture-sameg REPORT: after a REAL same-G action and ordinary save, while ACK
is pending, records the current positive publication and native actor semantics.
cold REPORT: after external ordinary quit/relaunch WITHOUT Reset, verifies that
the same original and later saved publication/native semantics survived.
Each invocation writes observations before asserting; no mutation request retry.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def observe(runner):
    topology = runner.call('/auditstate', domain='topology')['domains']['topology']
    need(topology.get('gameName') == 'CodexTest' and topology.get('server') is True
         and topology.get('remote') is False, 'authoritative disposable CodexTest required')
    row = runner.call('/auditstate', domain='npcRecovery')['domains']['npcRecovery']
    need(row.get('available') is True and row.get('authoritative') is True, 'actual recovery observation unavailable')
    return row


def command(runner, text):
    return runner.call('/console', 'POST', cmd='rbnpcrecoveryfixture ' + text + ' confirm=CodexTest')


def identity(row, baseline):
    for key in ('operation', 'generation'):
        need(row[key] == baseline[key], 'original changed: ' + key)


def heads(row):
    result = {}
    for store in row['stores']:
        publication = store['publication']
        need(publication.get('available') is True and publication.get('installed') is True
             and publication.get('pending') is False and publication.get('currentMatchesSaved') is True,
             'positive installed current publication required')
        need(publication['operation'] == row['operation'] and publication['generation'] == row['generation'],
             'publication original differs')
        result[store['file']] = {key: publication[key] for key in ('hash', 'sequence', 'currentHash')}
    need(result, 'pending actual members required')
    return result


def actors(row):
    need(row['actorCount'] <= row['actorLimit'] and row['actors'], 'complete actual native actor observation required')
    return {a['stable']: {key: a[key] for key in ('revision', 'order', 'ownership', 'name', 'nativeStable', 'equipment')}
            for a in row['actors']}


def run(args, report, runner):
    before = observe(runner)
    report.setdefault('observations', []).append(before); runner.save()
    if args.phase == 'boundary':
        need(args.step is not None and 0 <= args.step <= 4, 'exact step 0..4 required')
        need(before['pending'] is False, 'prepare a fresh genuine recovery original before each boundary')
        command(runner, 'arm rebirth_npc_equipment.xml cache-retired-' + str(args.step))
        command(runner, 'begin rebirth_npc_equipment.xml')
        deadline = time.monotonic() + 30
        while True:
            after = observe(runner)
            if after.get('fault', {}).get('paused') is True: break
            need(time.monotonic() < deadline, 'actual cache boundary was not reached'); time.sleep(.5)
        report['paused'] = after; runner.save()
        need(after['phase'] == 2 and after['pending'] is True and after['localInstalled'] is False
             and after['installing'] is True, 'group2 must retain local writer fence before all cache steps')
        cache = after['primitiveCaches']
        need(cache['operation'] == after['operation'] and cache['generation'] == after['generation']
             and cache['steps'] == 5 and cache['retired'] == args.step + 1 and cache['completed'] is False,
             'exact original retirement progress differs')
        need(cache['fenceCurrent'] is True and len(cache['stepStates']) == 5, 'exact active fence evidence required')
        for i, step in enumerate(cache['stepStates']):
            need(step['fenceCurrent'] is True and step['retirementCommits'] == (1 if i <= args.step else 0)
                 and step['detachCalls'] == (1 if i <= args.step else 0)
                 and step['preserveInstalled'] is False, 'actual ordered single cache retirement differs')
        need(after['fault']['point'] == 'cache-retired-' + str(args.step) and after['fault']['reached'] == 1,
             'wrong actual pause boundary')
        report['pausedHeads'] = heads(after)
        reply = command(runner, 'assertcachefenced')
        need('result=PASS actual group2 ordinary writer refusal' in json.dumps(reply),
             'actual ordinary writer refusal fixture did not pass')
        need(heads(observe(runner)) == report['pausedHeads'], 'writer probe changed actual publication')
    elif args.phase == 'warm':
        identity(before, report['paused'])
        need(before['fault']['paused'] is True and before['phase'] == 2, 'original paused group2 required')
        command(runner, 'release')
        deadline = time.monotonic() + 30
        while True:
            after = observe(runner)
            identity(after, before)
            need(after['phase'] in (None, 2, 3), 'warm retry regressed durable install phase')
            report['observations'].append(after); runner.save()
            if after['localInstalled'] or after['completed']: break
            need(time.monotonic() < deadline, 'warm cache install did not finish'); time.sleep(.5)
        if after['pending']:
            cache = after['primitiveCaches']
            need(cache['retired'] == 5 and cache['completed'] is True, 'all five actual retirements required')
            need(all(s['retirementCommits'] == 1 and s['detachCalls'] == 1 for s in cache['stepStates']),
                 'warm retry replayed a successful retirement')
            need(heads(after) == report['pausedHeads'], 'warm retry replaced installed primary publication')
        report['warm'] = after
    elif args.phase == 'capture-sameg':
        identity(before, report['paused'])
        need(before['phase'] == 2 and before['localInstalled'] is True, 'actual installed ACK-pending original required')
        current = heads(before)
        need(any(current[f]['sequence'] > report['pausedHeads'][f]['sequence'] for f in current),
             'perform a real sameG action AND ordinary save before capture')
        native = actors(before)
        need(native != actors(report['paused']), 'actual saved native semantic change required')
        report['sameg'] = {'observation': before, 'heads': current, 'actors': native, 'session': runner.scope}
    else:
        saved = report['sameg']
        need(runner.scope != saved['session'], 'new actual process session required for cold continuation')
        identity(before, saved['observation'])
        need(before['phase'] == 2 and before['localInstalled'] is True, 'retain actual ACK loss across cold continuation')
        need(heads(before) == saved['heads'], 'cold continuation reinstalled or changed latest sameG saved publication')
        need(actors(before) == saved['actors'], 'cold continuation lost actual sameG native semantics')
        report['cold'] = before


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=('boundary', 'warm', 'capture-sameg', 'cold'))
    parser.add_argument('report', type=Path)
    parser.add_argument('--step', type=int)
    parser.add_argument('--session', type=Path)
    args = parser.parse_args()
    report = {'status': 'NOT_RUN'} if args.phase == 'boundary' else json.loads(args.report.read_text(encoding='utf-8'))
    runner = Runner(report, args.report, session_path=args.session)
    try:
        run(args, report, runner)
        report.setdefault('phases', {})[args.phase] = 'PASS'
        report['status'] = 'PASS_' + args.phase.upper().replace('-', '_')
    except Exception as error:
        report.setdefault('phases', {})[args.phase] = 'FAIL'
        report['status'] = 'FAIL'
        report['failure'] = type(error).__name__ + ': ' + str(error)
        raise
    finally:
        runner.save()
