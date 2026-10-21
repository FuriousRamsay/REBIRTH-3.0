"""Opt-in genuine non-host held-item consumption; prepared inventory/infection required."""
import argparse
import json
import time
import urllib.parse
import urllib.request
from pathlib import Path
from observe_consumption_live import ROOT, observe, compare


def call(endpoint, method='GET', **args):
    session = json.loads((ROOT / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    request = urllib.request.Request(
        f"http://127.0.0.1:{session['port']}{endpoint}?" + urllib.parse.urlencode(args),
        headers={'X-Bridge-Token': session['token']}, method=method,
        data=b'' if method == 'POST' else None)
    with urllib.request.urlopen(request, timeout=30) as response:
        value = json.load(response)
    assert value.get('ok') is True, 'Bridge call failed: ' + endpoint
    return value


def idle(timeout=20):
    deadline = time.monotonic() + timeout
    quiet_since = None
    while time.monotonic() < deadline:
        guard = call('/guard')
        assert guard.get('enabled') is True, 'Guard must remain enabled'
        quiet_since = None if guard.get('busy') is not False else (quiet_since or time.monotonic())
        if quiet_since and time.monotonic() - quiet_since >= 1:
            return
        time.sleep(0.25)
    raise AssertionError('Guard did not become idle; prepare a clear safe test area')


def run(args, evidence):
    status = call('/ping')
    assert status['state'] == 'ingame', 'Prepared client must already be ingame'
    evidence['logStart'] = status['lastLogSeq']
    evidence['surroundings'] = call('/surroundings')
    evidence['openWindowsBefore'] = call('/state', sections='ui').get('openWindows')
    idle()
    before = observe()  # Existing helper requires actual remote=true.
    evidence['before'] = before
    source = before['toolbelt'][args.slot]
    assert source['name'] == args.item and source['count'] == args.count, 'Prepare exact named toolbelt stack/count before running'
    assert source['nativeHash'], 'Exact native source image unavailable'
    assert isinstance(before.get('metabolism'), dict), 'Scoped metabolism projection required'
    if args.honey:
        assert args.item == 'foodHoney' and before['cvars']['infectionCounter'] > 0, 'Honey requires preexisting actual infection'
        assert before['cvars']['$infectionMaxDuration'] > 0, 'Native infection must be initialized'
        assert before['cvars']['$infectionCureCounter'] == 0, 'Start with no existing infection cure'
        if args.count == 2:
            assert before['cvars']['infectionCounter'] > before['cvars']['$infectionMaxDuration'] * 0.10, \
                'Two treatment servings require infection greater than both 5% honey doses; use count1 for mild infection'
    evidence['selection'] = call('/select', 'POST', slot=args.slot + 1)
    time.sleep(1.2)
    evidence['uses'] = []
    for index in range(args.count):
        idle()
        baseline = observe()
        assert baseline['toolbelt'][args.slot]['count'] == args.count - index, 'Source changed before genuine use'
        if index:
            assert baseline['toolbelt'] == evidence['uses'][-1]['after']['toolbelt'] and baseline['backpack'] == evidence['uses'][-1]['after']['backpack'], 'Source identity changed between genuine uses'
        if args.honey:
            assert baseline['cvars']['infectionCounter'] > 0, 'Each honey dose requires remaining infection'
        row = {'before': baseline, 'observations': []}
        evidence['uses'].append(row)
        row['input'] = call('/press', 'POST', action='Primary', seconds=0.2)
        deadline = time.monotonic() + 12
        last_failure = 'No observation'
        while time.monotonic() < deadline:
            after = observe()
            row['observations'].append(after)
            try:
                compare(baseline, after, 'toolbelt', args.slot, args.honey, True)
                row['after'] = after
                break
            except AssertionError as failure:
                last_failure = str(failure)
                if after['toolbelt'][args.slot]['count'] < baseline['toolbelt'][args.slot]['count'] - 1:
                    raise AssertionError('More than one serving consumed by one input')
            time.sleep(0.25)
        else:
            raise AssertionError('Genuine use failed: ' + last_failure)
        # Observe without another input; detect an accidental second consumption.
        quiet_deadline = time.monotonic() + 3
        while time.monotonic() < quiet_deadline:
            quiet = observe()
            row['observations'].append(quiet)
            assert quiet['ownerEntityId'] == baseline['ownerEntityId'], 'Owner changed'
            assert quiet['toolbelt'] == row['after']['toolbelt'] and quiet['backpack'] == row['after']['backpack'], 'Inventory changed without another use'
            time.sleep(0.4)
        if args.honey:
            # A payload/buff alone is insufficient: each genuine serving must reduce
            # native infection, including the final dose after the source becomes empty.
            row['infectionProgression'] = []
            row['infectionReduced'] = row['after']['cvars']['infectionCounter'] < baseline['cvars']['infectionCounter']
            deadline = time.monotonic() + 15
            while not row['infectionReduced'] and time.monotonic() < deadline:
                value = observe()
                row['infectionProgression'].append(value)
                assert value['toolbelt'] == row['after']['toolbelt'] and value['backpack'] == row['after']['backpack'], 'Inventory changed while observing cure progression'
                assert value['ownerEntityId'] == baseline['ownerEntityId'], 'Owner changed during cure progression'
                row['infectionReduced'] = value['cvars']['infectionCounter'] < baseline['cvars']['infectionCounter']
                time.sleep(0.5)
            assert row['infectionReduced'], 'Cure payload observed but this serving did not reduce actual infection'
    if args.honey:
        evidence['infectionReduced'] = all(row['infectionReduced'] for row in evidence['uses'])
    evidence['screenshot'] = call('/screenshot', 'POST', name='consumption_' + args.item + '_final')
    evidence['errors'] = call('/log', since=evidence['logStart'], level='error')
    assert evidence['errors']['count'] == 0, 'New game errors appeared'
    evidence['duplicateTransportTest'] = 'Not performed: no consumption request/resend bridge route is exposed; quiet observations are not a duplicate packet test.'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--item', required=True)
    parser.add_argument('--count', type=int, choices=(1, 2), required=True, help='Exact prepared starting count; consumes this many genuine servings')
    parser.add_argument('--slot', type=int, default=0, help='Zero-based prepared toolbelt slot')
    parser.add_argument('--honey', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    assert args.slot >= 0 and not args.output.exists(), 'Nonnegative slot and fresh evidence path required'
    evidence = {'schema': 1, 'item': args.item, 'initialCount': args.count, 'slot': args.slot, 'pass': False,
                'scope': 'Genuine remote-client input and owner observation; no server receipt proof or synthetic packet injection'}
    try:
        run(args, evidence)
        evidence['pass'] = True
        print('PASS genuine source debit/nutrition' + ('/infection reduction' if args.honey else ''))
    except Exception as failure:
        evidence['failure'] = str(failure)
        raise
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(evidence, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
