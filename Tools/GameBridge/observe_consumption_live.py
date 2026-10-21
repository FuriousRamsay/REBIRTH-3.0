"""Capture/compare real player use; performs GET observations only, no gameplay input."""
import argparse
import json
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def observe():
    session = json.loads((ROOT / 'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
    request = urllib.request.Request(
        f"http://127.0.0.1:{session['port']}/state?" + urllib.parse.urlencode({'sections': 'consumption'}),
        headers={'X-Bridge-Token': session['token']})
    with urllib.request.urlopen(request, timeout=30) as response:
        reply = json.load(response)
    assert reply.get('ok') is True, 'Bridge observation failed'
    value = reply.get('consumption')
    assert isinstance(value, dict) and value.get('schema') == 1, 'Missing consumption schema 1'
    assert value.get('remote') is True, 'This regression requires a non-host client'
    return value


def compare(before, after, location, slot, treatment, nutrition):
    assert before['ownerEntityId'] == after['ownerEntityId'], 'Owner changed'
    assert before['remote'] is True and after['remote'] is True, 'Non-host observations required'
    prior = before[location][slot]
    current = after[location][slot]
    assert prior['slot'] == slot and current['slot'] == slot and prior['count'] > 0, 'Invalid source slot'
    assert current['count'] == prior['count'] - 1, 'Exact source was not consumed once'
    if current['count']:
        for key in ('type', 'seed', 'nativeHash', 'name'):
            assert current[key] == prior[key], f'Source identity changed: {key}'
    else:
        assert current['type'] == 0 and current['nativeHash'] is None, 'Consumed cell was not empty'
    for where in ('toolbelt', 'backpack'):
        assert len(before[where]) == len(after[where]), 'Inventory capacity changed'
        for index, (left, right) in enumerate(zip(before[where], after[where])):
            if where != location or index != slot:
                assert left == right, f'Unrelated source changed: {where}[{index}]'
    old, new = before.get('metabolism'), after.get('metabolism')
    if old is not None and new is not None:
        assert old['ownerEntityId'] == new['ownerEntityId'] == before['ownerEntityId'], 'Projection owner mismatch'
        assert old['creationId'] == new['creationId'], 'Character scope changed'
        assert new['sequence'] >= old['sequence'], 'Snapshot sequence regressed'
    if nutrition:
        assert isinstance(old, dict) and isinstance(new, dict), 'Scoped metabolism projection unavailable'
        assert new['sequence'] > old['sequence'], 'No new metabolism projection'
        assert (new['pendingFood'] > old['pendingFood'] or new['food'] > old['food']), 'No observed nutrition intake/gain'
    if treatment:
        assert prior['name'] == 'foodHoney', 'Honey treatment assertion requires foodHoney source'
        assert before['cvars']['infectionCounter'] > 0, 'Infection must exist before honey use'
        assert (after['cvars']['$infectionCureCounter'] > before['cvars']['$infectionCureCounter']
                or after['cvars']['$buffInfectionAddCurePerc'] > before['cvars']['$buffInfectionAddCurePerc']
                or (before['cvars']['$infectionCureCounter'] == 0
                    and after['cvars']['infectionCounter'] < before['cvars']['infectionCounter'])
                or ('buffInfectionAddCure' in after['buffs'] and 'buffInfectionAddCure' not in before['buffs'])), \
            'No new authored infection cure payload/buff observed'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('capture', 'check'))
    parser.add_argument('baseline', type=Path)
    parser.add_argument('--location', choices=('toolbelt', 'backpack'), required=True)
    parser.add_argument('--slot', type=int, required=True, help='Zero-based native slot')
    parser.add_argument('--nutrition', action='store_true')
    parser.add_argument('--honey', action='store_true')
    args = parser.parse_args()
    assert args.slot >= 0, 'Slot must be nonnegative'
    after = observe()
    if args.mode == 'capture':
        assert not args.baseline.exists(), 'Refusing to replace an existing baseline'
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        args.baseline.write_text(json.dumps(after, indent=2), encoding='utf-8')
        print('Captured non-host source; perform one real Use, then check promptly.')
    else:
        before = json.loads(args.baseline.read_text(encoding='utf-8-sig'))
        evidence = args.baseline.with_name(args.baseline.stem + '_after.json')
        evidence.write_text(json.dumps(after, indent=2), encoding='utf-8')
        compare(before, after, args.location, args.slot, args.honey, args.nutrition)
        print('PASS observed exact source debit and requested local effects; no server receipt/network qualification claimed.')


if __name__ == '__main__':
    main()
