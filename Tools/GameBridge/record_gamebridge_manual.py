"""Reserve bridge control while the human tests, then record explicit verdict/evidence."""
import argparse
import json
from pathlib import Path
import sys
import time
import uuid
from run_gamebridge_batch import HERE, OUT, BatchLock, Transport, read, write


def capture(bridge, directory, label):
    write(directory / (label + '-state.json'), bridge.call('/state', sections='player,stats,inventory,world,ui'))
    write(directory / (label + '-ui.json'), bridge.call('/ui/tree'))
    write(directory / (label + '-screenshot.json'), bridge.call('/screenshot', 'POST', name=label))


def begin(args):
    plan = read(args.plan)
    cases = {c['id']: c for c in plan['cases']}
    if args.case not in cases:
        raise ValueError('Unknown case; use its inventory id')
    bridge = Transport(args.session)
    ping = bridge.call('/ping')
    if ping.get('state') != 'ingame' or ping.get('version') != '1.1.0':
        raise RuntimeError('Load bridge 1.1.0 in the disposable test save first')
    run_id = 'manual-' + time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]
    directory = OUT / run_id
    lease = bridge.call('/batch', 'POST', action='acquire', run=run_id)
    bridge.run_token = lease['runToken']
    control = directory / 'control.json'
    write(control, {'session': str(bridge.path), 'runToken': bridge.run_token, 'run': run_id})
    try:
        world = bridge.call('/state', sections='world')['world']
        if world.get('gameName') != plan['save'] or world.get('gameWorld') != plan['world']:
            raise RuntimeError('Wrong disposable world/save')
        bridge.call('/stop', 'POST')
        bridge.call('/guard', 'POST', enabled=1)
        capture(bridge, directory, 'before')
        report = {'run': run_id, 'case': args.case, 'scope': cases[args.case].get('scope'), 'reference': cases[args.case].get('file'),
                  'status': 'WAITING_FOR_OPERATOR', 'session': bridge.identity, 'world': world,
                  'startSeq': ping['lastLogSeq'], 'startedUtc': time.time()}
        write(directory / 'report.json', report)
    except BaseException:
        bridge.call('/batch', 'POST', action='release')
        control.unlink(missing_ok=True)
        raise
    print(json.dumps({'status': 'WAITING_FOR_OPERATOR', 'control': str(control), 'report': str(directory / 'report.json')}))


def finish(args):
    control = read(args.control)
    directory = args.control.parent
    report = read(directory / 'report.json')
    bridge = Transport(control['session'])
    bridge.run_token = control['runToken']
    if bridge.identity != report['session']:
        raise RuntimeError('Manual test session changed; preserve evidence and do not adopt another process')
    capture(bridge, directory, 'after')
    errors = bridge.call('/log', since=report['startSeq'], level='error', limit=1000)
    warnings = bridge.call('/log', since=report['startSeq'], level='warning', limit=1000)
    write(directory / 'errors.json', errors)
    write(directory / 'warnings.json', warnings)
    after = read(directory / 'after-state.json')['world']
    same_world = all(after.get(k) == report['world'].get(k) for k in ('gameName', 'gameWorld'))
    report.update(operatorVerdict=args.verdict, operatorNotes=args.notes, finishedUtc=time.time(),
                  status=('MANUAL_PASS' if args.verdict == 'PASS' and errors.get('count') == 0 and same_world else 'FAIL'),
                  errorCount=errors.get('count'), warningCount=warnings.get('count'))
    write(directory / 'report.json', report)
    bridge.call('/stop', 'POST')
    bridge.call('/batch', 'POST', action='release')
    args.control.unlink()
    print(json.dumps({'status': report['status'], 'report': str(directory / 'report.json')}))
    return 0 if report['status'] == 'MANUAL_PASS' else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    start = sub.add_parser('begin')
    start.add_argument('--case', required=True)
    start.add_argument('--plan', type=Path, default=HERE / 'batches/current.json')
    start.add_argument('--session', type=Path, default=HERE / 'out/session.json')
    end = sub.add_parser('finish')
    end.add_argument('--control', type=Path, required=True)
    end.add_argument('--verdict', choices=['PASS', 'FAIL'], required=True)
    end.add_argument('--notes', required=True)
    args = parser.parse_args()
    with BatchLock():
        return begin(args) if args.command == 'begin' else finish(args)


if __name__ == '__main__':
    sys.exit(main())
