"""Strict native drag acceptance; requires parent-owned open camper workspace.

The parent supplies one exact backpack stack and an empty installed ammo slot.
No launch, setup cheats, direct inventory writes or fallback click are performed.
"""
import argparse
import json
import os
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner


def need(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entity', type=int, required=True)
    parser.add_argument('--corner', type=int, required=True, choices=range(4))
    parser.add_argument('--slot', type=int, required=True, choices=range(3))
    parser.add_argument('--item', required=True)
    parser.add_argument('--count', type=int, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    need(args.count > 0 and not args.report.exists(), 'positive count and fresh report required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),
         'parent exclusive session required')
    bridge = Runner({},args.report.with_suffix('.requests.json'),session_path=os.environ['REBIRTH_BRIDGE_SESSION'])

    report = {'schema': 1, 'passed': False, 'session': bridge.scope,
              'scope': 'strict native drag and observed exact ammo transfer/conservation'}
    started = time.monotonic()

    def observe():
        need(time.monotonic() - started < 20, 'drag case exceeded 20 seconds')
        state = bridge.call('/camperturrets', entity=args.entity)
        view = state.get('presentation') or {}
        need(view.get('open') and not view.get('cameraActive'), 'actual management workspace must remain open')
        return state

    def total(actor, state):
        return sum(cell.get('count', 0) for area in ('backpack', 'toolbelt')
                   for cell in actor[area] if cell.get('name') == args.item) + sum(
                       cell['count'] for corner in state['corners'] for cell in corner['ammo']
                       if cell.get('item') == args.item)

    try:
        need(bridge.call('/ping')['state'] == 'ingame', 'game not ready')
        before = observe()
        actor = bridge.call('/state', sections='inventory')
        report.update(before=before, actorBefore=actor)
        target = before['corners'][args.corner]
        need(target['installed'] and not target['ammo'][args.slot]['count'], 'installed turret and empty destination required')
        need(before['presentation']['cursorCount'] == 0, 'native cursor must start empty')
        nodes = bridge.call('/ui/tree')['nodes']
        source = [n for n in nodes if n.get('ctrl') == 'XUiC_RebirthCamperTurretBackpackSlot'
                  and (n.get('item') or {}).get('name') == args.item]
        need(len(source) == 1 and source[0]['item']['count'] == args.count, 'one exact visible source stack required')
        destination = [n for n in nodes if n.get('id') == f'turretAmmo{args.corner}_{args.slot}' and n.get('clickable')]
        need(len(destination) == 1, 'one visible destination required')
        report['input'] = bridge.call('/ui/drag', 'POST', strict=1, trace=1,
                                     **{'from.path': source[0]['path'], 'to.path': destination[0]['path']})
        deadline = time.monotonic() + 2
        while True:
            after = observe()
            cell = after['corners'][args.corner]['ammo'][args.slot]
            if cell.get('item') == args.item and cell['count'] == args.count:
                break
            need(time.monotonic() < deadline, 'mouse drag completed but actual destination did not receive stack')
            time.sleep(.08)
        actor_after = bridge.call('/state', sections='inventory')
        report.update(after=after, actorAfter=actor_after)
        need(after['presentation']['cursorCount'] == 0, 'native cursor retained ammunition')
        need(total(actor, before) == total(actor_after, after), 'ammunition conservation failed')
        need(report['input'].get('placedByClick') is False, 'strict drag used a fallback click')
        report['passed'] = True
    except Exception as error:
        report['failure'] = type(error).__name__ + ': ' + str(error)
        try:
            report['failureState'] = observe()
            report['failureActor'] = bridge.call('/state', sections='inventory')
        except Exception as capture_error:
            report['captureFailure'] = str(capture_error)
    finally:
        report['seconds'] = round(time.monotonic() - started, 3)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())

