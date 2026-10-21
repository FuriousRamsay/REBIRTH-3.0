"""Bounded mounted-turret combat observation; parent prepares a real hostile.

No spawning, damage injection, forced aiming, inventory writes or screenshots.
Pass means correlated target damage and native consumption, not exclusive damage
attribution, audible quality, remote contention or moving-vehicle qualification.
"""
import argparse
import math
import os
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner


def need(value, message):
    if not value:
        raise AssertionError(message)


def ammo_transition(previous, current):
    """Reject transfers and incorrect selection; return observed depletion fallback."""
    old, new = previous['ammo'], current['ammo']
    need(len(old) == len(new) == 3, 'three native ammo slots required')
    for before, after in zip(old, new):
        need(0 <= after['count'] <= before['count'], 'ammo increased during isolated firing')
        need(after['count'] == 0 or after['item'] == before['item'], 'ammo identity changed during firing')
    loaded = previous['loadedSlot']
    if loaded < 0:
        need(current['loadedSlot'] == -1, 'empty turret acquired ammunition')
        return False
    # Sampling may span several shots, but an undepleted selected stack must
    # remain selected. Once empty, the native fallback starts at slot zero.
    expected = loaded if new[loaded]['count'] else next((i for i, a in enumerate(new) if a['count']), -1)
    if new[loaded]['count']:
        need(all(new[i]['count'] == old[i]['count'] for i in range(3) if i != loaded),
             'unselected ammo consumed before selected stack depleted')
    need(current['loadedSlot'] == expected, 'selected ammo/fallback order differs')
    return expected >= 0 and expected != loaded


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--entity', type=int, required=True)
    p.add_argument('--corner', type=int, choices=range(4), required=True)
    p.add_argument('--target', type=int, required=True)
    p.add_argument('--weapon', required=True)
    p.add_argument('--sound', required=True)
    p.add_argument('--interval', type=float, required=True,
                   help='Expected native weapon seconds per shot, independently established from loaded effects')
    p.add_argument('--require-kill', action='store_true')
    p.add_argument('--require-fallback', action='store_true',
                   help='Use variants sharing the expected weapon/sound; selected nonfirst stack must deplete')
    p.add_argument('--report', type=Path, required=True)
    args = p.parse_args()
    need(math.isfinite(args.interval) and args.interval > 0, 'positive finite expected native cadence required')
    need(not args.report.exists(), 'fresh evidence path required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),
         'parent-owned pinned session and exclusive batch lease required')
    report = {'passed': False, 'samples': [], 'scope': __doc__}
    bridge = Runner(report, args.report)
    started = time.monotonic()
    identity = None
    first = None
    previous = None
    fallback = False
    try:
        while time.monotonic() - started < 20:
            state = bridge.call('/camperturrets', entity=args.entity, assets=0)
            target = bridge.call('/entities', entity=args.target)['entities'][0]
            key = tuple(target[k] for k in ('worldSession', 'entityInstance', 'entityId'))
            if identity is None:
                identity = key
            need(key == identity, 'target/world instance changed')
            need(state['authoritative'], 'authoritative mounted runtime required')
            gun = state['corners'][args.corner]
            need(gun['installed'] and gun['enabled'] and gun['runtimeBound'], 'active bound turret required')
            need(all(not c['installed'] or not c['enabled'] for c in state['corners']
                     if c['corner'] != args.corner), 'disable other mounted turrets before this isolated test')
            need(gun['controllerEntity'] is None, 'automatic case cannot qualify manual firing')
            for angle in ('yaw', 'pitch'):
                need(gun[angle + 'Min'] - .1 <= gun[angle] <= gun[angle + 'Max'] + .1,
                     angle + ' escaped measured rig limits')
            sample = {'time': state['time'], 'target': target, 'gun': gun}
            report['samples'].append(sample)
            if first is None:
                first = sample
                need(target['alive'], 'target must initially be alive')
                if args.require_fallback:
                    need(gun['loadedSlot'] > 0 and gun['ammo'][0]['count'] > 0,
                         'fallback case requires selected later slot and populated first slot')
            if previous is not None:
                fallback |= ammo_transition(previous, gun)
            previous = gun
            spent = sum(a['count'] for a in first['gun']['ammo']) - sum(a['count'] for a in gun['ammo'])
            shots = gun['shotsConsumed'] - first['gun']['shotsConsumed']
            need(spent == shots and spent >= 0, 'native ammo consumption/shot counter differs')
            if shots:
                need(gun['firingWeapon'] == args.weapon and gun['firingSound'] == args.sound,
                     'actual shot used unexpected native weapon/sound profile')
                need(math.isfinite(gun['shotInterval']) and
                     abs(gun['shotInterval'] - args.interval) <= max(.0001, args.interval * .001),
                     'actual configured cadence differs from expected native weapon cadence')
            if spent > 0 and target['health'] < first['target']['health']:
                if (not args.require_kill or not target['alive']) and (not args.require_fallback or fallback):
                    report['fallbackObserved'] = fallback
                    report['passed'] = True
                    break
            time.sleep(.1)
        need(report['passed'], 'deadline: no qualifying damage/consumption or required kill')
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        report['seconds'] = round(time.monotonic() - started, 3)
        bridge.save()
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
