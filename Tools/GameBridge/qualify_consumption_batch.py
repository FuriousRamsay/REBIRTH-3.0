"""Parent-run four-case genuine remote consumption batch; explicit setup between cases."""
import argparse
import json
from pathlib import Path
from types import SimpleNamespace
from qualify_consumption_live import run
from consumption_trace_live import endpoints, start, finish


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--food', required=True, help='Authored food item with nutrition intake; not foodHoney')
    parser.add_argument('--slot', type=int, default=0, help='Zero-based toolbelt slot')
    parser.add_argument('--output', type=Path, required=True, help='Fresh evidence directory')
    parser.add_argument('--trace', action='store_true', help='Explicitly enable bounded native diagnostics per case')
    parser.add_argument('--server-session', type=Path, help='Genuine separate authoritative server bridge session file (requires --trace)')
    parser.add_argument('--server-player', type=int, help='Explicit current remote owner entity ID on server; supports dedicated server observation')
    parser.add_argument('--server-url', help='Explicit server bridge base URL when on another machine; token stays in session file')
    args = parser.parse_args()
    assert args.food != 'foodHoney' and args.slot >= 0, 'Distinct food and valid slot required'
    assert not args.server_session or args.trace, '--server-session requires explicit --trace'
    assert not args.server_url or args.server_session, '--server-url requires server session credentials'
    assert args.server_player is None or args.server_session, '--server-player requires actual server session'
    assert not args.output.exists(), 'Fresh evidence directory required'
    args.output.mkdir(parents=True)
    summary = {'schema': 1, 'pass': False, 'cases': [],
               'scope': 'Real remote client input/native owner observations; no server receipt or duplicate transport qualification'}
    try:
        for item, honey in ((args.food, False), ('foodHoney', True)):
            for count in (1, 2):
                label = ('honey' if honey else 'food') + '_count' + str(count)
                print(f'Prepare disposable non-host client: exactly {count} {item} in toolbelt slot {args.slot + 1}; clear safe area, guard ON, gameplay windows closed, stomach room.')
                if honey:
                    print('Prepare initialized untreated native infection (curecounter 0); count2 needs infection above 10% to qualify both doses. Do not apply any other treatment during observations.')
                answer = input('Enter READY when prepared, or anything else to stop: ')
                assert answer == 'READY', 'Preparation not confirmed; batch stopped'
                evidence = {'schema': 1, 'item': item, 'initialCount': count, 'slot': args.slot, 'pass': False}
                summary['cases'].append({'name': label, 'file': label + '.json', 'pass': False})
                trace_endpoints = []
                try:
                    if args.trace:
                        trace_endpoints = endpoints(args)
                        start(trace_endpoints, evidence)
                    run(SimpleNamespace(item=item, count=count, slot=args.slot, honey=honey), evidence)
                    evidence['pass'] = True
                    summary['cases'][-1]['pass'] = True
                except Exception as failure:
                    evidence['failure'] = str(failure)
                    raise
                finally:
                    if trace_endpoints:
                        finish(trace_endpoints, evidence)
                    (args.output / (label + '.json')).write_text(json.dumps(evidence, indent=2), encoding='utf-8')
        summary['pass'] = True
        print('PASS all four genuine remote prepared cases')
    except Exception as failure:
        summary['failure'] = str(failure)
        raise
    finally:
        (args.output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
