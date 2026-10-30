"""Separate native acceptance helper. Lead owns enclosing disposable launch/finally quit.
No automatic source creation, item/metadata mutation, launch, build, teleport or fixture reset.
Plan is offline; run-case requires pre-staged actual jars/source and real Secondary input.
"""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import time

WATER = {
    'murky': ('drinkJarRiverWater', 'unsafeWater'),
    'boiled': ('drinkJarBoiledWater', 'cleanWater'),
    'mineral': ('drinkJarPureMineralWater', 'mineralWater'),
    'empty': ('drinkJarEmpty', 'empty'),
}
ORDER = {'murky': 0, 'boiled': 1, 'mineral': 2}
DEPENDENCY_SHA = '8C8A530433AB11082343F5E4906CEB05C65A0A030DFFF241D07FADAC7599043A'


def matrix():
    rows = []
    for existing in WATER:
        for incoming in ORDER:
            result = incoming if existing == 'empty' else min((existing, incoming), key=ORDER.get)
            rows.append(dict(id=existing + '_from_' + incoming, existing=existing, incoming=incoming,
                             result=result, beforeMl=0 if existing == 'empty' else 375,
                             status='NOT_RUN' if incoming == 'murky' else 'UNREACHABLE_CURRENT_SOURCE',
                             reason='Current natural/AF/sink refill callers all pass Grade.Murky.' if incoming != 'murky' else 'Stage physical source and actual single vessel.'))
    rows += [dict(id='finite_empty_shortage375', existing='empty', incoming='murky', result='empty', beforeMl=0,
                  status='NOT_RUN', chain='Refill a separate375mL boiled jar from finite500mL; observe375mL remaining, then refuse empty500mL jar.'),
             dict(id='finite_empty_shortage0', existing='empty', incoming='murky', result='empty', beforeMl=0,
                  status='NOT_RUN', chain='Fill a separate empty jar from finite500mL; observe0mL, then refuse second empty jar.'),
             dict(id='finite_partial_shortage100', existing='boiled', incoming='murky', result='boiled', beforeMl=375,
                  status='UNQUALIFIED_NATIVE_SETUP', reason='Requires independently observed100mL finite source plus same-source successful control. No deterministic100mL staging command is authorized or invented here.')]
    return rows


def row(snapshot, slot):
    found = [r for r in snapshot['toolbelt'] if r['slot'] == slot]
    assert len(found) == 1, 'Exactly one observed fixture vessel in selected slot required'
    return found[0]


def validate(before, after, slot, result_grade, finite, shortage=False):
    """Independent acceptance oracle over actual observer records, not a runtime plan call."""
    a, b = row(before, slot), row(after, slot)
    for snapshot in (before, after):
        assert snapshot['schema'] == 1 and snapshot['observationOnly']
        assert snapshot['cursorEmpty'] and snapshot['cursor'] == []
        assert snapshot['selectedSlot'] == slot, 'Read the actual selected fixture slot'
        if finite:
            assert snapshot['authoritative'] and snapshot['source']['profile'] == 'unsafeWater'
            assert snapshot['source']['ml'] >= 0
    assert a['count'] == b['count'] == 1 and a['capacityMl'] == b['capacityMl'] == 500
    assert a['seed'] == b['seed'], 'Physical vessel seed must survive'
    def elsewhere(snapshot):
        return {(group, r['slot']): r for group in ('toolbelt', 'backpack') for r in snapshot[group]
                if group != 'toolbelt' or r['slot'] != slot}
    assert elsewhere(before) == elsewhere(after), 'Unrelated liquid inventory changed'
    if shortage:
        assert finite and a == b and before['source'] == after['source'], 'Shortage must preserve exact source/revision and vessel image'
        return
    assert (b['name'], b['profile']) == WATER[result_grade], 'Wrong purity/type (never upgrade dirty remainder)'
    assert b['ml'] == 500 and b['ml'] > a['ml'], 'A no-op or partial attempt is not successful native refill'
    assert b['nativeHash'] != a['nativeHash'], 'Actual native vessel image must change'
    if finite:
        added = b['ml'] - a['ml']
        assert before['source']['kind'] == after['source']['kind']
        assert before['source']['ml'] - after['source']['ml'] == added, 'Source must debit exact added volume'
        assert before['source']['ml'] + a['ml'] == after['source']['ml'] + b['ml'], 'Finite total water conservation'
        assert after['source']['revision'] > before['source']['revision'], 'Require a source commit, not just an item display change'


def dependency():
    path = pathlib.Path(__file__).with_name('player_report_liquids.py')
    assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == DEPENDENCY_SHA, 'Existing liquid helper moved; review compatibility first'
    spec = importlib.util.spec_from_file_location('water_matrix_existing_driver', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class NativeCase:
    def __init__(self, args):
        self.a = args
        self.r = dependency().Run(args)
        self.records = []

    def check_scope(self):
        state = self.r.call('state', '-Sections', 'world,player,stats')
        names = [d['gameName'] for d in self.r.walk(state) if 'gameName' in d]
        assert names == [self.a.confirm_disposable] and self.a.confirm_disposable.startswith(('CodexTest', 'RebirthBridge'))
        assert state['guard']['enabled'] and not state['player']['dead'], 'Guard must stay on'
        audit = self.r.call('console', 'rbskill audit')
        lines = [s.split('] ', 1)[1] for s in dependency_strings(audit) if s.startswith('[REBIRTH Skill Audit] ')]
        assert len(lines) == 1
        value = json.loads(lines[0])
        assert value['creation'] == self.a.creation and value['complete'] and not value['creationHeld'] and not value['remoteWorld']

    def target(self):
        value = self.r.call('target')['target']
        if self.a.source == 'tank':
            assert value.get('block', {}).get('position') == self.a.xyz, 'Aim at the actual loaded tank parent coordinate'
            return ('tank', tuple(self.a.xyz))
        if self.a.source == 'sink':
            assert value.get('entity', {}).get('id') == self.a.entity, 'Aim at the actual camper sink, not another entity'
            # Vehicle ID alone does not prove the physical sink part. Mandatory
            # successful mutation control supplies that proof before shortage.
            assert len(value.get('point', [])) == 3, 'Physical sink ray point must be observed'
            return ('sink', self.a.entity, tuple(round(float(v), 1) for v in value['point']))
        block = value.get('block', {})
        assert 'water' in (block.get('name', '') + ' ' + str(value.get('tag', ''))).lower(), 'Observed natural-water target required'
        assert block.get('position') is not None
        return ('natural', tuple(block['position']))

    def observe(self):
        return self.r.observe(self.a.source != 'natural')

    def pin(self, slot, existing, ml):
        reply = self.r.call('select', f'slot={slot+1}')
        assert reply.get('actualHand') == WATER[existing][0], 'Stored slot is insufficient; actual hand required'
        snapshot = self.observe()
        assert snapshot['selectedSlot'] == slot
        vessel = row(snapshot, slot)
        assert (vessel['name'], vessel['profile']) == WATER[existing]
        assert vessel['ml'] == ml and vessel['capacityMl'] == 500 and vessel['count'] == 1
        assert snapshot['cursorEmpty'] and snapshot['cursor'] == []
        return snapshot

    def gesture(self, slot, existing, ml, result_grade, shortage=False):
        self.check_scope()
        self.r.close_crafting()
        before = self.pin(slot, existing, ml)
        if self.a.source != 'natural':
            assert before['authoritative'] and before['source']['profile'] == 'unsafeWater'
            assert before['source']['ml'] == (self.shortage_ml if shortage else 500), 'Exact isolated reservoir precondition required'
        target = self.target()
        if shortage:
            assert target == self.successful_target, 'Shortage requires the same proven source focus as the successful control'
        self.records.append(dict(stage='before-input', snapshot=before, target=target))
        self.save()
        witness = self.r.call('press', 'action=Secondary', 'seconds=0.2')
        assert witness.get('inputWitness', {}).get('injectedFrames', 0) > 0, 'Require actual injected native input'
        # Successful operations may be asynchronous. Failure/no-op cannot pass a
        # positive case. Shortage checks wait through native10s admission bound.
        deadline = time.monotonic() + (13 if shortage else 20)
        after = None
        while time.monotonic() < deadline:
            time.sleep(1)
            after = self.observe()
            if not shortage:
                try:
                    validate(before, after, slot, result_grade, self.a.source != 'natural')
                except AssertionError:
                    continue
                break
        assert after is not None
        validate(before, after, slot, result_grade, self.a.source != 'natural', shortage)
        assert self.target() == target, 'Source focus changed across input'
        self.check_scope()
        if not shortage:
            self.successful_target = target
        self.records.append(dict(stage='after-native-refill' if not shortage else 'after-shortage-refusal', snapshot=after, target=target))
        self.save()
        return after

    def save(self):
        (self.r.out/'purity-checkpoints.json').write_text(json.dumps(self.records, indent=2), encoding='utf-8')

    def run(self, case):
        assert case['status'] == 'NOT_RUN', 'Case is explicitly unreachable/unqualified with current source setup'
        mark = self.r.call('log', '-Limit', '1')['lastLogSeq']
        if case['id'].startswith('finite_empty_shortage'):
            assert self.a.source != 'natural' and self.a.positive_slot != self.a.slot
            positive_grade = 'boiled' if case['id'].endswith('375') else 'empty'
            positive_ml = 375 if positive_grade == 'boiled' else 0
            after = self.gesture(self.a.positive_slot, positive_grade, positive_ml, 'murky')
            self.shortage_ml = after['source']['ml']
            assert self.shortage_ml == (375 if positive_grade == 'boiled' else 0)
            self.gesture(self.a.slot, 'empty', 0, 'empty', shortage=True)
        else:
            self.gesture(self.a.slot, case['existing'], case['beforeMl'], case['result'])
        errors = self.r.call('errors', '-Since', str(mark))
        assert not errors.get('entries', []), 'New native errors need review'
        self.r.call('screenshot', 'water_purity_' + case['id'])


def dependency_strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from dependency_strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from dependency_strings(child)


def main():
    if not __debug__:
        raise RuntimeError('Acceptance assertions must remain enabled; do not run Python with -O')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['plan', 'run-case'])
    parser.add_argument('--out', required=True)
    parser.add_argument('--case', choices=[r['id'] for r in matrix()])
    parser.add_argument('--bridge')
    parser.add_argument('--confirm-disposable')
    parser.add_argument('--creation')
    parser.add_argument('--source', choices=['natural', 'tank', 'sink'], default='tank')
    parser.add_argument('--xyz', type=int, nargs=3)
    parser.add_argument('--entity', type=int)
    parser.add_argument('--slot', type=int, default=0, help='Zero-based prepared destination slot')
    parser.add_argument('--positive-slot', type=int, default=1, help='Separate prepared positive-control vessel for shortage chain')
    args = parser.parse_args()
    output = pathlib.Path(args.out)
    if args.mode == 'run-case' and any((output/name).exists() for name in ('commands.jsonl', 'case-result.json', 'purity-checkpoints.json')):
        parser.error('Use a fresh evidence directory per native case; never overwrite previous results')
    output.mkdir(parents=True, exist_ok=True)
    (output/'matrix.json').write_text(json.dumps({'status': 'NOT_RUN', 'cases': matrix(), 'distilled': 'No authored distilled item/source; mineralWater is the highest existing grade, not proof of distilled-specific native coverage.'}, indent=2), encoding='utf-8')
    if args.mode == 'plan':
        print('Prepared15-case matrix; all native cases NOT RUN or explicitly blocked. No bridge actions.')
        return
    if not all((args.case, args.bridge, args.confirm_disposable, args.creation)):
        parser.error('run-case requires --case --bridge --confirm-disposable --creation')
    if args.source == 'tank' and args.xyz is None:
        parser.error('tank requires actual --xyz')
    if args.source == 'sink' and args.entity is None:
        parser.error('sink requires actual --entity')
    if min(args.slot, args.positive_slot) < 0:
        parser.error('owned slot indices must be nonnegative')
    case = next(c for c in matrix() if c['id'] == args.case)
    receipt = dict(case=case, status='NOT_RUN', source=args.source)
    try:
        if case['status'] != 'NOT_RUN':
            raise ValueError('UNREACHABLE SOURCE/UNQUALIFIED SETUP: no native action will be attempted')
        NativeCase(args).run(case)
        receipt['status'] = 'PASS_CASE_ONLY'
        print('PASS single native case ' + case['id'] + '; does not qualify blocked matrix cases')
    except Exception as error:
        receipt.update(status='FAIL_OR_PRECONDITION_BLOCKED', error=str(error))
        raise
    finally:
        (output/'case-result.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
